# FC-Revolution 可插拔多核心架构重构方案

## 1. 文档目标

本文档定义 FC-Revolution 从当前的 `NES/Famicom 单核心应用` 重构为 `可插拔多模拟器宿主` 的完整设计方案。

目标能力：

1. 保留当前 `C#/.NET` 宿主层与现有 `FC/NES` 核心的可用性。
2. 将模拟器核心重构为可插拔模块，支持后续接入 `SFC`、`GB`、`MD` 等不同系统核心。
3. 支持两类核心实现路径：
   - `managed-dotnet`：适合当前 `C#` 核心
   - `native-cabi`：适合未来 `C/C++` 核心
4. 使用统一的 `.fcr` JSON 文档体系承载核心头文件、核心本地配置、核心注册表等数据。
5. 支持类似 RetroArch 的核心下载、安装、校验、选择、加载、更新与卸载流程。
6. 为未来多系统调试、回溯、状态存档、串流、输入映射与渲染适配保留扩展位。
7. 让主程序在“零核心”状态下仍可启动，并把 bundled core 明确降级为发行策略而不是架构前提。
8. 让核心可以位于主程序仓库之外，独立设计、独立测试、独立打包与独立发布。
9. 提供一套由主程序和 `Core Workbench` 共享的 Host Runtime，避免复制核心加载、目录探测、包校验与会话驱动逻辑。

非目标：

1. 本次不实现 `SFC` 或其他新核心。
2. 本次不直接重写当前 `NES` 核心逻辑。
3. 本次不一次性改造完所有 UI 与后端模块，而是给出可执行的分期重构路径。
4. 本次不要求立刻把所有现有核心都搬出主仓库，而是先定义稳定边界并以一个参考核心做外置试点。

## 2. 当前架构诊断

当前仓库的产品层已经比较成熟，但核心宿主边界仍然强绑定于 `NES` 具体实现。

更聚焦的现状判断、优先级和差距结论见
[FC-REVOLUTION-INDEPENDENT-CORE-HOSTING-REVIEW.md](/Users/pxm/Desktop/Cs/FC/FC-Revolution/docs/refactor-plans/FC-REVOLUTION-INDEPENDENT-CORE-HOSTING-REVIEW.md)。

### 2.1 当前有利条件

1. 宿主层已经具备 UI、后端宿主、串流、存储、时间线、预览等完整产品能力。
2. `NesConsole` 已经承担了统一入口角色，对外暴露了跑帧、音频、存档、读档、输入等关键语义。
3. 当前仓库已存在原生桥接经验：
   - `MacMetalBridge`
   - `FFmpegRuntimeBootstrap`
4. 当前 `.fcr` 文档已经具备基础的版本、类型、时间戳、迁移逻辑。

### 2.2 当前核心问题

1. 宿主直接依赖 `NesConsole`、`Ppu2C02`、`Bus.Read/Write` 等具体类型。
2. 时间线、调试、渲染元数据都编码了 `NES/6502/PPU` 的硬件语义。
3. 存档结构固定为 `CPU/PPU/RAM/APU/Cart` 五段，不适合作为多系统通用状态封装。
4. 输入系统假定 `2P + NesButton`。
5. 渲染抽象名义上存在，但接口仍直接吃 `Ppu2C02`。
6. 当前 `.fcr` 主要是本地配置文档，还不是“核心分发头文件”。
7. 当前原生动态库加载是“应用内固定依赖加载”，还不是“每个核心独立加载上下文”。
8. 当前主程序与核心构建、默认 bootstrap、目录探测曾长期混杂在同一产品壳里；本轮之后 headless 预览驱动也已开始下沉到共享运行时，但预览编码与 legacy preview 资产流程仍未完全脱离产品壳。

### 2.3 当前耦合点汇总

| 区域 | 当前耦合 | 问题 |
|---|---|---|
| 会话运行 | `GameWindowViewModel` 直接持有 `_nes` | 宿主无法切换为其他系统核心 |
| 时间线/回溯 | 依赖 `_nes.Ppu.Frame`、`_nes.CpuCycles`、`FrameSnapshot` | 时间语义绑定 NES |
| 调试 | 依赖 `DebugState`、`Bus.Read/Write`、零页/栈页 | 绑定 6502/NES 地址空间 |
| 渲染提取 | `IRenderDataExtractor.Extract(Ppu2C02 ppu, ...)` | “抽象层”实际上仍是 NES-only |
| 串流 | 直接监听 `FrameReady` / `AudioChunkReady` | 缺少通用帧包/音频包协商 |
| 核心装载 | `NesConsole` 直接构造 `Cpu6502/Ppu2C02/Apu2A03/NesBus` | 核心不可替换 |
| ROM/Cartridge | `ICartridge`、mapper、mirroring | 媒体模型被写死为 NES 卡带 |
| 状态存档 | `StateSnapshotData` 固定五段状态 | 不能兼容其他系统 |
| 输入模型 | `NesButton`、两手柄 | 不能泛化到其他设备 |
| FCR 文档 | `system.fcr`、ROM profile `.fcr` | 尚未形成“核心分发文档族” |
| 构建/交付 | UI 已去掉对 FC legacy adapter 的编译期依赖，改为 runtime-only adapter 注入；但 adapter 与具体核心仍位于主仓内，build 阶段仍会注入示例核心 | 说明“直接引用核心项目”问题已继续收口，但核心尚未完全脱离主程序仓库与主程序构建流程 |

## 3. 重构目标架构

### 3.1 总体原则

1. 宿主统一。
2. 核心自治。
3. 能力按需暴露。
4. 文档与包格式统一使用 `.fcr` 家族。
5. `binaryKind` 决定加载端口。
6. `sourceLanguage` 只作为签名后的可信元数据，不参与实际加载分流。
7. 主程序与 `Core Workbench` 必须共用同一套 Host Runtime，而不是复制 loader / package / probe-path 逻辑。
8. 主程序仓库与核心仓库边界必须分离，零核心启动能力是默认架构验收项。

### 3.2 目标模块图

```text
FC-Revolution.UI
FC-Revolution.Backend.Hosting
FC-Revolution.CoreWorkbench
FC-Revolution.Storage
FC-Revolution.Rendering
        │
        ▼
FC-Revolution.Emulation.Host (Shared Host Runtime)
        │
        ├── FC-Revolution.Emulation.Abstractions
        │       ├── 核心会话接口
        │       ├── 能力接口
        │       ├── FCR 文档模型
        │       ├── 输入模型
        │       ├── 状态/时间线模型
        │       └── 核心清单与注册表模型
        │
        ├── FC-Revolution.CoreLoader.Managed
        │       └── AssemblyLoadContext + IManagedCoreModule
        │
        ├── FC-Revolution.CoreLoader.Native
        │       └── NativeLibrary + FCR_GetCoreApi
        │
        ├── FC-Revolution.CoreCatalog
                ├── 下载
                ├── 解压
                ├── 哈希校验
                ├── 签名校验
                ├── 注册
                └── 版本管理
        │
        └── Shared Session Services
                ├── 核心检查 / manifest inspection
                ├── 目录探测 / probe-path 装载
                ├── headless 预览驱动
                └── smoke test / session 驱动

主程序仓库之外的核心仓库
├── fc-revolution-core-nes
│   ├── src/
│   ├── tests/
│   ├── packaging/
│   └── artifacts/*.fcrcore.zip
├── future-core-snes
└── future-core-gb

用户机器上的已安装核心
├── FC-Revolution.Core.Nes.Managed
├── Future.Core.Snes.Native
├── Future.Core.Gb.Managed
└── Future.Core.Md.Native
```

### 3.3 设计分层

#### A. 宿主层

职责：

1. ROM 库管理
2. UI 会话生命周期
3. 核心选择
4. 下载/安装/更新/卸载
5. 时间线 UI 与回放 UI
6. 串流与远程控制
7. 通用调试入口
8. 存储与配置

宿主层禁止再依赖：

1. `NesConsole`
2. `Ppu2C02`
3. `Cpu6502`
4. `Apu2A03`
5. `NesBus`
6. `NesButton`

#### B. 核心抽象层

职责：

1. 定义统一的核心会话接口
2. 定义核心能力模型
3. 定义输入 schema
4. 定义状态/时间线抽象
5. 定义核心清单和包格式
6. 定义 managed/native 加载入口约定

#### C. 核心实现层

职责：

1. 实现具体硬件模拟
2. 对宿主暴露统一接口
3. 按需暴露能力
4. 自行处理各系统媒体格式、硬件状态、调试信息

#### D. 共享运行时层

职责：

