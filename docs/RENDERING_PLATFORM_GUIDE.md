# FC-Revolution 现代化渲染方案

> **版本**: 2.0 | **更新**: 2026-03-31
> **当前实现目标**: macOS Apple Silicon（其他平台接口预留，后续推进）
> **核心理念**: 不走传统模拟器老路——用现代图形技术重新诠释复古内容，而非放大像素块

---

## 目录

1. [设计哲学](#设计哲学)
2. [渲染架构总览](#渲染架构总览)
3. [核心技术：PPU 分层渲染](#核心技术ppu-分层渲染)
4. [macOS Metal 实现方案（当前目标）](#macos-metal-实现方案当前目标)
5. [Avalonia/.NET 集成](#avalonia-net-集成)
6. [项目目录结构（跨平台预留）](#项目目录结构跨平台预留)
7. [其他平台预留方案](#其他平台预留方案)
8. [实现路径](#实现路径)
9. [效果预期](#效果预期)
10. [能力系统与配置策略（Capability + Policy）](#能力系统与配置策略capability--policy)
11. [用户体验模型（UX）](#用户体验模型ux)
12. [NES 渲染关键陷阱（必读）](#nes-渲染关键陷阱必读)
13. [Debug Renderer（参考渲染器，必须实现）](#debug-renderer参考渲染器必须实现)

---

## 设计哲学

### 传统路径 vs 现代化路径

传统模拟器的渲染方式：
```
NES PPU → 256×240 像素帧 → 双线性/双三次放大 → 显示
                          → CRT 着色器 → 显示
                          → xBR/HQx 像素艺术滤镜 → 显示
```

这些方案本质上都是在**对信息丢失后的产物做修补**，效果天花板有限。

FC-Revolution 的目标路径：
```
NES PPU 元数据（精灵列表 / Tilemap / 调色板 / 运动数据）
    ↓
GPU 分层重绘（每个精灵、每个图块以原生 4K 分辨率重新渲染）
    ↓
现代后处理（MetalFX Temporal 超分 + 可选法线光照）
    ↓
现代主机级画面输出
```

关键差异：**不是放大，是重新绘制**。NES 的图块和精灵数据是精确的，我们在 GPU 上直接以目标分辨率绘制，超分辨率算法用于润色而非救场。

### 对帧生成（Frame Generation）的定位

NES 本身是固定 60fps 游戏，帧生成不改变游戏逻辑帧率，只插入中间帧。
对于 NES 这种动画帧数极低的内容（精灵动画通常 1-3 帧间隔），过度插帧反而产生"肥皂剧效应"。

**结论**：帧生成是可选的锦上添花功能，优先级排在画质方案之后。macOS 当前阶段 MetalFX 也暂无帧生成 API，无需纠结。

---

## 渲染架构总览

```
┌─────────────────────────────────────────────────────────────────────┐
│                         FC-Revolution 渲染管线                       │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  NesConsole.RunFrame()                                               │
│       │                                                              │
│       ├── PPU 仿真 → FrameMetadata（精灵列表 / Tilemap / 调色板）    │
│       │                    │                                         │
│       │              ┌─────▼──────────────────────────┐             │
│       │              │   IRenderDataExtractor          │             │
│       │              │   (C# 层，从 PPU 提取元数据)    │             │
│       │              └─────┬──────────────────────────┘             │
│       │                    │                                         │
│       │              ┌─────▼──────────────────────────┐             │
│       │              │   ILayeredRenderer              │             │
│       │              │   ├── macOS: MetalLayeredRenderer│            │
│       │              │   ├── Win:   VulkanLayeredRenderer│           │  ← 预留
│       │              │   └── 兜底:  SoftwareRenderer   │            │  ← 预留
│       │              └─────┬──────────────────────────┘             │
│       │                    │                                         │
│       │         ┌──────────▼───────────────────┐                    │
│       │         │  GPU 渲染管线                 │                    │
│       │         │  1. 背景 Tilemap 层（4K 重绘）│                    │
│       │         │  2. 精灵层（实例化渲染）       │                    │
│       │         │  3. 后处理（锐化 / 光照）     │                    │
│       │         │  4. MetalFX Temporal 超分     │ ← macOS 主路径     │
│       │         └──────────────────────────────┘                    │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 核心技术：PPU 分层渲染

### 为什么要分层

NES PPU 输出的 256×240 像素帧是**已经合成好的最终结果**，信息已经丢失（精灵边界、图块索引、运动信息都不在其中）。

要做到现代化画质，必须在 PPU 合成**之前**拦截原始数据：

| 数据 | 来源 | 用途 |
|------|------|------|
| OAM（Object Attribute Memory） | PPU 精灵列表，64 个精灵的坐标/图块/翻转 | GPU 精灵实例化渲染 |
| Nametable | 背景 Tilemap，32×30 图块索引 | 背景高分辨率重绘 |
| Pattern Table | CHR ROM/RAM，8×8 图块像素数据 | 纹理生成 |
| Palette | 调色板寄存器，当前帧颜色 | GPU 颜色映射 |
| 精灵运动矢量 | 当前帧与上一帧 OAM 坐标差 | MetalFX Temporal 运动矢量输入 |

### PPU 元数据提取接口（C# 层）

```csharp
// FC-Revolution.Rendering/Abstractions/IFrameMetadata.cs
public interface IFrameMetadata
{
    ReadOnlySpan<SpriteEntry> Sprites { get; }       // OAM，最多 64 个
    ReadOnlySpan<byte>        Nametable { get; }     // 32×30 图块索引
    ReadOnlySpan<byte>        PatternTable { get; }  // CHR 数据，512 个 8×8 图块
    ReadOnlySpan<uint>        Palette { get; }       // 32 个调色板颜色（ARGB）
    ReadOnlySpan<Vector2>     MotionVectors { get; } // 64 个精灵运动矢量
}

public readonly struct SpriteEntry
{
    public byte Y        { get; init; }
    public byte TileId   { get; init; }
    public byte Attrs    { get; init; }  // 翻转、调色板、优先级
    public byte X        { get; init; }
}
```

```csharp
// FC-Revolution.Rendering/Abstractions/IRenderDataExtractor.cs
public interface IRenderDataExtractor
{
    IFrameMetadata Extract(IPpu ppu, IFrameMetadata? previousFrame);
}
```

### 背景 Tilemap GPU 渲染思路

```
Nametable (32×30 图块) → 每个图块对应 Pattern Table 中一个 8×8 tile
    ↓
在目标分辨率（如 3840×2160）上，一个图块对应多少像素？
    NES 背景图块 = 8×8 像素，目标 4K 下放大比例 = 3840/256 ≈ 15x
    即每个图块渲染为 ~120×120 像素区域（GPU 绘制，非放大）

GPU 渲染策略：
    - 每个图块 = 一个 Quad（两个三角形）
    - CHR 数据转为 GPU 纹理（512 个 8×8 tile → 纹理图集）
    - 使用 GPU 纹理采样 + 调色板颜色映射
    - 支持亚像素精确度（滚动坐标的小数部分）
```

### 精灵 GPU 渲染思路（GPU Instancing）

```
OAM 64 个精灵 → 每个精灵对应一个 8×8 或 8×16 图块
    ↓
GPU Instancing：一次 DrawInstanced(64) 调用渲染所有精灵
    - Instance Buffer：精灵坐标、图块 ID、翻转标志、调色板
    - Vertex Shader：根据 instance 数据定位精灵位置
    - Fragment Shader：从 CHR 纹理采样、映射调色板颜色
    - 支持精灵优先级（背景前/后）
```

### 运动矢量生成

```csharp
// 每帧比较精灵坐标差，生成运动矢量供 MetalFX Temporal 使用
public Vector2[] GenerateSpriteMotionVectors(
    ReadOnlySpan<SpriteEntry> current,
    ReadOnlySpan<SpriteEntry> previous,
    float scaleX, float scaleY)
{
    var vectors = new Vector2[64];
    for (int i = 0; i < 64; i++)
    {
        float dx = (current[i].X - previous[i].X) * scaleX;
        float dy = (current[i].Y - previous[i].Y) * scaleY;
        vectors[i] = new Vector2(dx, dy);
    }
    return vectors;
}
```

---

## macOS Metal 实现方案（当前目标）

> 本节为当前阶段的完整实现规格。其他平台方案见[预留章节](#其他平台预留方案)。

### 技术选型

```
渲染 API:        Metal（macOS 13+ / Metal 3）
超分辨率:        MetalFX Temporal Scaler（高质量）
                 MetalFX Spatial Scaler（M1 基础款低开销备选）
帧生成:          ❌ MetalFX 当前版本无帧生成 API（非硬件限制，API 未开放）
C# 集成方式:     Objective-C 动态库 (dylib) + P/Invoke
Avalonia 接入:   NativeControlHost + CAMetalLayer
```

### 整体渲染流程

```
NesConsole.RunFrame()
    │
    ├── PPU 仿真完成 → IRenderDataExtractor.Extract()
    │                        │
    │                  IFrameMetadata（精灵/Tilemap/CHR/调色板/运动矢量）
    │                        │
    │                  MetalLayeredRenderer.Render(metadata)
    │                        │
    │              ┌─────────▼──────────────────────────────┐
    │              │  Metal GPU 管线                         │
    │              │                                         │
    │              │  1. 上传 CHR 纹理（MTLTexture，512 tile）│
    │              │  2. 背景 Tilemap Pass                   │
    │              │     → 32×30 Quad 绘制（目标分辨率坐标） │
    │              │  3. 精灵 Pass（DrawInstanced）          │
    │              │     → 64 精灵实例化渲染                 │
    │              │  4. 后处理 Pass（可选锐化 / 色调映射）  │
    │              │  5. MetalFX Temporal Scaler             │
    │              │     输入: 上一步渲染结果 + 运动矢量     │
    │              │     输出: 目标分辨率（1080p / 4K）      │
    │              │  6. CAMetalLayer present()              │
    │              └─────────────────────────────────────────┘
```

### Objective-C 桥接层设计

> **为什么用 Objective-C 而非 Swift**：Obj-C 具有 C 兼容 ABI，P/Invoke 可直接调用，无需处理 Swift name mangling 或打包 Swift runtime。桥接层代码量约 300-400 行，维护成本低。

```objc
// MetalFXBridge/MetalFXBridge.h — 导出给 P/Invoke 的 C 接口

// 渲染器生命周期
void* FCR_CreateRenderer(void* nsViewHandle, uint32_t outWidth, uint32_t outHeight);
void  FCR_DestroyRenderer(void* renderer);
void  FCR_Resize(void* renderer, uint32_t newWidth, uint32_t newHeight);

// 纹理上传
void  FCR_UploadCHRTexture(void* renderer, const uint8_t* chrData, uint32_t size);
void  FCR_UploadPalette(void* renderer, const uint32_t* palette, uint32_t count);

// 分层渲染
void  FCR_BeginFrame(void* renderer);
void  FCR_DrawBackground(void* renderer, const uint8_t* nametable,
                         int scrollX, int scrollY);
void  FCR_DrawSprites(void* renderer, const uint8_t* oam,
                      uint32_t spriteCount);
void  FCR_DrawMotionVectors(void* renderer, const float* mvX, const float* mvY,
                             uint32_t count);
void  FCR_EndFrame(void* renderer);  // 触发 MetalFX + present
```

```csharp
// FC-Revolution.Rendering/Metal/MetalBridgeInterop.cs
internal static class MetalBridge
{
    private const string Lib = "FCRMetalBridge";

    [DllImport(Lib)] public static extern IntPtr FCR_CreateRenderer(
        IntPtr nsViewHandle, uint outWidth, uint outHeight);
    [DllImport(Lib)] public static extern void FCR_DestroyRenderer(IntPtr renderer);
    [DllImport(Lib)] public static extern void FCR_Resize(
        IntPtr renderer, uint newWidth, uint newHeight);
    [DllImport(Lib)] public static extern void FCR_UploadCHRTexture(
        IntPtr renderer, IntPtr chrData, uint size);
    [DllImport(Lib)] public static extern void FCR_UploadPalette(
        IntPtr renderer, IntPtr palette, uint count);
    [DllImport(Lib)] public static extern void FCR_BeginFrame(IntPtr renderer);
    [DllImport(Lib)] public static extern void FCR_DrawBackground(
        IntPtr renderer, IntPtr nametable, int scrollX, int scrollY);
    [DllImport(Lib)] public static extern void FCR_DrawSprites(
        IntPtr renderer, IntPtr oam, uint spriteCount);
    [DllImport(Lib)] public static extern void FCR_DrawMotionVectors(
        IntPtr renderer, IntPtr mvX, IntPtr mvY, uint count);
    [DllImport(Lib)] public static extern void FCR_EndFrame(IntPtr renderer);
}
```

### MetalFX Temporal 配置

```objc
// MetalFXBridge/MetalFXBridge.m — MetalFX Temporal Scaler 初始化
- (BOOL)setupMetalFXWithInputWidth:(uint32_t)inW inputHeight:(uint32_t)inH
                       outputWidth:(uint32_t)outW outputHeight:(uint32_t)outH
{
    MTLFXTemporalScalerDescriptor *desc = [MTLFXTemporalScalerDescriptor new];
    desc.inputWidth   = inW;   // 分层渲染的内部分辨率（推荐 1280×960 或更高）
    desc.inputHeight  = inH;
    desc.outputWidth  = outW;  // 显示器分辨率（1920×1080 / 3840×2160）
    desc.outputHeight = outH;
    desc.colorTextureFormat  = MTLPixelFormatBGRA8Unorm;
    desc.depthTextureFormat  = MTLPixelFormatDepth32Float;
    desc.motionTextureFormat = MTLPixelFormatRG16Float;

    _temporalScaler = [desc newTemporalScalerWithDevice:_device];
    if (!_temporalScaler) {
        // MetalFX 不可用时（Intel Mac / macOS 12），回退到 Spatial
        return [self setupMetalFXSpatialWithInputWidth:inW inputHeight:inH
                                          outputWidth:outW outputHeight:outH];
    }
    return YES;
}
```

### 芯片支持与推荐配置

| 芯片 | MetalFX Spatial | MetalFX Temporal | 推荐内部渲染分辨率 | 输出分辨率 |
|------|:--------------:|:----------------:|:-----------------:|:---------:|
| M1 | ✅ | ✅ | 1280×960 | 1920×1080 |
| M1 Pro / Max | ✅ | ✅ | 1920×1440 | 3840×2160 |
| M1 Ultra | ✅ | ✅ | 2560×1920 | 3840×2160 |
| M2 | ✅ | ✅ | 1280×960 | 1920×1080 |
| M2 Pro / Max | ✅ | ✅ | 1920×1440 | 3840×2160 |
| M2 Ultra | ✅ | ✅ | 2560×1920 | 3840×2160 |
| M3 / M3 Pro | ✅ | ✅ | 1920×1440 | 3840×2160 |
| M3 Max / Ultra | ✅ | ✅ | 2560×1920 | 3840×2160 |
| M4 / M4 Pro | ✅ | ✅ | 2560×1920 | 3840×2160 |
| M4 Max | ✅ | ✅ | 2560×1920 | 4K / 6K |

> **内部分辨率说明**：分层渲染直接在内部分辨率下绘制，不再依赖 NES 原生 256×240 帧。
> MetalFX Temporal 再将内部分辨率升采样到显示器目标分辨率，同时利用时序信息做质量增强。

### 效果预期

| 芯片 | 内部分辨率 | 输出分辨率 | 帧率 | 画质评分 |
|------|-----------|-----------|:----:|:--------:|
| M1 | 1280×960 | 1080p | 60fps | ⭐⭐⭐⭐☆ |
| M1 Pro | 1920×1440 | 4K | 60fps | ⭐⭐⭐⭐⭐ |
| M2 Max | 1920×1440 | 4K | 60fps | ⭐⭐⭐⭐⭐ |
| M3 Pro | 1920×1440 | 4K | 60fps | ⭐⭐⭐⭐⭐ |
| M4 Pro | 2560×1920 | 4K | 60fps | ⭐⭐⭐⭐⭐ |

---

## Avalonia/.NET 集成

### 接入方式：NativeControlHost + CAMetalLayer

Avalonia 11 通过 `NativeControlHost` 嵌入原生视图，是侵入性最低的方案：

```csharp
// FC-Revolution.UI/Views/GameRenderView.axaml.cs
public partial class GameRenderView : UserControl
{
    private IntPtr _renderer = IntPtr.Zero;

    public GameRenderView()
    {
        InitializeComponent();
        var host = new NativeControlHost();
        host.HandleCreated += OnHandleCreated;
        host.HandleDestroyed += OnHandleDestroyed;
        Content = host;
    }

    private void OnHandleCreated(object? sender, PlatformHandleEventArgs e)
    {
        if (OperatingSystem.IsMacOS())
        {
            var bounds = ((NativeControlHost)sender!).Bounds;
            _renderer = MetalBridge.FCR_CreateRenderer(
                e.Handle,
                (uint)bounds.Width,
                (uint)bounds.Height);
        }
        // 其他平台：VulkanRenderer.CreateSurface(e.Handle) — 预留
    }

    private void OnHandleDestroyed(object? sender, EventArgs e)
    {
        if (_renderer != IntPtr.Zero)
        {
            MetalBridge.FCR_DestroyRenderer(_renderer);
            _renderer = IntPtr.Zero;
        }
    }
}
```

### 帧渲染调用时序

```csharp
// 在 MainWindowViewModel 的计时器回调中（已有 async 防重入机制）
private async void OnTimerTick(object? sender, EventArgs e)
{
    if (_emuRunning || _renderer == IntPtr.Zero) return;
    _emuRunning = true;
    try
    {
        // 1. 仿真一帧，获取 PPU 元数据
        var metadata = await Task.Run(() =>
        {
            _nes.RunFrame();
            return _extractor.Extract(_nes.Ppu, _prevMetadata);
        });

        // 2. 上传 CHR / 调色板（仅变化时）
        if (metadata.ChrDirty)
            UploadCHR(metadata);
        if (metadata.PaletteDirty)
            UploadPalette(metadata);

        // 3. 驱动 GPU 分层渲染
        MetalBridge.FCR_BeginFrame(_renderer);
        unsafe
        {
            fixed (byte* nt = metadata.Nametable)
                MetalBridge.FCR_DrawBackground(_renderer, (IntPtr)nt,
                    metadata.ScrollX, metadata.ScrollY);
            fixed (byte* oam = metadata.OamRaw)
                MetalBridge.FCR_DrawSprites(_renderer, (IntPtr)oam, 64);
            fixed (float* mvx = metadata.MotionX, mvy = metadata.MotionY)
                MetalBridge.FCR_DrawMotionVectors(_renderer, (IntPtr)mvx,
                    (IntPtr)mvy, 64);
        }
        MetalBridge.FCR_EndFrame(_renderer);  // MetalFX + present

        _prevMetadata = metadata;
    }
    finally { _emuRunning = false; }
}
```

---

## 项目目录结构（跨平台预留）

```
FC-Revolution.Rendering/
│
├── Abstractions/                     ← 平台无关接口（已实现）
│   ├── ILayeredRenderer.cs
│   ├── IRenderDataExtractor.cs
│   ├── IFrameMetadata.cs
│   ├── IUpscaler.cs
│   ├── IPlatformCapabilities.cs      ← Capability 系统
│   ├── RenderFeatures.cs             ← UpscaleMode / FrameGenMode
│   ├── RenderPolicy.cs               ← userSetting + caps → RenderFeatures
│   ├── UserRenderSettings.cs         ← 用户配置（对应 UX 预设）
│   ├── SpriteEntry.cs
│   ├── VisibleTile.cs                ← scroll 解析后的可见图块
│   └── RenderConfig.cs
│
├── Common/                           ← 通用工具（已实现）
│   ├── MotionVectorGenerator.cs      ← 精灵 + 背景 scroll 运动矢量
│   ├── PaletteConverter.cs
│   ├── ChrTextureBuilder.cs
│   ├── VisibleTileResolver.cs        ← scroll/nametable/mirroring 解析（坑 1）
│   └── GpuInfo.cs
│
├── Metal/                            ← ✅ 当前实现目标（macOS）
│   ├── MetalLayeredRenderer.cs       ← ILayeredRenderer 的 Metal 实现
│   ├── MetalBridgeInterop.cs         ← P/Invoke 声明
│   ├── MetalFXUpscaler.cs            ← MetalFX Temporal/Spatial
│   └── Native/
│       ├── MetalFXBridge.h
│       ├── MetalFXBridge.m           ← Obj-C 桥接层（编译为 .dylib）
│       ├── TilemapRenderer.metal     ← 背景 Tilemap Shader
│       ├── SpriteRenderer.metal      ← 精灵 Instanced Shader
│       └── PostProcess.metal         ← 后处理 Shader（锐化等）
│
├── Vulkan/                           ← 🔲 预留（Windows / Linux / Android）
│   ├── VulkanLayeredRenderer.cs      ← TODO
│   ├── Upscalers/
│   │   ├── FsrUpscaler.cs            ← TODO（FSR 4 / 3.1）
│   │   ├── DlssUpscaler.cs           ← TODO（DLSS 4）
│   │   └── XeSSUpscaler.cs           ← TODO（XeSS 2）
│   └── Shaders/
│       └── (预留)
│
├── OpenGL/                           ← 🔲 预留（兜底方案）
│   ├── OpenGLLayeredRenderer.cs      ← TODO
│   └── Shaders/
│       └── (预留)
│
├── Software/                         ← ✅ Debug 阶段必须实现
│   ├── ReferenceRenderer.cs          ← PPU 原始帧输出，作为 diff 基准
│   └── DiffRenderer.cs               ← GPU 渲染 vs 参考渲染的像素 diff
│
└── Platform/                         ← 平台工厂（已定义接口）
    ├── RendererFactory.cs            ← 运行时选择最优渲染器
    └── UpscalerFactory.cs            ← 运行时选择最优超分辨率
```

---

## 其他平台预留方案

> 以下各平台方案已设计，**当前阶段不实现**，接口已在 `Abstractions/` 中预留。

### Windows

| GPU 厂商 | 渲染 API | 超分辨率 | 帧生成 |
|---------|---------|---------|--------|
| NVIDIA RTX 50+ | Vulkan 1.3 | DLSS 4 Multi Frame Gen | ✅ |
| NVIDIA RTX 40 | Vulkan 1.3 | DLSS 4 Frame Generation | ✅ |
| NVIDIA RTX 20/30 | Vulkan 1.3 | DLSS 3 | ❌ |
| AMD RDNA3+ | Vulkan 1.3 | FSR 4 + Frame Generation | ✅ |
| AMD RDNA1/2 | Vulkan 1.3 | FSR 3.1 | ❌ |
| Intel Arc | Vulkan 1.3 | XeSS 2 | ❌ |
| Intel 核显 | Vulkan 1.3 | FSR 3.1（兜底） | ❌ |

### Linux

| GPU 厂商 | 推荐驱动 | 超分辨率 |
|---------|---------|---------|
| NVIDIA | 专有驱动 565+ | DLSS 4 |
| AMD | Mesa 24+ (RADV) | FSR 4 |
| Intel | Mesa 24+ (ANV) | FSR 3.1 |

### Android / iOS

| 平台 | API | 超分辨率 | 策略 |
|------|-----|---------|------|
| Android | Vulkan 1.1+ | FSR 1.0 (低开销) | 功耗优先，动态降级 |
| iOS / iPadOS | Metal | MetalFX Spatial | 复用 macOS 桥接层 |

---

## 实现路径

### Phase 1：渲染抽象层 + PPU 元数据提取（2-3 周）

- [ ] 定义并实现 `IFrameMetadata`、`IRenderDataExtractor`、`ILayeredRenderer` 接口
- [ ] 实现 `RenderDataExtractor`：从 PPU 状态机提取 OAM / Nametable / CHR / 调色板
- [ ] 实现 `MotionVectorGenerator`：帧间精灵坐标差分
- [ ] 实现 `ChrTextureBuilder`：将 CHR ROM/RAM 转为 GPU 纹理图集格式
- [ ] 单元测试：验证各 ROM 下的元数据提取正确性

### Phase 2：macOS Metal 基础渲染（3-4 周）

- [ ] 实现 Obj-C 桥接层 `FCRMetalBridge.dylib`（CAMetalLayer 附加、命令缓冲区管理）
- [ ] 实现 `NativeControlHost` + `CAMetalLayer` Avalonia 集成
- [ ] 实现 `TilemapRenderer.metal`：背景图块 Shader（Nametable → Quad 实例化）
- [ ] 实现 `SpriteRenderer.metal`：精灵 Shader（OAM → DrawInstanced）
- [ ] 实现 `ChrTextureBuilder` 对接 Metal `MTLTexture`
- [ ] 验证：各 Mapper ROM 渲染画面正确性

### Phase 3：MetalFX 超分辨率接入（2-3 周）

- [ ] 集成 MetalFX Temporal Scaler（含运动矢量输入）
- [ ] 集成 MetalFX Spatial Scaler（M1 基础款备选路径，Temporal 不可用时自动回退）
- [ ] 实现 `MetalFXUpscaler.cs`（C# 侧封装）
- [ ] 调优内部分辨率（各 M 系列芯片分档配置）
- [ ] 实现 `PostProcess.metal`：可选轻微锐化 Pass

### Phase 4：后处理扩展（2-3 周，可选）

- [ ] 色调映射 / 饱和度调整（让 NES 调色板更接近 NTSC 原始色彩）
- [ ] 可选法线光照 Pass（算法推断法线贴图，给精灵加伪 3D 光照感）
- [ ] 用户配置界面（Avalonia UI，切换画质预设）
- [ ] 性能 Profile，各芯片帧时间优化

### Phase 5：其他平台（后续阶段）

- [ ] Windows Vulkan 渲染路径（复用 `ILayeredRenderer` 接口）
- [ ] FSR 4 / DLSS 4 / XeSS 2 集成
- [ ] Linux 验证
- [ ] iOS MetalFX Spatial（复用 macOS 桥接层）
- [ ] Android FSR 1.0

**macOS 阶段预估工时：9-13 周**
**全平台完成预估工时：20-27 周**

---

## 效果预期

### macOS（当前阶段目标）

| 芯片 | 内部渲染分辨率 | MetalFX 输出 | 帧率 | 画质 |
|------|:------------:|:-----------:|:----:|:----:|
| M1 | 1280×960 | 1920×1080 | 60fps | ⭐⭐⭐⭐☆ |
| M1 Pro / Max | 1920×1440 | 3840×2160 | 60fps | ⭐⭐⭐⭐⭐ |
| M2 / M2 Pro | 1920×1440 | 3840×2160 | 60fps | ⭐⭐⭐⭐⭐ |
| M3 / M3 Pro | 1920×1440 | 3840×2160 | 60fps | ⭐⭐⭐⭐⭐ |
| M4 / M4 Pro | 2560×1920 | 3840×2160 | 60fps | ⭐⭐⭐⭐⭐ |
| M4 Max | 2560×1920 | 4K / 6K | 60fps | ⭐⭐⭐⭐⭐ |

### 其他平台（预留，后续阶段）

| 平台 | GPU | 超分方案 | 输出 | 帧率 | 画质 |
|------|-----|---------|------|:----:|:----:|
| Windows | RTX 4090 | DLSS 4 FG | 4K | 60→240fps | ⭐⭐⭐⭐⭐ |
| Windows | RX 7800 XT | FSR 4 FG | 4K | 60→120fps | ⭐⭐⭐⭐⭐ |
| Windows | Arc A770 | XeSS 2 | 4K | 60fps | ⭐⭐⭐⭐☆ |
| Linux | RTX 3070 | DLSS 4 | 4K | 60fps | ⭐⭐⭐⭐☆ |
| Linux | RX 6800 | FSR 4 | 4K | 60fps | ⭐⭐⭐⭐☆ |
| iOS | A17 Pro | MetalFX Spatial | 原生 | 60fps | ⭐⭐⭐⭐☆ |

---

## 能力系统与配置策略（Capability + Policy）

> **重要**：不要用简单的 `bool` 开关控制超分辨率和帧生成。组合爆炸会在几周后让代码不可维护。

### 错误的做法

```csharp
bool EnableSuperResolution;  // ❌ 3 周后爆炸
bool EnableFrameGeneration;
```

### 正确做法：Capability + Policy 分层

```csharp
// FC-Revolution.Rendering/Abstractions/RenderFeatures.cs

public enum UpscaleMode
{
    None,
    Spatial,    // MetalFX Spatial / FSR 1.0
    Temporal    // MetalFX Temporal / DLSS / FSR 4
}

public enum FrameGenMode
{
    None,
    PlatformNative,  // DLSS FG / FSR FG（平台原生支持）
    Experimental     // 软件插帧（未来扩展）
}

public sealed class RenderFeatures
{
    public UpscaleMode Upscale         { get; init; }
    public FrameGenMode FrameGen       { get; init; }
    public bool EnablePostProcess      { get; init; }
    public int InternalResolutionWidth { get; init; }
    public int InternalResolutionHeight{ get; init; }
}
```

```csharp
// FC-Revolution.Rendering/Abstractions/IPlatformCapabilities.cs

public interface IPlatformCapabilities
{
    bool SupportsMetalFXTemporal  { get; }
    bool SupportsMetalFXSpatial   { get; }
    bool SupportsDLSS4            { get; }
    bool SupportsFSR4             { get; }
    bool SupportsXeSS2            { get; }
    bool SupportsFrameGeneration  { get; }
    int  MaxTextureSize           { get; }
    int  RecommendedInternalWidth { get; }
    int  RecommendedInternalHeight{ get; }
}
```

```csharp
// FC-Revolution.Rendering/Platform/RenderPolicy.cs
// 运行时将用户设置 + 平台能力解析为最终配置

public static class RenderPolicy
{
    public static RenderFeatures Resolve(
        UserRenderSettings userSettings,
        IPlatformCapabilities caps)
    {
        var upscale = userSettings.PreferredUpscale switch
        {
            UpscalePreference.Temporal when caps.SupportsMetalFXTemporal
                => UpscaleMode.Temporal,
            UpscalePreference.Temporal when caps.SupportsMetalFXSpatial
                => UpscaleMode.Spatial,   // 自动降级
            UpscalePreference.Spatial  when caps.SupportsMetalFXSpatial
                => UpscaleMode.Spatial,
            _   => UpscaleMode.None
        };

        var frameGen = userSettings.EnableFrameGen && caps.SupportsFrameGeneration
            ? FrameGenMode.PlatformNative
            : FrameGenMode.None;

        return new RenderFeatures
        {
            Upscale              = upscale,
            FrameGen             = frameGen,
            EnablePostProcess    = userSettings.EnablePostProcess,
            InternalResolutionWidth  = caps.RecommendedInternalWidth,
            InternalResolutionHeight = caps.RecommendedInternalHeight
        };
    }
}
```

**这个设计的意义**：未来接入 MetalFX / DLSS / FSR / XeSS，**无需修改 UI 层或核心逻辑**，只需实现新的 `IPlatformCapabilities`。

---

## 用户体验模型（UX）

> 用户不需要理解 MetalFX / DLSS / FSR，他们只需要"更清晰"或"更丝滑"。

### 推荐 UI 结构

```
画质模式（快速选择）：
  [ 原始像素 ]  [ 平衡 ]  [ 高质量 ]  [ 实验性 ]

高级设置（展开）：
  超分辨率：  [ 关闭 ]  [ 空间（低开销）]  [ 时序（高质量）]
  帧生成：    [ 关闭 ]  [ 平台支持 ]  [ 实验 ]
  内部分辨率：[ 自动 ]  [ 720p ]  [ 1080p ]  [ 1440p ]
  后处理：    [ 锐化 ]  [ 色彩增强 ]  [ 光照（可选）]
```

### 画质预设映射

| 预设 | UpscaleMode | FrameGenMode | 内部分辨率 | 后处理 |
|------|------------|--------------|-----------|--------|
| 原始像素 | None | None | Auto (低) | 关闭 |
| 平衡 | Spatial | None | Auto | 锐化 |
| 高质量 | Temporal | None | Auto (高) | 锐化+色彩 |
| 实验性 | Temporal | PlatformNative | Auto (最高) | 全开 |

---

## NES 渲染关键陷阱（必读）

> 以下是实现 GPU 分层渲染时最容易踩的坑，务必在对应 Phase 开始前仔细阅读。

### ⚠️ 坑 1：NES Scroll 不是简单平移

**错误假设**：`DrawBackground(scrollX, scrollY)` 直接平移 GPU 坐标。

**实际情况**：NES scroll 有三层：
- `fine scroll`：像素级偏移（0-7 像素）
- `coarse scroll`：图块级偏移（tile 坐标）
- `nametable wrapping` + `mirroring`（horizontal / vertical / single / four-screen）

直接传 scrollX/scrollY 做 GPU 平移在马里奥边界一定会出错。

**正确做法**：在 **CPU 侧（C# 层）** 完成可见 Tile 解析，输出"当前帧实际可见的图块列表 + 每个图块的精确屏幕坐标"，GPU 只负责按坐标绘制，不处理 scroll 逻辑。

```csharp
// IRenderDataExtractor 在提取时就完成 scroll 解析
// Nametable 输出的不是原始 32×30 数组，
// 而是：当前可见区域的图块列表（含精确屏幕坐标）
public sealed class VisibleTile
{
    public int ScreenX  { get; init; }  // 屏幕像素坐标（已含 scroll 偏移）
    public int ScreenY  { get; init; }
    public byte TileId  { get; init; }
    public byte PaletteId { get; init; }
}
```

### ⚠️ 坑 2：Sprite Priority（精灵优先级）

NES 精灵有两种优先级：前景（在背景之前）和背景（在背景之后）。影响：
- RTR 排序（正确的绘制顺序）
- 马里奥会"穿墙"或"被管道吞掉"

**GPU 渲染必须**：
1. Background Pass（先画背景）
2. Behind-BG Sprite Pass（优先级 = 背景后的精灵）
3. Foreground Sprite Pass（优先级 = 前景精灵）

或使用深度缓冲区模拟优先级（推荐）。

### ⚠️ 坑 3：Sprite 0 Hit（很多游戏依赖）

很多 NES 游戏利用 Sprite 0 Hit 实现分屏效果（如状态栏固定 + 画面滚动）。

**即使你做 GPU 重绘**：PPU 逻辑层仍然**必须保持 Sprite 0 Hit 行为**，否则画面会错位。当前 PPU 仿真已实现此行为，渲染层不要绕过它。

### ⚠️ 坑 4：Motion Vector 不只是精灵位移

**当前设计的运动矢量**：`delta = current.XY - previous.XY`（仅精灵）

**缺失的部分**：
- 背景 scroll 运动矢量（横向卷轴游戏的主要运动来源）
- 摄像机移动

MetalFX Temporal 如果没有背景运动矢量，**横向滚动游戏会出现严重拖影**（背景被算法认为是静止的）。

**修正**：`IRenderDataExtractor` 需要同时输出：
- 精灵运动矢量（64 个）
- 背景 scroll 运动矢量（一个整体矢量，覆盖整个背景图层）

### ⚠️ 坑 5：Frame Pacing（帧节奏，最隐蔽）

**当前架构**：`DispatcherTimer → RunFrame()`（固定 60Hz 逻辑帧）

**潜在问题**：Metal present 有 vsync，仿真是固定步长，如果不显式处理三者的时间关系：
- 抖动（jitter）
- 音画不同步
- 输入延迟增加

**建议明确三条时间线**：

```
Simulation 时间线：固定 60Hz，由计时器驱动，不受渲染影响
Render 时间线：    可变，跟随 vsync，Metal CADisplayLink 驱动
Audio 时间线：     时间主导，APU 输出按实际时间推进

三者通过 double-buffered FrameBuffer 解耦：
仿真线程写 → 交换 → 渲染线程读
```

---

## Debug Renderer（参考渲染器，必须实现）

> 没有参考渲染器，你将无法判断 GPU 渲染错误的来源。

### 为什么必须

当 GPU 分层渲染出现画面错误时，你需要判断错误来自：
- PPU 元数据提取？（C# 层）
- GPU 背景 Shader？（Metal Shader）
- GPU 精灵 Shader？
- MetalFX Temporal 拖影？

没有参考渲染器，这个问题**无法系统性调试**。

### 实现方式

```csharp
// FC-Revolution.Rendering/Software/ReferenceRenderer.cs
// 直接使用 PPU 仿真输出的 256×240 帧缓冲（已有，无需修改）
// 作为"正确答案"与 GPU 渲染结果做 diff

public sealed class ReferenceRenderer : ILayeredRenderer
{
    public void Render(IFrameMetadata metadata, RenderFeatures features)
    {
        // 直接输出 PPU 原始像素帧（256×240 uint[]）
        // 通过 WriteableBitmap 在 Avalonia 中显示
        // 用于与 MetalLayeredRenderer 输出做对比
    }
}
```

### Debug 对比工具

在开发阶段的 Debug 构建中，提供并排对比视图：

```
┌──────────────────┬──────────────────┐
│  参考渲染（PPU）  │   GPU 分层渲染   │
│   256×240         │   1920×1080      │
│（已知正确）        │（待验证）         │
└──────────────────┴──────────────────┘
         ↓ 像素级 diff
   差异高亮显示（红色标注）
```

---

## 附录

### A. 依赖库

| 功能 | macOS (当前) | Windows/Linux (预留) |
|------|-------------|---------------------|
| 渲染 API | Metal（系统内置） | Vulkan 1.3 SDK |
| 超分辨率 | MetalFX.framework（系统内置） | FidelityFX SDK (FSR) / NGX SDK (DLSS) / XeSS SDK |
| 桥接层 | FCRMetalBridge.dylib（自研） | — |
| Avalonia 集成 | NativeControlHost | NativeControlHost |

### B. 参考资料

- [Apple MetalFX Documentation](https://developer.apple.com/documentation/metalfx)
- [Metal Shading Language Specification](https://developer.apple.com/metal/Metal-Shading-Language-Specification.pdf)
- [AMD FidelityFX FSR 4](https://gpuopen.com/fidelityfx-super-resolution-4/)
- [NVIDIA DLSS 4 SDK](https://developer.nvidia.com/rtx/dlss)
- [Intel XeSS 2 SDK](https://github.com/intel/xess)
- [Avalonia NativeControlHost](https://docs.avaloniaui.net/docs/concepts/native-controls)

### C. 更新日志

| 版本 | 日期 | 变更 |
|------|------|------|
| 1.0 | 2026-03-31 | 初始版本（跨平台超分辨率后处理方案） |
| 2.0 | 2026-03-31 | 完全重写：以 macOS M 系芯片为核心实现目标；引入 PPU 分层渲染 + GPU 矢量化重绘架构（非像素帧放大）；补充 Obj-C 桥接层完整接口设计；跨平台目录结构预留；调整实现路径优先级 |
| 2.1 | 2026-03-31 | 新增 Capability+Policy 能力系统；新增用户体验 UX 模型；新增 5 个 NES 渲染关键陷阱（Scroll 解析、Sprite Priority、Sprite 0 Hit、Motion Vector 完整性、Frame Pacing）；新增 Debug Renderer / 参考渲染器方案；更新目录结构 |

---

*文档结束*
