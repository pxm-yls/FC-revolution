# FC-Revolution macOS 开发流程指南

> **版本**: 1.6 | **更新**: 2026-03-31
> **适用范围**: macOS Apple Silicon (M1+)，C# / .NET 10 + Avalonia 11
> **本文档目标**: 从零到完整 macOS Metal 渲染管线的具体开发步骤，包含踩坑预防和调试策略

---

## 目录

1. [开发前提与环境准备](#开发前提与环境准备)
2. [正确的开发节奏（三步走）](#正确的开发节奏三步走)
3. [Step 1：无超分 GPU 分层渲染](#step-1无超分-gpu-分层渲染)
4. [Step 2：MetalFX Spatial 接入](#step-2metalfx-spatial-接入)
5. [Step 3：MetalFX Temporal 接入](#step-3metalfx-temporal-接入)
6. [关键陷阱详解与解决方案](#关键陷阱详解与解决方案)
7. [Debug Renderer 使用流程](#debug-renderer-使用流程)
8. [Frame Pacing 架构](#frame-pacing-架构)
9. [Capability + Policy 运行时](#capability--policy-运行时)
10. [验收标准（各阶段 Definition of Done）](#验收标准各阶段-definition-of-done)

---

## 当前开发进度

> **最近更新**: 2026-03-31

**当前完成度**: 7 / 9 个开发任务

- [x] 开发前提检查：Apple Silicon / .NET 10 / Xcode Command Line Tools / macOS SDK / MetalFX Framework / `osx-arm64` 构建能力确认
- [x] Phase 1.1：创建 `FC-Revolution.Rendering` 与 `FC-Revolution.Rendering.Tests` 工程骨架
- [x] Phase 1.1：实现 `VisibleTileResolver`、`MotionVectorGenerator`、`FrameDoubleBuffer` 基础设施
- [x] Phase 1.1：补齐 scroll / mirroring / attribute / motion vector / double buffer 单元测试（15 / 15 通过）
- [x] Phase 1.2：实现 `PpuRenderStateSnapshot`、`IFrameMetadata` 扩展与 `IRenderDataExtractor`（渲染测试 17 / 17 通过，Core 测试保持 106 / 106 通过）
- [x] Phase 2.1：接入 macOS `NativeControlHost + CAMetalLayer` 最小原生 presenter，`GameWindow` 主视口已可走 `libFCRMetalBridge.dylib`，并保留软件 `Image` 回退；`FC-Revolution.UI` 默认构建与 `-r osx-arm64` 构建通过
- [x] Phase 2.2：基于 `IFrameMetadata` 的背景/精灵 GPU 分层渲染与 `ReferenceRenderer diff` 验证；新增离屏 Metal 输出读回与 GPU/Reference 自动化 diff 回归（`FC-Revolution.Rendering.Tests` 30 / 30 通过）
- [ ] Phase 3：接入 MetalFX Spatial
- [ ] Phase 4：接入 MetalFX Temporal

> **当前阶段说明**：Phase 2.2 已完成。`GameWindow` 主视口在 macOS 下已优先走基于 `IFrameMetadata` / `LayeredFrameData` 的背景与精灵 GPU 分层渲染；并新增离屏 Metal 输出读回，使用 `ReferenceRenderer` 做像素级自动化 diff 回归。下一步进入 Phase 3（MetalFX Spatial）。

> **后续执行清单**：Phase 3 / Phase 4 及其配套验证、运行 hygiene、收口标准，见 [MACOS_SPATIAL_TEMPORAL_EXECUTION_CHECKLIST.md](./MACOS_SPATIAL_TEMPORAL_EXECUTION_CHECKLIST.md)。

---

## 开发前提与环境准备

### 已具备的基础

当前项目已完成：
- ✅ CPU 6502 完整仿真
- ✅ PPU 2C02：背景/精灵渲染、Loopy scroll、NMI、Sprite0Hit
- ✅ APU、Mapper 0-4、StandardController
- ✅ Avalonia 11 UI 主框架（MainWindow、Timer、键盘输入）
- ✅ `NesConsole.RunFrame()` 指令级精确

### 需要新增的依赖

```xml
<!-- FC-Revolution.Rendering/FC-Revolution.Rendering.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <!-- macOS dylib 复制到输出目录 -->
    <RuntimeIdentifiers>osx-arm64;osx-x64</RuntimeIdentifiers>
  </PropertyGroup>

  <ItemGroup>
    <!-- Obj-C 桥接库，编译后放入 runtimes/osx-arm64/native/ -->
    <NativeLibrary Include="Native/FCRMetalBridge.dylib"
                   Condition="$([MSBuild]::IsOSPlatform('OSX'))" />
  </ItemGroup>
</Project>
```

### Xcode 命令行工具

```bash
# 确认已安装（用于编译 Obj-C 桥接层）
xcode-select --install
xcrun --show-sdk-path  # 应显示 macOS SDK 路径
```

### 本轮检查结果（2026-03-31）

- [x] 设备架构：`arm64`（Apple Silicon）
- [x] .NET SDK：`10.0.101`
- [x] Xcode Command Line Tools：`/Library/Developer/CommandLineTools`
- [x] macOS SDK：`/Library/Developer/CommandLineTools/SDKs/MacOSX.sdk`
- [x] 系统 Framework：`Metal.framework`、`MetalFX.framework`、`QuartzCore.framework` 存在
- [x] Obj-C 最小桥接烟测：可成功编译并链接 `Metal` / `MetalFX` / `QuartzCore` 动态库
- [x] 当前 `FC-Revolution.UI` 构建通过
- [x] 当前 `FC-Revolution.UI -r osx-arm64` 构建通过
- [x] 当前 `FC-Revolution.Core.Tests` 通过（106 通过 / 4 跳过）
- [x] 当前 `FC-Revolution.Backend.Hosting.Tests` 通过（8 通过）
- [x] 当前 `FC-Revolution.Rendering.Tests` 通过（30 通过）
- [ ] 当前 `FC-Revolution.UI.Tests` 全绿
  - 现状：44 通过 / 5 失败，属于进入渲染改造前需要单独清理的既有基线问题

---

## 正确的开发节奏（三步走）

> **核心原则**：先建立稳定的基础，逐步叠加复杂度。一旦你加上 MetalFX Temporal，调试成本会翻倍，所以必须分阶段。

```
Step 1: 无超分的 GPU 分层渲染
    ↓ 验证：PPU 元数据 → GPU 管线 100% 正确
    ↓ 工具：ReferenceRenderer diff 对比

Step 2: MetalFX Spatial Scaler
    ↓ 验证：无历史帧依赖，调试简单
    ↓ 只加 upscaler，不加 motion vector
    ↓ 验证：分辨率提升，无明显画质损失

Step 3: MetalFX Temporal Scaler
    ↓ 最后才引入历史帧 + motion vector
    ↓ 验证：无拖影，横向滚动游戏正确
```

**为什么必须这个顺序**：
- Step 1 错误 → 一定是 PPU 提取或 GPU Shader 问题
- Step 2 错误 → 一定是 upscaler 管线问题（Spatial 无历史帧，排查简单）
- Step 3 错误 → 可能是 motion vector 不完整、历史帧管理问题

---

## Step 1：无超分 GPU 分层渲染

### 目标

证明 `PPU → Metadata → GPU 管线` 是正确的。此阶段**不做 MetalFX**，输出尺寸为内部分辨率（如 1280×960）。

### 1.1 PPU 元数据提取

先实现 `VisibleTileResolver`，处理 NES scroll 的三层逻辑：

```csharp
// FC-Revolution.Rendering/Common/VisibleTileResolver.cs
public sealed class VisibleTileResolver
{
    /// <summary>
    /// 将 PPU Nametable + scroll 寄存器解析为当前帧实际可见的图块列表。
    /// GPU 不处理任何 scroll 逻辑，只按 ScreenX/ScreenY 绘制。
    /// </summary>
    public static List<VisibleTile> Resolve(
        ReadOnlySpan<byte> nametable,   // 2KB，两个 nametable
        ReadOnlySpan<byte> patternTable,
        int fineScrollX,                // $2005 X fine scroll (0-7)
        int fineScrollY,                // $2005 Y fine scroll (0-7)
        int coarseScrollX,              // Loopy V bits 4:0 (0-31)
        int coarseScrollY,              // Loopy V bits 9:5 (0-29)
        int nametableSelect,            // Loopy V bits 11:10 (0-3)
        MirrorMode mirrorMode,
        int screenWidth,
        int screenHeight)
    {
        var tiles = new List<VisibleTile>(40 * 32); // 稍大于可视区域
        // 计算可见区域起始图块（含跨 nametable 边界处理）
        // ...（实现 Loopy scroll 解析）
        return tiles;
    }
}
```

> **重要**：`fineScrollX/Y` 和 `coarseScrollX/Y` 必须从 PPU 的 Loopy 寄存器读取，不是简单的 `scrollX/scrollY`。参考当前 PPU 实现中的 `_v`（current VRAM address）和 `_t`（temporary VRAM address）寄存器。

### 1.2 Obj-C 桥接层（Step 1 最小版本）

```objc
// FC-Revolution.Rendering/Metal/Native/FCRRenderer.m

@interface FCRRenderer : NSObject
@property (nonatomic, strong) id<MTLDevice> device;
@property (nonatomic, strong) id<MTLCommandQueue> commandQueue;
@property (nonatomic, strong) CAMetalLayer* metalLayer;

// Render targets（内部分辨率）
@property (nonatomic, strong) id<MTLTexture> colorTarget;
@property (nonatomic, strong) id<MTLTexture> depthTarget;

// Pipeline states
@property (nonatomic, strong) id<MTLRenderPipelineState> tilemapPipeline;
@property (nonatomic, strong) id<MTLRenderPipelineState> spritePipeline;

// CHR 纹理图集（512 个 8×8 tiles → 128×64 像素纹理图集）
@property (nonatomic, strong) id<MTLTexture> chrAtlas;
@end

// === 导出 C 接口 ===

void* FCR_CreateRenderer(void* nsViewPtr, uint32_t outW, uint32_t outH)
{
    FCRRenderer* r = [[FCRRenderer alloc] initWithNSView:nsViewPtr
                                             outputWidth:outW
                                            outputHeight:outH];
    return (__bridge_retained void*)r;
}

void FCR_BeginFrame(void* ctx)
{
    FCRRenderer* r = (__bridge FCRRenderer*)ctx;
    [r beginFrame];
}

void FCR_DrawBackground(void* ctx, const uint8_t* visibleTiles,
                         uint32_t tileCount)
{
    // visibleTiles: 结构化数组，每个 tile = { screenX(int), screenY(int),
    //               tileId(u8), paletteId(u8) }
    FCRRenderer* r = (__bridge FCRRenderer*)ctx;
    [r drawBackgroundTiles:visibleTiles count:tileCount];
}

void FCR_DrawSprites(void* ctx, const uint8_t* oam, uint32_t count)
{
    FCRRenderer* r = (__bridge FCRRenderer*)ctx;
    [r drawSprites:oam count:count];
}

void FCR_EndFrame(void* ctx)
{
    FCRRenderer* r = (__bridge FCRRenderer*)ctx;
    [r endFrameWithUpscale:NO];  // Step 1 不做超分
}
```

### 1.3 Metal Shader：Tilemap

```metal
// TilemapRenderer.metal

struct TileInstance {
    float2 screenPos;   // 屏幕像素坐标（左上角）
    uint   tileId;      // 0-511
    uint   paletteId;   // 0-7
    uint   flipH;
    uint   flipV;
};

struct VertexOut {
    float4 position [[position]];
    float2 uv;
    uint   paletteId;
};

vertex VertexOut tilemap_vertex(
    uint instanceId [[instance_id]],
    uint vertexId   [[vertex_id]],
    constant TileInstance* instances [[buffer(0)]],
    constant float2& viewportSize    [[buffer(1)]])
{
    TileInstance inst = instances[instanceId];
    // 计算 quad 的 4 个顶点（vertexId 0-3）
    float2 offsets[4] = {
        {0, 0}, {8, 0}, {0, 8}, {8, 8}  // 基础 8×8 tile，GPU 会根据缩放比例处理
    };
    // 将屏幕坐标转为 NDC（-1 到 1）
    float2 pos = (inst.screenPos + offsets[vertexId]) / viewportSize * 2.0 - 1.0;
    pos.y = -pos.y;  // Metal Y 轴朝上

    VertexOut out;
    out.position = float4(pos, 0, 1);
    out.uv = ComputeCHRAtlasUV(inst.tileId, vertexId, inst.flipH, inst.flipV);
    out.paletteId = inst.paletteId;
    return out;
}

fragment float4 tilemap_fragment(
    VertexOut in [[stage_in]],
    texture2d<uint> chrAtlas    [[texture(0)]],
    constant uint* paletteData  [[buffer(0)]])
{
    constexpr sampler s(filter::nearest);  // 必须 nearest，保持像素锐利
    uint colorIndex = chrAtlas.sample(s, in.uv).r;
    if (colorIndex == 0) discard_fragment();  // 透明色
    uint argb = paletteData[in.paletteId * 4 + colorIndex];
    return float4(
        float((argb >> 16) & 0xFF) / 255.0,
        float((argb >>  8) & 0xFF) / 255.0,
        float( argb        & 0xFF) / 255.0,
        1.0
    );
}
```

### 1.4 精灵渲染（含 Priority 处理）

```metal
// SpriteRenderer.metal

// 精灵优先级通过深度值模拟：
// 前景精灵 depth = 0.3
// 背景精灵 depth = 0.7（会被背景图块遮挡）
// 背景图块 depth = 0.5

struct SpriteInstance {
    float2 screenPos;
    uint   tileId;
    uint   paletteId;
    uint   flipH;
    uint   flipV;
    uint   behindBG;   // 0=前景，1=背景后
    uint   spriteSize; // 0=8x8，1=8x16
};

vertex VertexOut sprite_vertex(
    uint instanceId [[instance_id]],
    uint vertexId   [[vertex_id]],
    constant SpriteInstance* instances [[buffer(0)]],
    constant float2& viewportSize      [[buffer(1)]])
{
    SpriteInstance inst = instances[instanceId];
    float depth = inst.behindBG ? 0.7 : 0.3;
    // ...（同 tilemap_vertex，但使用 depth 值）
}
```

### 1.5 Step 1 验收标准

在进入 Step 2 之前，以下测试必须全部通过：

- [ ] 超级马里奥：画面无错位，精灵无穿墙，管道遮挡正确
- [ ] 忍者神龟：多精灵游戏，精灵优先级正确
- [ ] 冒险岛：横向卷轴，scroll 边界无撕裂
- [ ] ReferenceRenderer diff：所有测试 ROM 的像素差异 < 1%（允许亚像素误差）
- [ ] 帧率稳定 60fps（通过 Metal GPU Capture 验证）

---

## Step 2：MetalFX Spatial 接入

### 目标

验证超分辨率管线是通的。Spatial 无历史帧、无 motion vector，排查最简单。

### 为什么先 Spatial 不先 Temporal

| 比较项 | Spatial | Temporal |
|--------|---------|---------|
| 历史帧依赖 | ❌ 无 | ✅ 需要 |
| Motion vector | ❌ 不需要 | ✅ 必须 |
| 调试难度 | 低 | 高 |
| 回退条件 | M1+ / macOS 13+ | M1+ / macOS 13+ |
| 画质 | 良好 | 更好 |

### 2.1 修改 Obj-C 桥接层

```objc
// 在 FCRRenderer 中添加 Spatial Scaler
- (BOOL)setupSpatialScalerOutputWidth:(uint32_t)outW outputHeight:(uint32_t)outH
{
    MTLFXSpatialScalerDescriptor *desc = [MTLFXSpatialScalerDescriptor new];
    desc.inputWidth   = _internalWidth;   // Step 1 的内部分辨率
    desc.inputHeight  = _internalHeight;
    desc.outputWidth  = outW;             // 显示器分辨率
    desc.outputHeight = outH;
    desc.colorTextureFormat = MTLPixelFormatBGRA8Unorm;

    _spatialScaler = [desc newSpatialScalerWithDevice:_device];
    return _spatialScaler != nil;
}

void FCR_EndFrame(void* ctx)
{
    FCRRenderer* r = (__bridge FCRRenderer*)ctx;
    [r endFrameWithUpscale:YES];  // Step 2 开启 Spatial
}
```

### 2.2 渲染流程修改

```
Step 2 渲染流程：

GPU 分层渲染 → colorTarget（内部分辨率，如 1280×960）
    ↓
MetalFX Spatial Scaler
    输入: colorTarget
    输出: outputTexture（显示器分辨率，如 1920×1080）
    ↓
CAMetalLayer.nextDrawable → present
```

### 2.3 Step 2 验收标准

- [ ] 画面清晰度明显优于 Step 1（像素边缘更锐利）
- [ ] 无明显画质损失（对比 ReferenceRenderer）
- [ ] M1 基础款性能达标（< 16.7ms/帧，即 60fps 以上）
- [ ] 切换 Spatial/无超分 的渲染路径无崩溃

---

## Step 3：MetalFX Temporal 接入

### 目标

引入历史帧和 motion vector，实现最高画质输出。**必须在 Step 2 稳定后才开始**。

### 3.1 Motion Vector 完整实现

> ⚠️ 仅有精灵运动矢量是不够的，背景 scroll 运动矢量同等重要。

```csharp
// FC-Revolution.Rendering/Common/MotionVectorGenerator.cs

public sealed class MotionVectorGenerator
{
    /// <summary>
    /// 生成完整运动矢量：精灵位移 + 背景 scroll 位移。
    /// 注意：输出格式为 Metal RG16Float，值范围 [-screenWidth, +screenWidth]
    /// </summary>
    public static MotionVectorData Generate(
        IFrameMetadata current,
        IFrameMetadata previous,
        float scaleX, float scaleY)
    {
        var spriteVectors = new Vector2[64];
        for (int i = 0; i < 64; i++)
        {
            float dx = (current.Sprites[i].X - previous.Sprites[i].X) * scaleX;
            float dy = (current.Sprites[i].Y - previous.Sprites[i].Y) * scaleY;
            spriteVectors[i] = new Vector2(dx, dy);
        }

        // 背景整体运动矢量（scroll 变化量）
        float bgMvX = (current.ScrollX - previous.ScrollX) * scaleX;
        float bgMvY = (current.ScrollY - previous.ScrollY) * scaleY;
        var bgVector = new Vector2(bgMvX, bgMvY);

        return new MotionVectorData(spriteVectors, bgVector);
    }
}
```

### 3.2 Motion Vector 纹理生成

MetalFX Temporal 需要一张覆盖整个帧的 `RG16Float` 运动矢量纹理：

```objc
// 在 Metal Shader 中生成运动矢量纹理
// 背景区域：填充背景 scroll 运动矢量
// 精灵区域：根据精灵 bounding box 填充对应精灵的运动矢量

kernel void generate_motion_vectors(
    texture2d<half, access::write> motionOut [[texture(0)]],
    constant float2& bgMotion [[buffer(0)]],
    constant SpriteInstance* sprites [[buffer(1)]],
    constant float2* spriteMotions [[buffer(2)]],
    uint2 gid [[thread_position_in_grid]])
{
    half2 mv = half2(bgMotion);  // 默认背景运动矢量

    // 检查是否在某个精灵的区域内
    for (int i = 0; i < 64; i++) {
        if (PointInSpriteBounds(gid, sprites[i])) {
            mv = half2(spriteMotions[i]);
            break;
        }
    }

    motionOut.write(mv, gid);
}
```

### 3.3 历史帧管理

```objc
// Temporal Scaler 需要历史帧，必须管理 history texture
// MetalFX 框架内部维护历史，但需要你设置 reset flag

// 以下情况必须 reset（告诉 Temporal 忽略历史帧）：
// 1. ROM 加载时
// 2. 存档读取时
// 3. 画面跳切（如过关动画结束、暂停菜单进出）

_temporalScaler.reset = YES;  // 下一帧重置历史
```

### 3.4 Step 3 验收标准

- [ ] 超级马里奥横向滚动：背景无拖影
- [ ] 忍者神龟：快速移动精灵无明显鬼影
- [ ] 与 Step 2 (Spatial) 画质对比：Temporal 明显更清晰
- [ ] 存档读取后：首帧无残影（reset 机制正确）
- [ ] M1 Pro 性能达标：< 16.7ms/帧，GPU 占用率合理

---

## 关键陷阱详解与解决方案

### 陷阱 1：NES Scroll 解析

**问题本质**：NES 的 scroll 由 `Loopy` 寄存器精确控制，不是简单的 X/Y 坐标。

```
PPU 内部 scroll 状态（当前实现）：
  _v (current VRAM address, 15 bits):
    bits 14-12: fine Y scroll (0-7)
    bits 11-10: nametable select (0-3)
    bits  9-5:  coarse Y (0-29)
    bits  4-0:  coarse X (0-31)

  _x (fine X scroll, 3 bits): 0-7
```

**正确提取方式**：

```csharp
// IRenderDataExtractor 实现中，从 PPU 提取 scroll 信息
public IFrameMetadata Extract(IPpu ppu, IFrameMetadata? prev)
{
    // 在 PPU 渲染完成后（FrameComplete 标志置位时）读取
    int fineX      = ppu.FineX;           // _x 寄存器，0-7
    int fineY      = (ppu.V >> 12) & 7;   // v bits 14:12
    int coarseX    = ppu.V & 0x1F;        // v bits 4:0
    int coarseY    = (ppu.V >> 5) & 0x1F; // v bits 9:5
    int ntSelect   = (ppu.V >> 10) & 3;   // v bits 11:10

    var tiles = VisibleTileResolver.Resolve(
        ppu.Nametable, ppu.PatternTable,
        fineX, fineY, coarseX, coarseY, ntSelect,
        ppu.MirrorMode,
        256, 240);
    // ...
}
```

### 陷阱 2：Sprite 0 Hit 与渲染层解耦

PPU 仿真中的 Sprite 0 Hit 检测**必须继续在仿真层运行**，不要试图在 GPU 渲染层"优化"掉它。渲染层只负责视觉输出。

### 陷阱 3：精灵优先级深度方案

推荐使用深度缓冲区而非多 Pass 排序：

```
深度值分配：
  背景图块（不透明区域）：depth = 0.5
  背景图块（透明色）：    discard（不写深度）
  背景后精灵：           depth = 0.7（被背景遮挡）
  前景精灵：             depth = 0.3（在背景前）

Metal Depth Test 设置：
  depthCompareFunction = .less
  isDepthWriteEnabled  = true
```

### 陷阱 4：Motion Vector 坐标系

MetalFX 的 motion vector 要求值为**当前帧到上一帧的位移**（注意方向），单位是像素，坐标系与渲染分辨率一致。

```objc
// 正确方向：mv = previousPos - currentPos（向过去运动）
// 错误方向：mv = currentPos - previousPos（会造成反向拖影）
```

### 陷阱 5：Frame Pacing 完整方案

详见下一节。

---

## Debug Renderer 使用流程

### 何时使用

- Phase 1 开发过程中：每个新功能完成后立即 diff
- 出现画面 bug 时：第一步永远是开启 diff 模式定位层级

### 使用步骤

```
1. 启动应用，加载 ROM
2. 快捷键（建议 Ctrl+D）切换到 Debug Split 视图：
   ┌──────────────────┬──────────────────┐
   │  Reference（PPU） │  GPU 分层渲染    │
   └──────────────────┴──────────────────┘

3. 如发现差异，按 Ctrl+Shift+D 开启 Pixel Diff 模式：
   - 相同像素：黑色
   - 差异像素：红色（差值越大越亮）

4. 根据 diff 区域判断问题层级：
   - 背景区域差异 → VisibleTileResolver 或 TilemapShader 问题
   - 精灵区域差异 → SpriteShader 或 Priority 问题
   - 全帧差异      → 调色板数据问题
```

### 自动化回归测试

```csharp
// FC-Revolution.Rendering.Tests/RenderRegressionTests.cs
[Theory]
[InlineData("SuperMario.nes", 300)]   // 第 300 帧
[InlineData("Ninja Turtles.nes", 120)]
public async Task GpuRenderer_MatchesReference_WithinTolerance(
    string rom, int frameIndex)
{
    var referenceFrame  = await RunToFrame_Reference(rom, frameIndex);
    var gpuFrame        = await RunToFrame_GPU(rom, frameIndex);

    float pixelDiff = PixelDiff.Compare(referenceFrame, gpuFrame);
    Assert.True(pixelDiff < 0.01f,
        $"GPU vs Reference diff {pixelDiff:P2} exceeds 1% threshold");
}
```

**测试后收口要求**：
- macOS 下在 `dotnet test`、`dotnet run`、IDE 调试之间频繁切换时，若存在未退出的 `FC-Revolution.UI`、`dotnet`、`testhost` 残留进程，可能继续占用输出目录中的程序集 / PDB / dylib。
- 典型现象包括：游戏窗口无法正常拉起、重新运行看似无响应、或新进程仍复用旧实例。
- 每次完成渲染测试、UI 调试或手动烟测后，必须先检查是否有残留进程，再进行实际运行。
- 若发现残留进程，优先正常退出；必要时再手动结束进程。仓库内已提供 `scripts/fc-clean-residual-processes.sh` 用于检查和清理；检查与清理命令见附录 A。

---

## Frame Pacing 架构

### 三条时间线解耦

```
┌─────────────────────────────────────────────────────────────────┐
│                     FC-Revolution 时间线架构                     │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Simulation Thread（固定 60Hz）                                  │
│      DispatcherTimer / 精确 16.67ms 计时                         │
│      NesConsole.RunFrame() → FrameMetadata                       │
│      写入 DoubleBuffer.Back                                      │
│      ↓ 交换                                                      │
│  DoubleBuffer（线程安全交换）                                     │
│      ↓ 读取                                                      │
│  Render Thread（跟随 CADisplayLink vsync）                       │
│      读取 DoubleBuffer.Front                                     │
│      GPU 渲染 → MetalFX → CAMetalLayer.present()                │
│                                                                  │
│  Audio Thread（时间主导）                                         │
│      APU 按实际时间推进                                           │
│      音频缓冲区独立，不受渲染帧率影响                              │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### DoubleBuffer 实现

```csharp
// FC-Revolution.Rendering/Common/FrameDoubleBuffer.cs
public sealed class FrameDoubleBuffer<T> where T : class
{
    private T? _front;
    private T? _back;
    private readonly object _lock = new();

    public void WriteBack(T frame)
    {
        lock (_lock) { _back = frame; }
    }

    public void Swap()
    {
        lock (_lock) { (_front, _back) = (_back, _front); }
    }

    public T? ReadFront()
    {
        lock (_lock) { return _front; }
    }
}
```

### CADisplayLink 接入（macOS）

```objc
// 渲染端使用 CADisplayLink 而非 NSTimer，保证 vsync 对齐
CVDisplayLinkRef _displayLink;

static CVReturn DisplayLinkCallback(
    CVDisplayLinkRef link,
    const CVTimeStamp* now,
    const CVTimeStamp* outputTime,
    CVOptionFlags flagsIn,
    CVOptionFlags* flagsOut,
    void* ctx)
{
    FCRRenderer* r = (__bridge FCRRenderer*)ctx;
    [r renderFrameIfReady];  // 读取 DoubleBuffer.Front，提交 GPU
    return kCVReturnSuccess;
}
```

---

## Capability + Policy 运行时

### macOS 平台能力实现

```csharp
// FC-Revolution.Rendering/Metal/MacOSCapabilities.cs
public sealed class MacOSCapabilities : IPlatformCapabilities
{
    public bool SupportsMetalFXTemporal  => IsAppleSilicon && OSVersion >= new Version(13, 0);
    public bool SupportsMetalFXSpatial   => IsAppleSilicon && OSVersion >= new Version(13, 0);
    public bool SupportsDLSS4            => false;
    public bool SupportsFSR4             => false;
    public bool SupportsXeSS2            => false;
    public bool SupportsFrameGeneration  => false;  // MetalFX 当前无帧生成 API
    public int  MaxTextureSize           => IsAppleSilicon ? 16384 : 8192;

    public int RecommendedInternalWidth  => ChipTier switch
    {
        AppleSiliconTier.M1Base     => 1280,
        AppleSiliconTier.M1ProMax   => 1920,
        AppleSiliconTier.M2Plus     => 1920,
        AppleSiliconTier.M3Plus     => 1920,
        AppleSiliconTier.M4Plus     => 2560,
        _                           => 1280
    };

    public int RecommendedInternalHeight => RecommendedInternalWidth * 240 / 256;

    private bool IsAppleSilicon => RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                                   && OperatingSystem.IsMacOS();

    private Version OSVersion => Environment.OSVersion.Version;

    private AppleSiliconTier ChipTier => DetectChipTier();

    private static AppleSiliconTier DetectChipTier()
    {
        // 通过 sysctlbyname("hw.model") 读取芯片型号
        // 或通过核心数量 / GPU 核心数估算
        // ...
        return AppleSiliconTier.M1Base; // 默认最保守配置
    }
}

public enum AppleSiliconTier { M1Base, M1ProMax, M2Plus, M3Plus, M4Plus }
```

### 运行时配置流程

```csharp
// 应用启动时执行一次
var caps    = new MacOSCapabilities();
var userSettings = UserRenderSettings.Load();  // 从配置文件读取

// RenderPolicy 解析最终配置
var features = RenderPolicy.Resolve(userSettings, caps);

// 传给渲染器
_renderer = RendererFactory.Create(features);
```

---

## 验收标准（各阶段 Definition of Done）

### Phase 1：抽象层 + PPU 元数据提取

| 验收项 | 检验方式 |
|--------|---------|
| `IFrameMetadata` 提取正确 | 单元测试：Mario 帧 300 的精灵坐标与 PPU OAM 一致 |
| `VisibleTileResolver` scroll 正确 | 单元测试：Mario 水平 scroll = 32 时，tile 列表左边界 tileId 正确 |
| Nametable mirroring 正确 | 单元测试：vertical mirror / horizontal mirror 各一个 ROM |
| Motion vector 基本正确 | 单元测试：精灵从 X=10 移到 X=20，mvX = 10 * scaleX |

### Phase 2：macOS Metal 基础渲染（Step 1）

| 验收项 | 检验方式 |
|--------|---------|
| 背景渲染正确 | ReferenceRenderer diff < 1% |
| 精灵优先级正确 | 人工验证：Mario 在管道后时被遮挡 |
| Scroll 边界无撕裂 | 人工验证：冒险岛横向滚动 |
| 帧率稳定 | Metal GPU Capture：frame time < 5ms |

### Phase 3：MetalFX Spatial（Step 2）

| 验收项 | 检验方式 |
|--------|---------|
| 超分分辨率正确 | 输出分辨率 = 显示器分辨率 |
| 无明显画质损失 | 人工对比 Spatial vs 无超分 |
| M1 帧率达标 | 60fps，GPU < 8ms/帧 |

### Phase 4：MetalFX Temporal（Step 3）

| 验收项 | 检验方式 |
|--------|---------|
| 横向滚动无拖影 | 人工验证：冒险岛、马里奥背景清晰 |
| 快速精灵无鬼影 | 人工验证：忍者神龟战斗场面 |
| 存档读取 reset 正确 | 存档读取后第 1-3 帧无残影 |
| 优于 Spatial 画质 | 静止场景 Temporal vs Spatial 对比 |

---

## 附录

### A. 调试命令速查

```bash
# 优先使用仓库脚本检查残留 UI / testhost / dotnet 进程
scripts/fc-clean-residual-processes.sh --check

# 若脚本输出异常，也可直接用 ps + rg 回退检查
ps -ax -o pid=,ppid=,etime=,command= | rg "FC-Revolution.UI|testhost|dotnet"

# 仅查看 FC-Revolution.UI 相关进程
pgrep -af "FC-Revolution.UI"

# 使用仓库脚本结束残留进程（先 TERM，再对顽固进程 KILL）
scripts/fc-clean-residual-processes.sh --kill

# 必要时结束残留 testhost 进程（谨慎使用）
pkill -f "testhost"

# 编译 Obj-C 桥接层（arm64）
clang -arch arm64 -fmodules -fobjc-arc \
  -framework Metal -framework MetalFX -framework QuartzCore \
  -dynamiclib -o FCRMetalBridge.dylib \
  FCRRenderer.m FCRMetalBridge.m

# 验证 dylib 导出符号
nm -gU FCRMetalBridge.dylib | grep FCR_

# Metal GPU Capture（命令行触发）
xcrun -sdk macosx metalvalidation
```

### B. 相关文档

- [主渲染方案文档](./RENDERING_PLATFORM_GUIDE.md)
- [Apple Metal Best Practices](https://developer.apple.com/documentation/metal/best_practices_guides)
- [MetalFX API Reference](https://developer.apple.com/documentation/metalfx)
- [NES PPU Loopy Scroll 详解](https://www.nesdev.org/wiki/PPU_scrolling)
- [NES Sprite Priority](https://www.nesdev.org/wiki/PPU_OAM#Byte_2_(%E2%80%93_Attributes))

### C. 更新日志

| 版本 | 日期 | 变更 |
|------|------|------|
| 1.6 | 2026-03-31 | 新增独立执行清单文档 `MACOS_SPATIAL_TEMPORAL_EXECUTION_CHECKLIST.md`，专门承接 2.2 之后的 macOS 后续任务；覆盖 Spatial、Temporal、统一验证顺序、运行 hygiene 与收口标准，并在本文档中补充入口链接 |
| 1.5 | 2026-03-31 | 补充测试运行注意事项：记录 macOS 下残留 `FC-Revolution.UI` / `dotnet` / `testhost` 进程可能占用输出产物、影响游戏窗口重新拉起；在“自动化回归测试”和“调试命令速查”中新增测试后进程检查与清理说明 |
| 1.4 | 2026-03-31 | 完成 Phase 2.2：补充 `MacMetalOffscreenRenderer` 与 `FCR_RenderLayeredFrameOffscreen`；新增离屏 Metal 输出读回；`FC-Revolution.Rendering.Tests` 增加 GPU vs `ReferenceFrameRenderer` 自动化 diff 回归并通过（30 / 30）；`FCRMetalBridge` 构建逻辑抽为共享 targets，测试项目与 UI 输出均可自动携带 `libFCRMetalBridge.dylib` |
| 1.3 | 2026-03-31 | 完成 Phase 2.1：新增 `FCRMetalBridge.m`、`MacMetalPresenter`、`MacMetalViewHost`；`GameWindow` 主视口接入 `NativeControlHost + CAMetalLayer` 原生 presenter；保留软件 `Image` 回退；`FC-Revolution.UI` 默认构建与 `-r osx-arm64` 构建通过；bridge 产物为 `arm64 + x86_64` universal dylib，并导出 `FCR_*` 符号 |
| 1.2 | 2026-03-31 | 完成 Phase 1.2：新增 `PpuRenderStateSnapshot`；实现 `IRenderDataExtractor` / `RenderDataExtractor`；`FrameMetadata` 增补 scroll、mirroring、visible tiles 元信息；新增 extractor 测试并通过 |
| 1.1 | 2026-03-31 | 新增开发进度跟踪；完成开发前提检查；落地 `FC-Revolution.Rendering` / `FC-Revolution.Rendering.Tests` 工程骨架；实现 `VisibleTileResolver`、`MotionVectorGenerator`、`FrameDoubleBuffer`；新增 15 个基础单元测试 |
| 1.0 | 2026-03-31 | 初始版本：macOS 三步走开发节奏；5 个关键陷阱详解；Debug Renderer 流程；Frame Pacing 架构；Capability 运行时；各阶段验收标准 |

---

*文档结束*