1. 统一提供核心发现、目录探测、包安装、manifest inspection、会话创建与 capability 路由
2. 向主程序和 `Core Workbench` 同时暴露可复用的会话驱动、预览生成、smoke test 服务
3. 把“如何加载核心”从具体 UI/工具工作流中隔离出来

禁止事项：

1. 不直接依赖某个具体系统核心
2. 不直接内嵌主程序 UI 状态、中文展示文案或产品壳专属配置流程

### 3.4 仓库边界与交付拓扑

目标拓扑建议明确为：

1. 主程序仓库负责：
   - 抽象层
   - 共享 Host Runtime
   - 主程序 UI / Backend
   - `Core Workbench`
   - package / manifest / 校验工具链
2. 每个模拟器核心仓库负责：
   - 核心实现
   - 该核心专属 adapter
   - 核心单元测试 / 集成测试
   - pack 脚本与发布产物
3. 主程序启动时允许没有任何核心。
4. 是否预装一个 bundled core，属于发行包策略，不属于运行时架构前提。

过渡期策略：

1. 可以先保留一个参考核心在主仓库内，作为迁移样板。
2. 但它最终也应遵循与外部核心仓库一致的 build / test / pack / install 合约。

## 4. 统一核心接口设计

### 4.1 模块发现接口

托管核心应实现：

```csharp
public interface IManagedCoreModule
{
    CoreManifest Manifest { get; }
    IEmulatorCoreFactory CreateFactory();
}
```

### 4.2 核心工厂接口

```csharp
public interface IEmulatorCoreFactory
{
    IEmulatorCoreSession CreateSession(CoreSessionCreateOptions options);
}
```

### 4.3 核心会话接口

```csharp
public interface IEmulatorCoreSession : IDisposable
{
    event Action<VideoFramePacket>? VideoFrameReady;
    event Action<AudioPacket>? AudioReady;

    CoreRuntimeInfo RuntimeInfo { get; }
    CoreCapabilitySet Capabilities { get; }
    IInputSchema InputSchema { get; }

    CoreLoadResult LoadMedia(CoreMediaLoadRequest request);
    void Reset();
    void Pause();
    void Resume();
    CoreStepResult RunFrame();
    CoreStepResult StepInstruction();

    CoreStateBlob CaptureState(bool includeThumbnail = false);
    void RestoreState(CoreStateBlob state);

    bool TryGetCapability<TCapability>(out TCapability capability)
        where TCapability : class;
}
```

### 4.4 关键说明

1. 宿主只依赖 `IEmulatorCoreSession`。
2. 视频和音频必须统一为包类型，不能再直接裸传 `uint[]` / `float[]`。
3. 调试、反汇编、渲染元数据、时间线等全部通过 capability 获取。
4. 存档统一为 `CoreStateBlob`，内部状态布局由具体核心负责。

## 5. 能力模型设计

### 5.1 基础能力

所有核心至少声明：

1. `video-frame`
2. `audio-output`
3. `save-state`
4. `media-load`
5. `input-schema`

### 5.2 可选能力

通用 capability：

1. `time-travel`
2. `debug-memory`
3. `debug-registers`
4. `disassembly`
5. `layered-render`
6. `cheat-patch`
7. `media-probe`
8. `achievement-hooks`
9. `netplay-state-sync`

### 5.3 系统特化 capability

不要把系统特有内容强行塞进基础接口。

示例：

1. `system-nes-render-state`
2. `system-snes-ppu-layers`
3. `system-gb-lcd-state`
4. `system-md-vdp-state`

宿主策略：

1. 没有 capability 时，游戏仍可运行。
2. 有 capability 时，点亮对应高级 UI。
3. UI 不直接依赖某个具体系统核心，而是依赖 capability id 和对应接口。

## 6. 输入模型设计

当前输入模型是 `NesButton + 2P`，必须改成 manifest 驱动。

### 6.1 抽象模型

```csharp
public interface IInputSchema
{
    IReadOnlyList<InputPortDescriptor> Ports { get; }
    IReadOnlyList<InputActionDescriptor> Actions { get; }
}
```

### 6.2 Manifest 中声明

每个核心在清单中声明：

1. 端口数量
2. 每个端口支持的设备类型
3. 每种设备支持的动作
4. 数字/模拟/相对轴类型

### 6.3 宿主改造原则

1. 现有 `NesButton` 映射逻辑保留在 `NES` 核心适配层，不再进入宿主通用层。
2. `SystemConfigProfile` 与 `RomConfigProfile` 中的输入覆盖要迁移为 `按 coreId + systemId + inputSchemaVersion` 命名空间存储。
3. 后端远程控制协议未来要从 “NES 按钮 DTO” 演进为 “动作名 + 端口 + 值”。

### 6.4 后端协议迁移策略

当前后端与 WebSocket/远控契约仍然直接绑定：

1. `NesButtonDto`
2. `ButtonStateRequest(int Player, NesButtonDto Button, bool Pressed)`
3. `ChannelReader<uint[]>` 视频
4. `ChannelReader<float[]>` 音频

这套协议在 `NES` 内部可行，但无法直接承载：

1. 多设备类型
2. 模拟轴输入
3. 可变玩家数量
4. 可协商的视频/音频格式
5. 不同系统核心的动作语义

建议演进为双协议阶段：

#### 阶段 A：新增通用协议，不立刻删除 NES 旧协议

新增建议 DTO：

```csharp
public sealed record InputActionValueDto(
    string PortId,
    string DeviceType,
    string ActionId,
    float Value);

public sealed record SetInputStateRequest(
    IReadOnlyList<InputActionValueDto> Actions);

public sealed record VideoFramePacketDescriptor(
    int Width,
    int Height,
    string PixelFormat,
    long PresentationIndex,
    double TimestampSeconds);

public sealed record AudioPacketDescriptor(
    int SampleRate,
    int Channels,
    string SampleFormat,
    int SampleCount,
    double TimestampSeconds);
```

#### 阶段 B：兼容桥接

1. `NES managed core` 保留 legacy `NesButtonDto` 入口。
2. 后端桥接层负责把 legacy DTO 映射为通用动作：
   - `player=1 + button=A` -> `portId=p1, actionId=a, value=1`
3. 新 UI / WebPad / API 客户端优先走通用协议。
4. 待宿主与前端全部迁移完，再废弃 NES-only DTO。

#### 阶段 C：流媒体协议协商

`BackendStreamSubscription` 后续应升级为：

1. 元数据 + 数据包分离
2. 会话建立时协商：
   - 视频像素格式
   - 音频采样格式
   - 声道数
   - chunk 策略
3. 核心产出统一 `VideoFramePacket` / `AudioPacket`
4. 串流编码器根据 descriptor 进行适配

结论：

1. 现有 NES DTO 不应直接删除。
2. 但必须尽快被包裹成兼容层，而不是继续扩展。

## 7. 通用状态与时间线模型

### 7.1 新状态封装

当前 `StateSnapshotData` 固定为 `CpuState/PpuState/RamState/CartState/ApuState`，应改为：

```csharp
public sealed class CoreStateBlob
{
    public string SystemId { get; init; } = "";
    public string CoreId { get; init; } = "";
    public string CoreVersion { get; init; } = "";
    public int StateFormatVersion { get; init; }
    public long FrameOrStep { get; init; }
    public double TimestampSeconds { get; init; }
    public byte[] OpaqueState { get; init; } = [];
    public byte[]? ThumbnailBytes { get; init; }
}
```

### 7.2 时间线模型

统一目标：

1. 宿主只保存 `checkpoint`。
2. checkpoint 内部可包含：
   - 时间戳
   - 展示缩略图
   - 核心 opaque state
3. 时间线不再依赖 `Ppu.Frame` 或 `CPU 1789773 Hz` 常量。

### 7.3 时间线服务抽象

```csharp
public interface ITimeTravelService
{
    CoreCheckpoint CaptureCheckpoint();
    void RestoreCheckpoint(CoreCheckpoint checkpoint);
    long GetMonotonicPresentationIndex();
    double GetPresentationTimestampSeconds();
}
```

`NES` 核心可以继续内部按 frame 记录。  
`SFC` 或其他系统核心则可以按自己的 presentation frame 或 master clock 映射。

### 7.3.1 当前落地快照（2026-04-08）

当前代码已经先落下一版“过渡态 capability 边界”，用于把 UI 从 `FrameSnapshot` / `BranchPoint` 直接耦合中解开，但暂未完全收敛到文档中最小化的 `CoreCheckpoint` 形态。

已落地内容：

1. `ITimeTravelService` 对 UI 暴露的时间线对象已改为抽象层模型：
   - `CoreTimelineSnapshot`
   - `CoreBranchPoint`
2. `CoreTimelineSnapshot` 当前承载：
   - `Frame`
   - `TimestampSeconds`
   - `Thumbnail`
   - `CoreStateBlob State`
3. `CoreBranchPoint` 当前承载：
   - 分支标识与名称
   - `Frame`
   - `TimestampSeconds`
   - `Snapshot`
   - `CreatedAt`
   - 子分支集合
4. `GameWindowViewModel.Timeline`、`MainWindowViewModel` 与 `BranchGalleryViewModel` 已切到消费 `CoreTimelineSnapshot` / `CoreBranchPoint`。
5. `FrameSnapshot` / `BranchPoint` 目前已退到：
   - `NES` core 内部时间线实现
   - 时间线仓储与持久化
   - UI 侧的窄桥接 `CoreTimelineModelBridge`

仍待继续收口：

1. 把时间线仓储从 `FrameSnapshot` / `BranchPoint` 持久化模型进一步提升为抽象层 checkpoint / branch metadata。
2. 把当前 `Frame` 语义进一步抽象成更通用的 presentation index / monotonic index。
3. 在保留 `NES` 兼容的前提下，把 `ITimeTravelService` 进一步收敛到文档里更简化的 checkpoint API。

### 7.4 状态兼容策略

仅有 `CoreVersion` 和 `StateFormatVersion` 还不够。

状态恢复至少要回答四个问题：

1. 是否必须同 `coreId`
2. 是否必须同 `systemId`
3. 是否允许跨版本恢复
4. 是否允许只读降级导入

建议在 `core-manifest.fcr` 中新增：

```json
"stateCompatibility": {
  "schemaId": "fc-revolution.nes.state",
  "currentFormatVersion": 1,
  "minimumReadableFormatVersion": 1,
  "sameCoreOnly": true,
  "crossVersionPolicy": "same-major"
}
```

宿主恢复规则建议为：

1. `systemId` 不同：直接拒绝恢复
2. `coreId` 不同且 `sameCoreOnly = true`：直接拒绝恢复
3. `schemaId` 不同：直接拒绝恢复
4. `StateFormatVersion` 超出 manifest 可读范围：拒绝恢复
5. 同 `coreId` 且策略允许：
   - `exact`：仅完全相同版本
   - `same-major`：允许同主版本恢复
   - `backward-compatible-range`：按 manifest 声明范围恢复
6. 被拒绝恢复的状态可保留为只读条目，供用户导出或迁移

时间线 checkpoint 也必须沿用同一套兼容规则，不能只对手动存档生效。

## 8. FCR 文档家族设计

### 8.1 总体原则

保留当前 `.fcr` 的 JSON 风格，但提升为统一文档家族，而不是继续把所有内容塞进现有 profile 类。

### 8.2 通用 envelope

```json
{
  "fcrFormatVersion": 1,
  "documentKind": "FC-Revolution-Core-Manifest",
  "documentVersion": 1,
  "createdAtUtc": "2026-04-07T12:00:00Z",
  "lastUpdatedAtUtc": "2026-04-07T12:00:00Z",
  "payload": {}
}
```

### 8.3 文档种类

建议拆分为：

1. `core-manifest.fcr`
   - 随核心包分发
   - 只读
   - 可签名
   - 用于安装、校验、路由加载
2. `core-config.fcr`
   - 本地可变
   - 记录核心默认选项、偏好与特定开关
3. `core-registry.fcr`
   - 本地核心注册表
   - 记录已安装核心、版本、路径、状态
4. `machine-config.fcr`
   - 现有 `system.fcr` 的演进版
   - 记录宿主全局设置
5. `game-profile.fcr`
   - 现有 ROM 配置的演进版
   - 记录某游戏的宿主偏好与资源引用

### 8.4 为什么不能把当前 `.fcr` 直接当核心头文件

当前 `.fcr` 里已经包含：

1. `machineFingerprint`
2. 本地资源路径/对象引用
3. 修改内存
4. 本地输入覆盖

这些都属于本地配置，不适合进入“核心分发头文件”。

结论：

1. 要复用 `.fcr` 体系。
2. 但必须拆成不同 `documentKind`。
3. `core-manifest.fcr` 不应该包含机器指纹。

## 9. 核心头文件设计

### 9.1 运行时头文件

运行时真正用于安装和加载的头文件建议是：

`core-manifest.fcr`

其关键字段：

```json
{
  "fcrFormatVersion": 1,
  "documentKind": "FC-Revolution-Core-Manifest",
  "documentVersion": 1,
  "payload": {
    "coreId": "fc-revolution.nes.managed",
    "displayName": "FC Revolution NES",
    "version": "1.0.0",
    "systemIds": ["nes", "famicom"],
    "hostApiVersion": "1.0.0",
    "binaryKind": "managed-dotnet",
    "sourceLanguage": "csharp",
    "executionModel": "inproc",
    "entryPoint": {
      "assemblyPath": "managed/FCRevolution.Core.Nes.Managed.dll",
      "factoryType": "FCRevolution.Core.Nes.Managed.NesManagedCoreModule"
    },
    "mediaSupport": {
      "extensions": [".nes", ".fds", ".unf", ".unif"]
    },
    "capabilities": [
      "video-frame",
      "audio-output",
      "save-state",
      "time-travel",
      "debug-memory",
      "debug-registers",
      "system-nes-render-state"
    ],
    "inputPorts": [
      { "portId": "p1", "deviceTypes": ["nes-standard-pad"] },
      { "portId": "p2", "deviceTypes": ["nes-standard-pad"] }
    ],
    "files": [
      {
        "path": "managed/FCRevolution.Core.Nes.Managed.dll",
        "sha256": "..."
      }
    ],
    "stateCompatibility": {
      "schemaId": "fc-revolution.nes.state",
      "currentFormatVersion": 1,
      "minimumReadableFormatVersion": 1,
      "sameCoreOnly": true,
      "crossVersionPolicy": "same-major"
    },
    "signature": {
      "publisherId": "fc-revolution",
      "algorithm": "ed25519",
      "keyId": "official-store",
      "value": "..."
    }
  }
}
```

### 9.2 `binaryKind` 与 `sourceLanguage` 的职责分离

必须明确：

1. `binaryKind`
   - 决定宿主如何加载
   - 是硬路由字段
2. `sourceLanguage`
   - 表示实现语言
   - 是签名后的可信元数据
   - 不决定加载方式

举例：

1. `binaryKind = managed-dotnet` + `sourceLanguage = csharp`
2. `binaryKind = native-cabi` + `sourceLanguage = cpp`
3. `binaryKind = native-cabi` + `sourceLanguage = csharp`
   - 对应未来 `C# NativeAOT`

### 9.3 为什么还要保留 C 头文件

对 `C/C++` 核心作者，还需要一个 SDK 头文件：

`fcr_core_api.h`

它不是运行时发现机制，而是开发接口契约。

## 10. Managed 与 Native 双端口设计

### 10.1 Managed 端口

模块：

`FC-Revolution.CoreLoader.Managed`

职责：

1. 每个核心版本创建独立 `AssemblyLoadContext`
2. 加载 `managed-dotnet` 核心
3. 找到 `IManagedCoreModule`
4. 创建 `IEmulatorCoreFactory`
5. 实例化 `IEmulatorCoreSession`

关键要求：

1. 避免不同版本核心间程序集冲突
2. 支持卸载
3. 限制宿主可见 API 面

### 10.2 Native 端口

模块：

`FC-Revolution.CoreLoader.Native`

职责：

1. 用 `NativeLibrary.Load` 加载 manifest 指定动态库
2. 解析 `FCR_GetCoreApi`
3. 校验 `ABI version`
4. 建立本地函数表
5. 包装成 `IEmulatorCoreSession`

### 10.3 Native C ABI 约定

建议导出：

```c
typedef struct FcrCoreApi
{
    uint32_t abi_version;
    uint32_t struct_size;

    const char* (*get_core_id)(void);
    const char* (*get_manifest_json)(void);

    void* (*create_session)(const void* host_api);
    void  (*destroy_session)(void* session);

    int   (*load_media)(void* session, const uint8_t* data, int32_t length, const char* file_name);
    void  (*reset)(void* session);
    void  (*pause)(void* session);
    void  (*resume)(void* session);
    int   (*run_frame)(void* session);
    int   (*step_instruction)(void* session);

    int   (*copy_video_frame)(void* session, void* dst, int32_t dst_length);
    int   (*copy_audio_packet)(void* session, void* dst, int32_t dst_length);

    int   (*capture_state)(void* session, uint8_t* dst, int32_t capacity);
    int   (*restore_state)(void* session, const uint8_t* data, int32_t length);
} FcrCoreApi;

const FcrCoreApi* FCR_GetCoreApi(uint32_t host_abi_version);
```

### 10.4 未来预留端口

预留：

1. `external-process`
   - 用于不可信原生核心隔离
2. `wasm-sandbox`
   - 远期预留

## 11. 核心包格式与目录布局

### 11.1 核心包目录

```text
cores/
  installed/
    fc-revolution.nes.managed/
      1.0.0/
        core-manifest.fcr
        managed/
          FCRevolution.Core.Nes.Managed.dll

    future.snes.native/
      0.1.0/
        core-manifest.fcr
        native/
          win-x64/fcr_snes_core.dll
          osx-arm64/libfcr_snes_core.dylib
          linux-x64/libfcr_snes_core.so

  registry/
    core-registry.fcr

  config/
    fc-revolution.nes.managed/
      core-config.fcr

  cache/
  temp/
  packages/
```

### 11.2 对当前存储层的建议

当前 `ObjectStorageBucket` 只有：

1. `Roms`
2. `PreviewVideos`
3. `Configurations`
4. `Saves`
5. `Images`
6. `Other`

建议新增两种可选路径：

方案 A：

1. 扩展 bucket：
   - `Cores`
   - `CorePackages`
   - `CoreCache`

方案 B：

1. 在 `Other` 下建立标准子树：
   - `cores/installed`
   - `cores/packages`
   - `cores/cache`
   - `cores/temp`

建议优先使用方案 A，原因：

1. 路径职责清晰
2. 更容易做权限边界与清理策略
3. 更适合未来核心市场和离线包管理

### 11.3 外部核心仓库与独立测试流

推荐把核心开发工作流分成“核心仓库内完成”和“主程序/Workbench 验证”两段：

#### A. 核心仓库内完成

每个核心仓库建议具备下面结构：

```text
fc-revolution-core-foo/
  src/
    FC-Revolution.Core.Foo/
  tests/
    FC-Revolution.Core.Foo.Tests/
  packaging/
    core-manifest.template.fcr
    pack.(ps1|sh)
  artifacts/
    *.fcrcore.zip
```

核心仓库本地应完成：

1. 单元测试
2. 最小集成测试
3. manifest 生成
4. 包产物生成

#### B. 使用共享运行时做产品级验证

验证入口建议统一为两种：

1. `Core Workbench`
   - 图形化加载 `probe path` 或 `*.fcrcore.zip`
   - 查看 manifest / capability / 输入 schema / 包内容
   - 运行最小 ROM smoke test / headless preview / 调试能力检查
2. CLI checker
   - 适合 CI
   - 负责 manifest / 包结构 / hash / 入口点 / 最小加载闭环检查

关键约束：

1. `Core Workbench` 与 CLI checker 必须共用主程序同一套 Host Runtime / loader / package service。
2. 不允许为测试工具复制另一条“专用装载逻辑”。
3. 主程序加载核心和 Workbench 加载核心，必须走相同 manifest / loader / session contract。

## 12. 核心下载、安装与校验流程

### 12.1 安装流程

1. 下载核心包 `*.fcrcore.zip`
2. 解压到 `temp`
3. 读取 `core-manifest.fcr`
4. 校验 `documentKind`
5. 校验 `fcrFormatVersion` / `documentVersion`
6. 校验 `hostApiVersion`
7. 校验 `files[].sha256`
8. 校验签名
9. 校验当前平台是否存在可用二进制
10. 根据 `binaryKind` 检查入口点是否完整
11. 安装到 `cores/installed/{coreId}/{version}`
12. 更新 `core-registry.fcr`

### 12.2 运行流程

1. 宿主启动时即使没有任何核心，也应能正常进入产品 shell / 设置页 / 核心管理页。
2. 用户选择媒体文件
3. 核心目录服务按：
   - 文件扩展名
   - 媒体探测器
   - `systemId`
   - 用户偏好
   选出候选核心
4. 如有多个核心，交给用户选择默认项
5. 读取选中核心的 `core-manifest.fcr`
6. 根据 `binaryKind` 走不同加载端口：
   - `managed-dotnet` -> Managed loader
   - `native-cabi` -> Native loader
7. 创建 `IEmulatorCoreSession`
8. 将 session 交给宿主会话
9. UI、后端、串流、时间线、调试均通过统一接口接入

注意：

1. `Core Workbench` 的加载流程应与这里完全一致，只是会话后的 UI / 调试工作流不同。
2. 主程序与 Workbench 的差异只能停留在产品壳层，不能落到 manifest、loader、package service 或 session contract 层。

### 12.3 更新流程

1. 下载新版本核心包
2. 并行安装到新版本目录
3. 更新 `core-registry.fcr`
4. 保留旧版本，直到没有活动会话
5. 用户确认后执行旧版本清理

### 12.4 卸载流程

1. 检查是否存在活动会话
2. 如无活动会话，移除版本目录
3. 更新 `core-registry.fcr`
4. 可选择保留 `core-config.fcr`

### 12.5 核心仓库索引、信任根与撤销机制

如果未来要支持“下载核心”，则不能只停留在“对单个 manifest 验签”。

还需要三类附加文档：

1. `core-index.fcr`
   - 核心仓库索引
   - 列出可下载核心、版本、平台包、摘要、发布日期
2. `trusted-publishers.fcr`
   - 宿主内置信任根或用户信任的发布者列表
   - 保存 `publisherId -> public keys`
3. `revocation-list.fcr`
   - 已撤销 key、已撤销 publisher、已下架核心版本列表

推荐校验链：

1. 先校验 `core-index.fcr` 的签名
2. 再检查 `publisherId/keyId` 是否存在于 `trusted-publishers.fcr`
3. 再检查 `publisherId/keyId/coreId/version` 是否命中 `revocation-list.fcr`
4. 再下载包并校验 `core-manifest.fcr`
5. 再按 manifest 中 `files[].sha256` 检查二进制

推荐撤销策略：

1. key 被撤销：
   - 阻止新安装
   - 已安装核心标记为 `untrusted`
2. 某核心版本被撤销：
   - 阻止启动
   - 提示用户升级或切换版本
3. 某发布者被撤销：
   - 整个发布者名下核心进入隔离状态

离线包场景建议：

1. 默认仍校验 manifest 签名
2. 若发布者不在信任根中，由用户显式执行“信任发布者”
3. `trusted-publishers.fcr` 必须记录该动作的来源与时间戳

结论：

1. `signature` 字段只解决单包真实性
2. `trusted-publishers.fcr` 与 `revocation-list.fcr` 才能解决持续信任管理

## 13. 宿主模块改造矩阵

| 模块区域 | 当前文件 | 当前问题 | 目标改造 |
|---|---|---|---|
| 会话运行 | `GameWindowViewModel.Session.cs` | 直接操作 `_nes` | 改为 `IEmulatorCoreSession` |
| 回溯/分支 | `GameWindowViewModel.Timeline.cs` | 依赖 `Ppu.Frame`、`CpuCycles`、`FrameSnapshot` | 改为 `ITimeTravelService` + checkpoint |
| 分支画廊 | `BranchGalleryViewModel.cs` | 依赖 `NesConsole`、`StateSnapshotData` | 改为只依赖时间线服务与缩略图数据 |
| 调试窗口 | `DebugViewModel.cs` | 依赖 `Bus.Read/Write` 与 6502/NES 语义 | 改为 `ICoreDebugSurface` |
| 串流广播 | `SessionStreamBroadcaster.cs` | 订阅原始 `FrameReady` / `AudioChunkReady` | 改为 `VideoFramePacket` / `AudioPacket` |
| 运行时适配 | `ArcadeRuntimeContractAdapter.cs` | 通过 `session.ViewModel.NesConsole` 组装 | 改为核心无关会话桥接 |
| 后端桥接与契约 | `IBackendRuntimeBridge.cs`、`Requests.cs`、`NesButtonDto.cs` | 远控与串流契约仍绑定 `NesButtonDto` 与原始数组流 | 改为通用动作 DTO、流 descriptor 与协商模型 |
| 渲染抽象 | `IRenderDataExtractor.cs` | 参数仍为 `Ppu2C02` | 改为 `IRenderStateSnapshotProvider` |
| 渲染管线 | `RenderDataExtractor.cs` | 提取逻辑写死 NES PPU 数据 | 移到 NES capability 适配层 |
| 存储布局 | `AppObjectStorage.cs`、`AppStorageLayoutPolicy.cs` | 没有核心目录与核心命名空间 | 增加 core storage topology |
| ROM 配置 | `RomConfigProfile.cs` | 同时承担本地资源、输入、机器信任 | 演进为 `game-profile.fcr` |
| 系统配置 | `SystemConfigProfile.cs` | 全局配置和运行时强耦合 | 演进为 `machine-config.fcr` |
| 原生依赖加载 | `FFmpegRuntimeBootstrap.cs`、`MacMetalBridge.cs` | 全局固定依赖探测 | 抽象为 `ICoreLoadContext`/`INativeDependencyResolver` |

## 14. 核心侧模块改造矩阵

| 模块区域 | 当前文件 | 当前问题 | 目标改造 |
|---|---|---|---|
| 核心入口 | `NesConsole.cs` | 宿主唯一入口是具体 NES 控制台 | 包装为 `NES managed core` 实现 |
| 组件接口 | `IEmulationComponent.cs` | 只有 `Reset/Clock/Serialize` 粗接口 | 新增核心模块、能力、组件描述模型 |
| 调试状态 | `DebugState.cs` | 直接暴露 6502/PPU 字段 | 改为 capability 返回的调试模型 |
| 状态封装 | `StateSnapshotData.cs` | 固定五段状态 | 改为 `CoreStateBlob` + component chunks |
| 状态序列化 | `StateSnapshotSerializer.cs` | `FCRS` 固定布局 | 改为多系统兼容 envelope |
| 时间线 | `TimelineController.cs` | 强绑定 `NesConsole` | 改为通用时间线服务，NES 通过 adapter 接入 |
| 总线 | `NesBus.cs` | 写死 NES 地址空间 | 留在 NES core 内部，不进入通用宿主接口 |
| CPU/PPU/APU | `Cpu6502.cs`、`Ppu2C02.cs`、`Apu2A03.cs` | 具体硬件实现直接暴露给外层 | 下沉为核心私有实现 |
| 媒体模型 | `ICartridge.cs`、`MapperCartridge.cs` | 把媒体模型写死为 NES 卡带 | 对宿主只暴露 `LoadMedia`，卡带/mapper 私有化 |
| 渲染元信息 | `FrameMetadata.cs` | 包含 nametable、mirroring、scroll 等 NES 语义 | 基础渲染包 + NES 专用 capability |

## 15. 分期重构步骤

### Phase 0：冻结现状与兼容边界

目标：

1. 明确当前 `NES` 行为为基线实现
2. 确认现有 `.fcr`、save-state、timeline、streaming 的兼容策略

交付：

1. 本设计文档
2. 兼容策略表
3. 核心演进的命名约定

### Phase 1：建立抽象层

新增项目建议：

1. `FC-Revolution.Emulation.Abstractions`
2. `FC-Revolution.Emulation.Host`
3. `FC-Revolution.CoreCatalog`

工作项：

1. 定义 `IEmulatorCoreSession`
2. 定义 capability 系统
3. 定义 `CoreStateBlob`
4. 定义 `FcrDocumentEnvelope`
5. 定义 `CoreManifest` / `CoreRegistry` / `CoreConfig`

退出标准：

1. 宿主层可引用抽象层而不引用未来核心实现项目

### Phase 2：把现有 NES 核包装为托管核心模块

新增项目建议：

1. `FC-Revolution.Core.Nes.Managed`

工作项：

1. 将现有 `FC-Revolution.Core` 作为内部实现依赖
2. 新增 `NesManagedCoreModule`
3. 新增 `NesManagedCoreFactory`
4. 新增 `NesManagedCoreSession`
5. 生成第一版 `core-manifest.fcr`

退出标准：

1. 不改 UI 的情况下，宿主可通过抽象层跑起来 `NES managed core`

### Phase 3：宿主改为依赖统一核心接口

工作项：

1. `GameWindowViewModel` 不再持有 `NesConsole`
2. `ArcadeRuntimeContractAdapter` 改为通过 `IEmulatorCoreSession`
3. `SessionStreamBroadcaster` 改为订阅通用帧/音频包
4. `IBackendRuntimeBridge` 与远控契约新增通用动作协议
5. `MainWindow` 与会话启动逻辑支持“核心选择”

退出标准：

1. `NES` 仍能跑
2. 宿主不再直接依赖 `NesConsole`

当前进展（截至 `2026-04-14`）：

1. `GameWindowViewModel`、`MainWindowViewModel` 与预览生成链路已经统一通过 `IEmulatorCoreSession` / capability resolver 消费核心会话，不再在 UI 侧重复持有 `NesConsole` fallback 逻辑。
2. `SystemConfigProfile` 与 `MainWindowViewModel` 已新增 `DefaultCoreId` 持久化，主会话、预览会话和 `StartGameSession` 本地启动链路都能把配置的核心 ID 继续传到 `CoreSessionLaunchRequest` / `StartSessionWithCore(...)`。
3. `DefaultManagedCoreModuleCatalog` 已成为默认 managed core 注册入口，`DefaultEmulatorCoreHost` 不再把唯一模块实例化硬编码在宿主构造路径里；当前不仅支持进程内注册 / 卸载附加模块，还支持以 registration source 方式统一挂接程序集扫描与目录扫描。
4. `DirectoryManagedCoreModuleRegistrationSource` 已落地，`Program` 启动时会同时探测 `current-appdomain`、`AppContext.BaseDirectory/cores/managed`、`{ResourceRootPath}/cores/managed` 与 `SystemConfigProfile.ManagedCoreProbePaths` 中的 managed core DLL，外部核心首次拥有可配置的目录挂载入口。
5. 仓库内现已新增 `FC-Revolution.Core.Sample.Managed` 作为第二个 managed core demo，`FC-Revolution.UI` build 输出会自动把它复制到 `cores/managed`，并已有 focused host test 验证“目录发现 -> CreateSession -> LoadMedia -> RunFrame”闭环。
6. `MainWindowViewModel` 已公开已安装核心清单与默认核心显示名，并通过 `SelectedDefaultCoreManifest` 接到主窗口设置页；默认核心现在已经有可见的 UI 选择入口。设置页同时已补上 managed core 来源目录输入、应用/重载入口与 effective probe directory 摘要，新 DLL 的挂载入口不再只能靠手工改配置再重启摸黑验证。
7. `SessionRemoteControlService` 已把 `claim/release/heartbeat` 的 `portId -> player` 解析收口到单点 helper，并把 `button/input` 新写路径继续下沉到 `IGameSessionService -> GameWindowViewModel.SetRemoteInputState(...)` 通用入口；本轮进一步把 UI 侧 `x/y` alias、bindable action 过滤与 legacy bitmask mirror 改为读取 `InputSchema` 元数据（`CanonicalActionId` / `IsBindable` / `LegacyBitMask`），UI 不再保留 `NesInputAdapter` 私有桥接；当前剩余兼容仍主要停留在 contracts / backend 边界的 `p1/p2` 与 legacy `/buttons` 过渡壳。
8. `ClaimControlRequest`、`ReleaseControlRequest`、`RefreshHeartbeatRequest` 与 backend WebSocket/HTTP/WebPad 入口已进入“`portId` 优先 + legacy `player` 兼容”的双栈过渡期；当前 legacy `player` 解析已经被收口到边界 helper，backend WebSocket lease state、heartbeat 新请求与 UI session runtime 写入主链都已改成 `portId-first`，`BackendContractClient`、HTTP `/buttons` 兼容入口与 WebSocket `button` 事件也已统一在边界层把 `ActionId` 转成 `SetInputStateRequest`，generic button 写路径继续优先走通用 `input` 协议；同时 `ArcadeRuntimeContractAdapter.SetButtonStateAsync(...)` 也已切到 generic-first（`ActionId` 存在时优先转 `SetInputStateRequest`，仅纯 legacy payload 才回落到 `SetButtonState` 兼容壳）；这一轮之后，`SessionRemoteControlService.SetInputState(...)` 已不再把 generic input 限制回 `p1/p2`，WebPad 的 `claim/heartbeat/input/release` 新写路径也已改成 `portId-first`，只在兼容边界保留 `player` fallback。
9. `GameWindowSessionLoopHost` 已落地，`GameWindowViewModel.Session` 中原先直接持有的会话线程创建/停止、逐帧执行节奏与失败回调编排已下沉到独立宿主；此前已补上 `GameWindowDebugWindowWorkflowController`（并由 `GameWindowDebugWindowHost` 承接窗口宿主生命周期）、`GameWindowSaveStateWorkflowController`、`GameWindowDebugWindowOpenController`、`GameWindowSessionFailureHandler` 与 `GameWindowSessionCommandController`，把 debug window 生命周期、quick save/load/pause-resume 命令壳、session failure apply 与 UI 线程 repost/toast apply 全部从 `Session` partial 中收走；本轮再补上 `GameWindowRomLoadHandler`、`GameWindowProfileTrustHandler` 与 `GameWindowDisposeHandler`，把 ROM load success projection、profile trust/re-sign workflow 与 dispose cleanup sequence 继续从 `Session` partial 中下沉。当前 `GameWindowViewModel.Session` 已退回到 handler forward 与 modified-memory apply 薄壳。
10. `BranchGalleryCanvasProjectionController` 已把 `BranchGalleryViewModel` 中原先直接承担的 timeline/mainline/branch/preview marker 画布投影、layout 和 orientation/zoom 坐标换算抽成独立 controller；此前已补上 `BranchGalleryPreviewNodeWorkflowController`、`BranchGalleryCanvasRefreshController`、`BranchGalleryTimelineNavigationExecutionController`、`BranchGalleryExportExecutionController` 与 `BranchGallerySelectionController`，把 preview workflow、canvas refresh plan、timeline navigation execution、导出执行壳以及 selection 展示派生逻辑逐步从 `ViewModel` 中抽离；本轮再补上 `BranchGalleryBranchWorkflowController`、`BranchGallerySelectionEntryController`、`BranchGalleryViewportWorkflowController` 与 `BranchGalleryCanvasApplyController`，把 branch create/delete/rename、seek/select 命令入口、orientation/zoom/time-scale 调整，以及 `RebuildCanvas` 的最终 apply 都继续下沉。当前 `BranchGalleryViewModel` 已退回到命令入口、展示投影与最终 selection apply 薄壳。
11. `MainWindowActiveInputRuntimeController` 已把 `MainWindowViewModel.InputAndShortcuts` 中的 `_pressedKeys`、turbo pulse、desired action 计算、transition 生成与 `ICoreInputStateWriter` 写回下沉到独立运行时 controller；此前已补上 `MainWindowActiveInputWorkflowController` 与 `MainWindowInputKeyboardWorkflowController`，把 active input ROM 路径解析、refresh/apply 决策、turbo pulse 编排、legacy mirror 生成，以及 `OnKeyDown/OnKeyUp/ShouldHandleKey` 的键盘工作流/动作分发继续从 partial 中抽离；本轮再补上 `MainWindowInputBindingWorkflowController`，并把主窗口 / 游戏窗口输入绑定解析、extra binding 选项、remote alias 归一化与 legacy replay mask mirror 统一改为吃 `CoreInputBindingSchema`，让 UI 生产代码彻底脱离 `NesInputAdapter + byte mask` 真相来源。主窗口输入 partial 已退回到输入绑定命令、active input apply 与最终 legacy mirror 同步薄壳；按 `FC-Revolution.UI / LAN / 预览治理任务清单` 的口径，`1.5` 已可标记完成。
12. `MainWindowManagedCoreCatalogController` 与 `MainWindowManagedCoreInstallController` 已补齐本地 managed core DLL 的最小产品闭环：设置页现在可以直接“导入 DLL -> 刷新默认核心列表 -> 展示选中核心来源/路径/是否可卸载 -> 卸载所选核心”，当被卸载的是当前默认核心时，`DefaultCoreId` 会自动回退到 fallback core 并持久化。为避免同程序集身份在 default load context 下被折叠后丢失真实安装来源，catalog 展示侧已改用独立 inspection `AssemblyLoadContext` 按 DLL 文件路径读取 manifest，安装目录来源与可卸载判定已可稳定追踪。
13. `CorePreviewFrameCaptureService` 已落地到 `FC-Revolution.Emulation.Host`，把 `MainWindowPreviewGenerationController` 中原先直接承担的 `IEmulatorCoreSession` 创建、`LoadMedia`、离线 `RunFrame`、`VideoFrameReady` 捕帧、节流与结构化进度汇报下沉到共享 Host Runtime；主程序预览控制器现在主要只保留缩放、ffmpeg 编码和 legacy preview 文件读写。这样主程序与未来 `Core Workbench`/CLI checker 已开始共用同一套 headless preview session driver。
14. `TimelineStoragePaths` 已从 `FC-Revolution.Core.FC/Timeline/Persistence` 下沉到 [FC-Revolution.Storage](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Storage/TimelineStoragePaths.cs)，Core 旧 namespace 仅保留 forwarding shim；`FC-Revolution.FC.LegacyAdapters` 里的纯路径 wrapper 已删除，UI 本地 `LegacyTimelineStorageAdapter` 也已直接转到通用 `Storage` helper。这样 timeline 的 ROM hash / branch path / export path / manifest write-time 这层纯存储能力不再挂在 FC core namespace 下。
15. replay log 的纯文件格式与 snapshot 基准帧读取也已从 FC/NES 边界下沉到 [FrameInputRecord.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Storage/FrameInputRecord.cs)、[ReplayLogWriter.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Storage/ReplayLogWriter.cs)、[ReplayLogReader.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Storage/ReplayLogReader.cs) 与 [StateSnapshotFrameReader.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Storage/StateSnapshotFrameReader.cs)。`TimelineInputLogWriter` 与 `TimelineVideoExporter.BuildReplayPlan(...)` 已直接使用通用 helper，`NesReplayLogWriter` 这层空壳已删除；当前剩余的 NES 专用部分主要只剩 replay 渲染执行本身。
16. timeline repository 的 bridge DTO / contract 也已抽成 [TimelineBridgeContracts.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Abstractions/TimelineBridgeContracts.cs)，`LegacyTimelineRepositoryAdapter` 现在实现 `ITimelineRepositoryBridge`，`LegacyTimelineManifestHandle` 实现 `ITimelineManifestHandle`；UI 本地 `GameWindowTimelinePersistenceController`、`GameWindowPreviewNodeFactory` 与 `LegacyTimelineSessionAdapter` 已改成只依赖通用 `TimelinePreviewEntry` / `ITimelineManifestHandle` / `ITimelineRepositoryBridge`。这样 UI 侧已经不再直接吃 `LegacyTimelinePreviewEntry` / `LegacyTimelineManifestHandle` 这类 FC 命名 bridge DTO。
17. `FC-Revolution.UI` 现已去掉对 `FC-Revolution.FC.LegacyAdapters` 的编译期依赖：timeline repository、replay frame renderer 与 ROM mapper inspector 分别经由 [LegacyFeatureBridgeLoader.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.UI/Infrastructure/LegacyFeatureBridgeLoader.cs) 按 [LegacyFeatureBridgeContracts.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Abstractions/LegacyFeatureBridgeContracts.cs) 反射加载 runtime-only adapter；同时 backend HTTP / WebSocket 入口已允许任意非空 `portId` 透传，只在 legacy `player -> p1/p2` 兼容边界继续做 fallback，`StartGameSession`、LAN runtime 和 `SessionLifecycleService` 也已开始用真实 `InputSchema` 生成启动期的 `inputBindingsByPort`，不再无条件退回 `p1/p2`。
18. `CoreCheckerCli` 中原先用于 `--package` 的隔离安装工作区逻辑已经下沉为共享 [CoreRuntimeWorkspace.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Host/CoreRuntimeWorkspace.cs)。现在 CLI checker 与图形化 `Core Workbench` 会共用同一套 resource-root / probe-dir / isolated-package runtime 入口，而不再各自复制一套临时 package install 流程。
19. 图形化 [FC-Revolution.Core.Workbench.csproj](/Users/pxm/Desktop/Cs/FC/FC-Revolution/tools/FC-Revolution.Core.Workbench/FC-Revolution.Core.Workbench.csproj) 已落地为 MVP：它通过共享 runtime 暴露 resource root、probe directories、`*.fcrcore.zip` 临时工作区、catalog 刷新与 smoke check，不再需要从主程序复制第二套 loader/package/probe-path 逻辑。
20. [FC-Revolution.CoreLoader.Native.csproj](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.CoreLoader.Native/FC-Revolution.CoreLoader.Native.csproj) 已完成首个 `native-cabi` package-first 宿主端口：当前能通过 `NativeLibrary.Load + FCR_GetCoreApi` 完成 ABI version 校验、manifest 读取、最小 session 装载，并由 [CoreCheckerCliTests.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/tests/FC-Revolution.Core.Checker.Tests/CoreCheckerCliTests.cs) 中动态编译的 fake native core 跑通 install/load/media/smoke 闭环。

当前仍未完成：

1. 宿主虽然已经具备“程序集扫描 + 目录探测 + 配置追加 probe path + 本地 DLL 导入/卸载”的 managed core 最小闭环，并已接入一个 demo 级第二 managed core，但还没有远程下载 / 更新 / 版本管理闭环，也还没有 package 级校验与 registry 元数据。
2. timeline 的纯存储路径层、replay log 文件格式层、snapshot base-frame 读取层与 bridge contract 层已经通用化，UI 对 `FC-Revolution.FC.LegacyAdapters` 的编译期依赖也已移除；但 `LegacyFeatureBridgeLoader` 仍通过硬编码类型名加载 `FC-Revolution.FC.LegacyAdapters`，而 `LegacyTimelineRepositoryAdapter` / `LegacyReplayFrameRenderer` / `LegacyRomMapperInspector` 本身仍是 FC/NES 专用实现。也就是说，当前剩下的主要是实现注入与 NES 专用 replay / mapper 能力，而不再是 DTO / file-format 或项目引用层的耦合。
3. 远控 generic button 写路径虽已在 `BackendContractClient`、HTTP `/buttons` 兼容入口与 WebSocket `button` 边界层优先走 `input` 协议，backend WebSocket/lease internals 也已切到 `portId-first`，而且任意非空 `portId` 已可透传到 runtime；但 contracts 仍保留 `RemoteControlPorts` / `p1/p2` 端口命名以及 `SetButtonState` / `/buttons` compatibility shell，WebPad 与会话摘要/UI 状态也仍然保留 `Player1/Player2` 双槽位假设，尚未升级为真正的系统无关动作协议。
4. 主窗口虽然已经有默认核心选择、来源目录管理/重载、本地 DLL 导入/卸载与来源摘要 UI，但还缺远程下载 / 更新 / 版本管理闭环以及更完整的 manifest 展示，当前仍停留在基础挂载能力。
5. `Core Workbench` 与 `native-cabi` 都已经进入 MVP 阶段，但 `Core Workbench` 仍未接入 preview/export/debug 等更完整的核心开发工作流，而 native loader 也还没有 loose probe-dir 发现、 richer input/video capability 映射与外部核心仓库样板。

### Phase 4：调试、时间线、渲染能力下沉为 capability

状态：

1. 进行中（截至 `2026-04-08`）

工作项：

1. `DebugViewModel` 改为 `ICoreDebugSurface`
2. 时间线逻辑改为 `ITimeTravelService`
3. 渲染抽象改为核心快照 provider
4. `NES` 相关细节转移到 `system-nes-* capability`

退出标准：

1. 通用 UI 不再直接碰 `Ppu2C02`、`Bus.Read/Write`、`DebugState`

当前已完成：

1. `DebugViewModel` 已通过 `ICoreDebugSurface` 获取 `CoreDebugState`，UI 不再直接依赖 legacy `FCRevolution.Core.Debug.DebugState`。
2. `GameWindowViewModel.Timeline`、`MainWindowViewModel`、`BranchGalleryViewModel` 已通过 `ITimeTravelService` 消费 `CoreTimelineSnapshot` / `CoreBranchPoint`。
3. 时间线导出与快速存读档主路径已切到 `CoreStateBlob`，不再要求 UI 直接从 `FrameSnapshot` 组装存档 envelope。
4. `RenderDataExtractor` 已支持从核心 capability 获取渲染快照，通用 UI 不再硬性依赖 `Ppu2C02` 入口。

当前仍未完成：

1. 时间线仓储和分支持久化内部仍使用 `FrameSnapshot` / `BranchPoint`，只是已被限制在 repository / adapter 边界内。
2. `CoreDebugState` 虽然已把 UI 从 `DebugState` 解耦出来，但字段形状仍然明显偏向 `NES/6502/PPU`。
3. `ICoreDebugSurface` 之外的更高层调试模型仍未抽象为真正系统无关的 panel / region / disassembly provider。

### Phase 5：FCR 文档家族与存储布局升级

工作项：

1. 定义新 envelope
2. 引入：
   - `core-manifest.fcr`
   - `core-config.fcr`
   - `core-registry.fcr`
   - `core-index.fcr`
   - `trusted-publishers.fcr`
   - `revocation-list.fcr`
   - `machine-config.fcr`
   - `game-profile.fcr`
3. 做旧版 `system.fcr` 与 ROM profile `.fcr` 的读取兼容与迁移
4. 增加核心安装目录、缓存目录、包目录

退出标准：

1. 核心注册与配置具备独立存储命名空间

### Phase 6：引入 managed 核心加载器

工作项：

1. 用 `AssemblyLoadContext` 做核心加载
2. 完成 manifest 校验与托管入口发现
3. 支持同核心多版本安装

退出标准：

1. `NES managed core` 通过安装目录而不是源码硬连方式启动

当前进展（截至 `2026-04-09`）：

1. `DefaultManagedCoreModuleCatalog` 已支持 registration source，managed core 的发现入口不再只靠硬编码模块列表。
2. `DirectoryManagedCoreModuleRegistrationSource` 已支持从目录递归枚举 `*.dll`，并优先复用当前进程已加载程序集，避免简单重复装载导致的失败。
3. UI 启动阶段已默认探测以下 managed core 目录：
   - `AppContext.BaseDirectory/cores/managed`
   - `{ResourceRootPath}/cores/managed`
   - `SystemConfigProfile.ManagedCoreProbePaths`
4. 设置页已新增 managed core 来源目录输入、应用/重载入口、effective probe directory 摘要、本地 DLL 导入/卸载按钮与选中核心来源摘要，`GameSessionRegistry` 也已改为新建会话时按当前配置重建 core host，当前已形成“配置追加目录 / 导入本地 DLL -> 重新加载默认核心列表 -> 后续新会话生效 -> 卸载当前 core 时自动回退默认核心”的最小产品闭环。
5. 为避免同 identity managed assembly 在 default load context 下丢失真实来源路径，managed core catalog 展示侧已改为使用独立 inspection `AssemblyLoadContext` 按 DLL 文件路径读取 manifest；安装目录来源、程序集路径展示与可卸载判定不再依赖 `type.Assembly.Location` 的不稳定推断。

当前仍未完成：

1. 还没有独立 `AssemblyLoadContext` 隔离与卸载策略，当前仍使用 default load context 做最小可行落地。
2. 还没有核心 manifest 校验、安装目录版本拓扑、远程 registry / package 元数据与更新策略。
3. `NES managed core` 目前仍以内置项目引用方式参与构建，尚未真正迁移为“只通过安装目录加载”的分发形态。

### Phase 6A：抽出共享 Host Runtime 并支持零核心启动

工作项：

1. 把目录探测、程序集检查、manifest inspection 从 UI 控制器下沉到共享 Host Runtime 服务
2. 把基于 `IEmulatorCoreSession` 的 headless 预览驱动抽到共享服务，避免 `Core Workbench` 复制一套
3. 让 `EmulatorCoreHost` 支持 empty catalog
4. 去掉 UI / Host 对具体核心项目的直接 `ProjectReference`
5. 把 bundled core bootstrap 降级为发行层可选预装策略

退出标准：

1. 主程序在没有任何核心时仍可正常启动到空态
2. 主程序与未来 `Core Workbench` 已能共用同一套核心发现、probe-path、会话创建与预览驱动服务
3. `UI/Host` 编译时不再直接依赖具体 NES 核心项目

### Phase 6B：Core Workbench 与独立核心仓库试点

工作项：

1. 新建图形化 `FC-Revolution.CoreWorkbench`
2. 新建 CLI checker，负责 manifest / 包结构 / 最小加载闭环检查
3. 选一个参考核心做主仓库外置试点，验证独立 build / test / pack / install / run
4. 让主程序和 `Core Workbench` 都能加载同一个 `probe path` 或 `*.fcrcore.zip`

退出标准：

1. 外部核心仓库产物无需并入主程序仓库，也能被主程序与 `Core Workbench` 加载
2. 不存在测试工具专用的第二套 loader / package / probe-path 逻辑
3. 核心作者可在主程序仓库之外完成核心开发、测试与打包

当前进展（截至 `2026-04-17`）：

1. 图形化 `FC-Revolution.Core.Workbench` MVP 已存在，并已加入 solution。
2. `CoreRuntimeWorkspace` 已把 `--package` 临时安装工作区从 CLI 下沉到共享 Host Runtime，因此 `Core Workbench` 与 CLI checker 已共用同一套 package/probe/resource-root 逻辑。
3. 当前未完成的部分是 preview/export/debug workflow 与外部核心仓库试点。

### Phase 7：引入 native C ABI 加载器

工作项：

1. 定义 `fcr_core_api.h`
2. 实现 `FC-Revolution.CoreLoader.Native`
3. 增加 `binaryKind = native-cabi` 路由
4. 完成 ABI version 校验和本地库装载上下文

退出标准：

1. 宿主能加载一个最小 native demo core

当前进展（截至 `2026-04-17`）：

1. `FC-Revolution.CoreLoader.Native` 已落地。
2. `native-cabi` 已接入 internal loader registry，并支持 package-first `install -> load -> create session -> load media -> run frame -> capture state` 的最小闭环。
3. 当前剩余的是 loose probe-dir native discovery、 richer capability bridge、跨平台/跨仓库样板与更完整的 native ABI 文档化。

### Phase 8：核心下载与安装服务

工作项：

1. 定义核心仓库索引
2. 实现下载器
3. 实现哈希校验
4. 实现签名校验
5. 实现发布者信任根与撤销列表
6. 实现安装、更新、卸载 UI

退出标准：

1. 用户可下载并安装核心

### Phase 9：接入第二个系统核心

建议优先级：

1. `GB` 或简单 `8-bit` 系统
2. 再上 `SFC`

原因：

1. 先用第二系统验证抽象边界
2. 避免直接用 `SFC` 的复杂度冲垮抽象设计

退出标准：

1. 同一宿主能运行至少两个不同系统核心

## 16. 与当前仓库的具体落点建议

### 16.1 新增项目建议

1. `src/FC-Revolution.Emulation.Abstractions`
2. `src/FC-Revolution.Emulation.Host`
3. `src/FC-Revolution.CoreCatalog`
4. `src/FC-Revolution.CoreLoader.Managed`
5. `src/FC-Revolution.CoreLoader.Native`
6. `src/FC-Revolution.CoreWorkbench`
7. `tools/FC-Revolution.Core.Checker`
8. `src/FC-Revolution.Core.Nes.Managed`
9. `src/FC-Revolution.Core.Native.Abstractions`

### 16.2 当前项目演进建议

1. `FC-Revolution.Core`
   - 短期保留
   - 中期变成 `NES` 私有实现依赖
2. `FC-Revolution.Rendering.Abstractions`
   - 改为真正系统无关的渲染抽象
3. `FC-Revolution.Backend.Abstractions`
   - 将会话/串流协议从 `NES` 语义中抽离
4. `FC-Revolution.Storage`
   - 增加核心目录与注册表支持
5. `FC-Revolution.UI`
   - 会话、调试、时间线、预览全部改为 capability 驱动
   - 从中移出核心检查、目录探测、headless 预览驱动等可复用逻辑
6. `FC-Revolution.Emulation.Host`
   - 继续收口为主程序与 `Core Workbench` 共用的 Shared Host Runtime
7. 外部核心仓库
   - 逐步把具体模拟器核心迁出主程序仓库
   - 每个仓库独立承担 build / test / pack / publish

## 17. 兼容策略

### 17.1 现有 NES 用户兼容目标

1. 旧 ROM 正常运行
2. 旧 `system.fcr` 自动迁移
3. 旧 ROM profile `.fcr` 自动迁移
4. 旧 timeline 存档可继续读取，必要时以只读兼容方式迁移

### 17.2 兼容优先级

1. `NES 运行兼容`
2. `存档兼容`
3. `用户配置兼容`
4. `预览与时间线资源兼容`

### 17.3 不建议承诺的兼容项

1. 新宿主加载旧未来核心的无限期兼容
2. 不同核心之间互相兼容的存档状态

## 18. 风险与关键决策

### 18.1 高风险点

1. 时间线抽象是否足够通用
2. 调试 capability 是否会被设计得过窄或过宽
3. 渲染抽象是否会重新掉回 NES 语义
4. 原生核心的依赖冲突与卸载问题
5. `SFC` 这类复杂系统会暴露更多抽象缺口
6. 如果主程序与 `Core Workbench` 演化出两套加载逻辑，后续所有核心接入都会出现双维护风险
7. 如果外部核心仓库缺少统一 pack / validate / smoke test 流程，所谓“独立核心”会退化为难以集成的散装 DLL

### 18.2 关键决策

1. `binaryKind` 与 `sourceLanguage` 必须分离
2. 核心 manifest 必须是独立 `.fcr` 文档
3. 当前 `NES` 核心优先作为第一个 managed plugin，而不是直接原地大改
4. capability 机制必须早于第二个系统核心落地
5. 先完成 managed plugin 路径，再接 native 路径
6. `FC-Revolution.Emulation.Host` 必须收口为主程序与 `Core Workbench` 共用的 Shared Host Runtime
7. bundled core 只允许作为发行策略，不能再作为宿主启动前提
8. 至少要用一个外部核心仓库试点验证独立 build / test / pack / install / run

## 19. 验证策略

### 19.1 架构级验证

1. `NES managed core` 能通过新 loader 启动
2. 宿主无须直接引用 `NesConsole`
3. 核心 manifest 校验与注册表更新可独立测试
4. 存档与时间线使用 `CoreStateBlob` 后仍能驱动 `NES`
5. 主程序在零核心状态下仍可启动到空态
6. `Core Workbench` 与主程序加载同一核心包时，必须走同一套 loader / package / probe-path 路径
7. 至少一个外部核心仓库产物可在不改主程序源码的情况下完成安装与加载

### 19.2 自动化测试建议

1. `CoreManifestParserTests`
2. `CoreRegistryTests`
3. `ManagedCoreLoaderTests`
4. `NativeCoreLoaderAbiTests`
5. `GameProfileMigrationTests`
6. `MachineConfigMigrationTests`
7. `StateCompatibilityPolicyTests`
8. `CoreFeedSignatureTests`
9. `CoreRevocationListTests`
10. `TimeTravelServiceCompatibilityTests`
11. `CapabilityDiscoveryTests`
12. `InputSchemaProjectionTests`
13. `RemoteControlContractV2Tests`
14. `BackendStreamPacketFormatTests`
15. `ZeroCoreStartupTests`
16. `ManagedCoreInspectionServiceTests`
17. `CoreWorkbenchSharedRuntimeParityTests`
18. `ExternalCorePackageSmokeTests`

### 19.3 当前阶段验证快照（2026-04-08）

本轮 capability 下沉与时间线抽象切换后，已完成的定向验证包括：

1. `dotnet build FC-Revolution.slnx` 通过。
2. `GameWindowRewindSequencePlannerTests` 与 `GameWindowPreviewNodeFactoryTests` 通过。
3. `MainWindow*` 与 `TimelineVideoExporterTests` 的聚焦回归通过。

当前已知验证缺口：

1. `GameWindowViewModelTimelineTests` 中涉及 branch load / rewind 的两条用例，在过滤执行时仍会触发 Avalonia 测试线程问题：
   - `BranchGalleryRewind_UpdatesTemporalResetReason_ToTimelineJump`
   - `BranchGalleryLoadBranch_UpdatesTemporalResetReason_ToTimelineJump`
2. 该问题当前更像测试基础设施的 UI 线程调度问题，而不是本轮 capability 边界改造直接引入的业务回归，但仍需单独收口。

### 19.4 手工验证建议

1. 在零核心状态启动主程序，确认可进入空态与核心管理入口
2. 安装 `NES managed core`
3. 启动 ROM
4. 读档/存档
5. 回溯
6. 打开调试窗口
7. 远程串流
8. 更新核心版本
9. 切换默认核心
10. 用 `Core Workbench` 加载同一核心包，确认 manifest / capability / smoke test 与主程序一致

## 20. 最终建议

结论：

1. 当前架构适合演进为 `宿主固定 + 核心可插拔`。
2. 当前 `C#` 依然适合作为 `NES` 核心实现语言。
3. 未来其他系统核心完全可以使用 `C/C++`，但必须通过统一的 `native-cabi` 端口接入。
4. 运行时“头文件”建议采用 `core-manifest.fcr`，并与开发时 `fcr_core_api.h` 明确分工。
5. 当前 `.fcr` 设计值得保留，但必须提升为 `FCR 文档家族`，而不是继续复用现有 profile 类型承载所有职责。
6. 主程序和 `Core Workbench` 必须共用同一套 Shared Host Runtime。
7. 真正的独立核心不止是代码解耦，还包括外部仓库、独立测试、独立打包与零核心启动。

推荐执行顺序：

1. 先建抽象层
2. 再把 `NES` 包成第一个 managed plugin
3. 再让宿主只认统一核心接口
4. 再把共享 Host Runtime 从 UI 中收口出来，并支持零核心启动
5. 再落 `Core Workbench` 与外部核心仓库试点
6. 再升级 FCR 文档家族与核心存储
7. 最后接 native loader 与第二个系统核心

这条路径的优点是：

1. 风险可控
2. 可以持续保持现有 `NES` 可运行
3. 可以逐步验证抽象质量
4. 能尽早验证“共享运行时 + 独立核心仓库 + Workbench”这条真正影响长期演化的主轴
5. 不会因为直接上 `SFC/C++` 而把仓库拖入一次性重写
