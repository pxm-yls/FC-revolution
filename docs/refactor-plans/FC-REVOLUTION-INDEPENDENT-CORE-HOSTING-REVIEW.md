# FC-Revolution 独立核心宿主评审

本文档用于对照当前仓库实现，评审下面四个核心判断是否成立，以及它们应该如何进一步落地：

1. 模拟器核心是否应通过中间层与 UI / Host / Backend 对接
2. 未来是否应同时支持 `C#` 与 `C++` 两类核心
3. 核心是否应能独立打包，并且宿主在“没有任何核心”的情况下仍可正常启动
4. 核心是否应逐步移出主程序仓库，并通过共享运行时接入主程序与独立测试工具

结论先行：

- 你的方向整体是对的。
- 当前仓库已经做出了一部分中间层和 package-first 形态，但还没有真正走到“核心只是挂件”。
- 当前最大的剩余差距已经从“宿主是否还能启动”转移到“legacy adapter 仍是显式 FC/NES provider 兼容层、package-first 虽已具备内部 `binaryKind` loader dispatch 且 package manifest/registry 已通用化为 `entryPath/activationType`，并新增了 `native-cabi` package-first loader + 最小 smoke test，但 native 路线仍缺 loose probe-dir 发现、更丰富的 ABI/capability 映射与外部仓库验证；同时 `Core Workbench` MVP 虽已落地，独立核心工具链与外部核心仓库试点仍未完成”这三件事。
- 本轮之后，`FC-Revolution.UI` 已去掉对 `FC-Revolution.FC.LegacyAdapters` 的编译期程序集依赖，也不再在 UI build/publish 输出中直接复制 `FC-Revolution.FC.LegacyAdapters` 或 `FC-Revolution.Core.Sample.Managed`；legacy bridge 改为可降级的 runtime 兼容层，不再把 FC provider 当作主程序输出物前提，并且 bridge loader 已从“单个 provider 包办全部能力”推进到“按 capability 独立解析 provider，同时兼容旧的聚合 provider”。与此同时，backend WebSocket / heartbeat / claim-release 入口、WebPad 端口选择、`GameSessionSummaryDto`、`MainWindow` 输入绑定链以及 `GameWindow` 本地/远控输入链都已经收敛为 `portId` / `actionId` 驱动，公开 contracts 不再暴露 `Player`。系统/ROM 配置主字段也已切到 `PortInputOverrides`，开发核心探测配置主字段已切到 `CoreProbePaths`；旧的 `ManagedCoreProbePaths`、`InputOverrides`、`PlayerInputOverrides` 与 `ExtraInputBindingProfile.Player` 已不再作为活动模型字段写回磁盘，而是退化为 JSON 读阶段兼容迁移。`CoreInputBindingSchema` 的 player-based 公共辅助入口，以及 `MainWindow/GameWindow` 中只被测试消费的输入 mask helper 面也已清理；当前剩余输入兼容残留主要集中在回放/时间线仍使用的 byte-mask bridge，以及已经被收口到专用 controller 的回放输入镜像。
- 此外，UI 当前对游戏介质文件的发现/导入已开始从 `CoreManifest.SupportedMediaFilePatterns` 聚合模式，不再把库扫描与文件选择器完全硬编码为 `*.nes`；这把“新增核心时 UI 至少不必再改一轮后缀判断”这件事前移到了 manifest 元数据层。
- 渲染公共抽象也已经从 `nametable/patternTable/OAM/mirroring` 这类 NES/PPU 术语，收敛为 `backgroundPlane/tileGraphics/spriteBytes/backgroundPlaneLayout` 等 capability 语义；NES 专有 `PpuRenderStateSnapshot` 与 `MirroringMode` 现在只停留在 NES adapter 内部映射，不再直接泄漏到通用渲染抽象层。
- `FC-Revolution.Storage` 的通用 `FrameInputRecord` 也已从 player/button mask 模型切到 `ActionsByPort`、`GetPressedActions(portId)` 与 `IsActionPressed(portId, actionId)`；NES 顺序化兼容只停留在 FC core / adapter 侧。

## 1. 当前判断总览

| 主题 | 你的想法 | 当前仓库状态 | 结论 |
| --- | --- | --- | --- |
| 中间层 | 核心应通过中间层与宿主交换数据 | 已有中间层，输入/远控主链已 `portId` 化，但 legacy bridge/provider 与更复杂介质模型仍有缺口 | 方向正确，需要继续强化 |
| C# / C++ 双核心 | 未来会同时存在两类实现 | 方案已考虑，代码已落地 managed 与 package-first `native-cabi` MVP loader | 方向正确，但 native 仍需继续补完 |
| 核心独立打包 | 核心应可独立打包为 DLL/组件 | managed package / install / export / uninstall / zero-core 启动已支持；UI 对 FC adapter 的编译期程序集依赖已移除，但运行时仍保留显式 FC adapter provider 包 | 方向正确，运行时前提已基本独立，最后的实现 provider 边界还需继续收口 |
| 外部核心仓库 + 测试工具 | 核心应可独立设计、独立测试、独立打包，并由独立工具验证 | 已有共享运行时、CLI checker、`CoreRuntimeWorkspace` 与图形化 `Core Workbench` MVP，但外部核心仓库试点与更完整的工具工作流仍未完成 | 方向正确，应继续收口为“共享 Host Runtime + CLI checker + Core Workbench” |

## 2. 关于“中间层”的判断

### 2.1 当前已经存在的中间层

当前仓库不是“UI 直接连 `NesConsole`”的原始状态了，已经有一条明确的中间层：

- 核心抽象层：
  - [CoreContracts.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Abstractions/CoreContracts.cs)
- 核心发现与实例化：
  - [DefaultManagedCoreModuleCatalog.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Host/DefaultManagedCoreModuleCatalog.cs)
  - [ManagedCoreModuleRegistrationSource.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Host/ManagedCoreModuleRegistrationSource.cs)
  - [EmulatorCoreHost.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Host/EmulatorCoreHost.cs)
- UI/运行时 capability 解析：
  - [CoreSessionCapabilityResolver.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.UI/Infrastructure/CoreSessionCapabilityResolver.cs)

这条链路当前的核心接口是：

- `IEmulatorCoreModule`
- `IEmulatorCoreFactory`
- `IEmulatorCoreSession`
- `CoreManifest`
- `CoreCapabilitySet`
- `IInputSchema`

也就是说，当前“中间层”已经存在，而且已经是宿主的主通道，不再只是设想。这里的宿主主线已经不再直接绑定 `IManagedCoreModule`；当前 runtime/discovery 内部也已经引入按 `binaryKind` 分派的 internal loader registry，package manifest / installed registry 的入口点也已从 `assemblyPath/factoryType` 兼容扩展到通用 `entryPath/activationType`，并允许 `native-cabi` 包先进入安装/目录发现链路；remaining managed-only 残留主要收缩到 loose managed export helper、public 命名和缺失的 native loader port，而不是 UI/Host 主表面。

### 2.2 当前中间层还不够彻底的地方

尽管主通道已经存在，但它还没有完全达到“未来多模拟器都能自然接入”的程度。

主要缺口有：

1. 媒体载入抽象太薄。
当前只有 `CoreMediaLoadRequest(string MediaPath)`，这更像“单文件 ROM 路径”，不够描述：
- BIOS
- 多文件介质
- 光盘
- 多盘切换
- 外部存档/NVRAM 初始化

2. 调试地址模型偏 16-bit 地址空间。
当前 `ICoreDebugSurface` 直接使用 `ushort` 地址，这对 6502/NES 很自然，但对未来更多系统未必合适。

3. 高级渲染元数据仍未真正跨系统统一。
虽然最终帧已经走 `VideoFramePacket`，但 layered/render metadata 这一类高级能力目前仍没有成熟的跨系统 capability 契约。

4. 部分系统专有残留仍存在于核心周边协作链。
当前仓库还有一些文档和测试层面依赖 `FCRevolution.Core.*` 的 NES 语义，这说明“中间层”还没完全把系统特化隔离到底。

### 2.3 对你这个想法的优化建议

你的“中间层”想法建议再精炼成三层，而不是一个大而全的 adapter：

1. 会话主接口层
用于最小运行闭环：
- 加载介质
- 跑帧
- 暂停/恢复
- 音视频输出
- 存档/读档

2. capability 扩展层
用于可选高级能力：
- 输入写入
- 时间线/回溯
- 调试
- 分层渲染
- 反汇编

3. loader / package 层
用于解决“如何发现、安装、装载这个核心”：
- manifest
- package
- registry
- binary kind
- assembly/native loading

这比“只有一个中间层”更稳，因为未来加新核心时，大多数变化会落在 capability 和 loader 层，而不需要频繁改主会话接口。

## 3. 当前中间层是否满足未来新增模拟器的需求

### 3.1 可以满足的部分

如果新核心只是：

- 单文件介质
- 固定输入设备
- 基本音视频
- 基本存档
- 基本时间线

那么当前中间层已经可以承接第一版接入。

`SampleManagedCoreModule` 和 `NesManagedCoreModule` 已经证明了这条路径是可跑通的。

### 3.2 不能自然满足的部分

如果未来新增的是更复杂的系统，当前抽象会很快吃紧，例如：

- 需要 BIOS 的系统
- 多盘/多介质系统
- 更复杂地址空间调试模型
- 鼠标、触控、光枪、模拟摇杆
- 复杂 GPU/VDP/PPU 元数据
- 多音频设备或多声道布局

### 3.3 对新增接口难度的判断

当前新增接口“还不算难”，但前提是继续遵守一个原则：

- 新需求优先加 capability / 新通用模型
- 不要为了图快把新系统类型直接暴露给 UI / Host / Backend

如果坚持这个原则，未来加接口是可控的。

如果一旦放松，重新把 `SfcButton`、`GbLcdState`、`MdVdpState` 放进公共层，中间层会很快退化成“表面抽象 + 实际系统分支”。

## 4. 关于 C# 与 C++ 双核心的判断

### 4.1 当前仓库有没有考虑到

考虑到了，但主要停留在方案和枚举层。

现有证据：

- [CoreContracts.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Abstractions/CoreContracts.cs) 里已经有：
  - `managed-dotnet`
  - `native-cabi`
- [FC-REVOLUTION-PLUGGABLE-CORE-ARCHITECTURE-PLAN.md](/Users/pxm/Desktop/Cs/FC/FC-Revolution/docs/refactor-plans/FC-REVOLUTION-PLUGGABLE-CORE-ARCHITECTURE-PLAN.md) 已明确规划：
  - `FC-Revolution.CoreLoader.Managed`
  - `FC-Revolution.CoreLoader.Native`
  - `NativeLibrary + FCR_GetCoreApi`

### 4.2 当前真正落地到什么程度

当前真正跑通的是 managed core 路线：

- Host 主线已抽到 `IEmulatorCoreModule`
- package-first discovery/runtime 已开始通过 internal loader registry 按 `binaryKind` 分派，但当前唯一已实现的 loader port 仍然是 managed-dotnet
- assembly discovery
- package install/export
- package-first load

当前没有看到真正落地的 native core loader：

- 没有 `src/FC-Revolution.CoreLoader.Native`
- 没有 native runtime bridge
- 没有 native session adapter
- 没有 native core package 安装/加载闭环

也就是说：

- “未来支持 C++ 核心”这个方向是对的
- 但当前代码不能说已经具备 C++ 核心接入能力

### 4.3 建议优化

你的这个判断建议转成更明确的架构目标：

1. 宿主层不关心核心是 `C#` 还是 `C++`
2. 宿主只认：
   - `binaryKind`
   - manifest
   - session contract
   - capability contract
3. `C#` 和 `C++` 的差异只能停留在 loader port

换句话说，不是“支持两种语言”，而是“支持两种装载端口”。

这个表述更准确，也更利于落地。

## 5. 关于“核心应独立打包为 DLL/组件”的判断

### 5.1 当前已经做到的一半

当前 managed core 已经具备比较清晰的独立打包方向：

- [ManagedCorePackageService.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Host/ManagedCorePackageService.cs)
- `core-manifest.fcr`
- `core-registry.fcr`
- install / export / uninstall
- package-first loading

这说明“运行时形态”已经开始接近独立核心包，而不是只能靠 UI 直接 new 某个核心对象。

### 5.2 当前还没达到“真正独立”的地方

你最重要的观察是对的：如果编译时仍把宿主和核心绑在一起，那只是代码分层，不是真正独立。

当前仓库的主要问题有：

1. `Emulation.Host` 已不再直接引用 NES managed core 项目。
现状见 [FC-Revolution.Emulation.Host.csproj](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Host/FC-Revolution.Emulation.Host.csproj)。

2. 历史上的 bundled NES bootstrap 前提已被移除，宿主现在允许零核心启动，`EmulatorCoreHost` 也接受 empty catalog。
现状见 [ManagedCoreRuntime.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Host/ManagedCoreRuntime.cs) 与 [EmulatorCoreHost.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Host/EmulatorCoreHost.cs)。

3. 主窗口构造阶段虽然仍会创建主核心会话，但已能优雅退回 unavailable session，而不是要求必须有 NES bundled core。
现状见 [MainWindowViewModel.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.UI/ViewModels/MainWindowViewModel.cs) 中的 `_coreSession = CreateMainCoreSession();`

4. 这一轮之后，`FC-Revolution.UI` 已不再直接引用 [FC-Revolution.Core.csproj](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Core/FC-Revolution.Core.FC/FC-Revolution.Core.csproj)，也已去掉对 [FC-Revolution.FC.LegacyAdapters.csproj](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.FC.LegacyAdapters/FC-Revolution.FC.LegacyAdapters.csproj) 的编译期程序集引用；并且 UI build/publish 输出也不再直接复制 `FC-Revolution.FC.LegacyAdapters` 或 `FC-Revolution.Core.Sample.Managed`。显式 FC 兼容桥现通过 [LegacyFeatureBridgeLoader.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.UI/Infrastructure/LegacyFeatureBridgeLoader.cs) 与 [LegacyFeatureRuntime.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.UI/Infrastructure/LegacyFeatureRuntime.cs) 以 provider 契约按需发现、并在缺失 provider 时 fail-soft；同时 loader 已支持按 timeline/replay/mapper capability 独立解析 provider，而不是继续要求单个总 provider 包办全部能力。这进一步降低了未来新增系统专用 adapter 时的耦合面，而不是把 FC adapter 继续当作主程序输出物前提。
   另外，timeline storage path 已迁到 [TimelineStoragePaths.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Storage/TimelineStoragePaths.cs) 通用层，replay log 文件格式与快照基准帧读取也已迁到 [ReplayLogWriter.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Storage/ReplayLogWriter.cs)、[ReplayLogReader.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Storage/ReplayLogReader.cs) 与 [StateSnapshotFrameReader.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Storage/StateSnapshotFrameReader.cs)。
   此外，timeline bridge DTO / repository surface 也已经通用化到 [TimelineBridgeContracts.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Abstractions/TimelineBridgeContracts.cs)，ROM mapper 与 replay frame 渲染也已抽成 [LegacyFeatureBridgeContracts.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Abstractions/LegacyFeatureBridgeContracts.cs)；UI 不再直接吃 `LegacyTimelineManifestHandle` / `LegacyTimelinePreviewEntry` 这类 FC 命名 DTO，也不再直接引用 `NesReplayInterop` / `NesRomInspector`。
   同时，系统/ROM 配置与 extra input binding 的旧字段兼容也已经退化成读时迁移：`ManagedCoreProbePaths`、`InputOverrides`、`PlayerInputOverrides` 与 `ExtraInputBindingProfile.Player` 不再回写到新配置文件，`CoreInputBindingSchema` 也不再保留 player-based 公共辅助入口。
   这说明“非核心项目直接编译依赖 `FCRevolution.Core.*` 私有实现”这件事已经继续收口，但 legacy timeline repository 实现、replay 渲染、mapper 检查与桥接加载入口仍然还是 FC/NES 专用逻辑，只是被限制在显式 adapter 包和 runtime bridge loader 后面。

5. 共享运行时已经能承担 package/probe/catalog/session、隔离 package workspace 与最小 smoke test，并且图形化 [FC-Revolution.Core.Workbench.csproj](/Users/pxm/Desktop/Cs/FC/FC-Revolution/tools/FC-Revolution.Core.Workbench/FC-Revolution.Core.Workbench.csproj) 已作为第二个前端落地；当前还没完成的是更完整的 preview/export 工作流与外部核心仓库试点，因此“独立打包”仍主要停留在主仓内验证阶段。
现状见 [ManagedCoreRuntime.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Host/ManagedCoreRuntime.cs)、[CoreRuntimeWorkspace.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Host/CoreRuntimeWorkspace.cs)、[CoreSessionSmokeTester.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Host/CoreSessionSmokeTester.cs) 与 [FC-Revolution.Core.Workbench.csproj](/Users/pxm/Desktop/Cs/FC/FC-Revolution/tools/FC-Revolution.Core.Workbench/FC-Revolution.Core.Workbench.csproj)。

这几件事叠加起来意味着：

- 运行时启动模型已经基本摆脱“NES 是必需基础设施”的前提
- 但旧时间线 repository、mapper 与 replay 渲染链路仍没有摆脱 FC/NES 私有实现，只是已经被收口到显式 adapter 层

### 5.3 对你这个想法的优化建议

你的想法建议再进一步明确成下面的验收标准：

1. 宿主项目不直接 `ProjectReference` 任何具体核心项目
2. 所有核心都通过 package/probe/loader 装入
3. 宿主在零核心状态可以正常启动
4. 零核心状态下 UI 会进入“未安装核心”空态，而不是崩溃
5. “是否随安装包预置某个核心”是发行策略，而不是宿主架构前提

这个区分非常重要：

- “发行包内预装一个 NES core”可以接受
- “宿主架构必须依赖 NES core 才能启动”不应再接受

## 6. 关于“核心移出主仓库、独立测试与 Core Workbench”的判断

### 6.1 当前仓库是否已经有基础

有，而且基础已经不算薄。

当前已经存在几块可直接提升为共享运行时的能力：

1. [ManagedCoreModuleRegistrationSource.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Host/ManagedCoreModuleRegistrationSource.cs)
   - 已具备程序集源、目录源、注册表源三类 managed core 发现入口
   - 已具备按 `coreId` 去重，并通过 internal loader registry 按 `binaryKind + entryPath + moduleType` 分派装载的共享雏形
2. [ManagedCoreRuntime.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Host/ManagedCoreRuntime.cs)
   - 已具备按 DLL 检查 manifest、独立 inspection `AssemblyLoadContext` 读取来源信息、目录枚举、catalog 汇总，以及 internal loader dispatch 等共享能力
   - UI catalog controller 现在主要只负责展示映射，这部分已不再是必须继续从 UI 下沉的主阻塞
3. [CorePreviewFrameCaptureService.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Host/CorePreviewFrameCaptureService.cs)
   - 已把创建 `IEmulatorCoreSession`、`LoadMedia`、离线 `RunFrame`、节流与 `VideoFrameReady` 采样收口到共享 Host Runtime
   - [MainWindowPreviewGenerationController.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.UI/ViewModels/MainWindow/MainWindowPreviewGenerationController.cs) 现在主要只保留缩放、ffmpeg 编码和 legacy 预览文件处理

结论：

- 当前仓库已经具备“共享 Host Runtime”的雏形。
- 但它还没有整理成一个可同时服务主程序和独立测试工具的稳定边界。

### 6.2 当前还没达到“外置核心仓库 + 独立测试工具”的地方

主要差距有：

1. UI 主会话与默认核心列表已经不再依赖编译期 `NesManagedCoreModule`，`FC-Revolution.UI` 也不再直接引用 `FC-Revolution.Core.FC`；当前剩余的是 [FC-Revolution.FC.LegacyAdapters.csproj](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.FC.LegacyAdapters/FC-Revolution.FC.LegacyAdapters.csproj) 这一显式 FC adapter/provider 层仍留在主仓内，虽然桥接发现已不再硬编码 FC 类型名，但 provider 包本身还没有被替换成真正系统无关的能力实现。
2. 目录探测、程序集检查、来源汇总和 headless 预览驱动已经收口到 [ManagedCoreRuntime.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Host/ManagedCoreRuntime.cs) 与 [CorePreviewFrameCaptureService.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Host/CorePreviewFrameCaptureService.cs)，但 ffmpeg 编码、preview asset 管理与 legacy 预览文件迁移仍在 UI。
3. 轻量 CLI checker 与图形化 `Core Workbench` MVP 都已完成，但 preview/export 等更完整的工具工作流仍未并入，外部核心仓库试点也还没有验证独立 build / test / pack / install / run 闭环。
4. `native-cabi` 已不再只是 manifest / binary kind 约定；当前仓库已经有 package-first loader、ABI version 校验与最小 smoke test，但 loose probe-dir 发现、更丰富的 input/video capability 桥接与跨仓库样板仍未完成。

这意味着：

- 现在已经不是“主程序必须内置 NES 才能启动”的状态
- 但仍然还不是“主程序与核心仓库真正解耦，任何核心都可独立开发、独立测试、独立打包”

### 6.3 我的建议

这个方向建议明确成下面三个约束：

1. 核心仓库边界
   - 每个模拟器核心最终应位于自己的仓库或独立源码根
   - 能独立 build、独立 test、独立 pack，而不是跟主程序一同编译才成立
2. 共享运行时边界
   - 主程序与未来 `Core Workbench` 只能共用同一套 Host Runtime / loader / package / probe-path 逻辑
   - 不要复制一套“测试工具专用装载链”
3. 测试工具边界
   - 应有一个图形化 `Core Workbench`
   - 同时建议有一个更轻量的 CLI checker，适合 CI 验证 manifest、包结构、加载与最小 smoke test

换句话说：

- 主程序不是唯一宿主前端
- `Core Workbench` 也只是同一套共享运行时的第二个前端

## 7. 建议的目标形态（摘要）

更严格的目标形态建议定义成下面这样：

```text
Main App UI / Backend / Core Workbench
    只依赖 Emulation.Abstractions + Shared Host Runtime + Loader + Package Catalog

Shared Host Runtime
    统一负责：
    - 核心发现
    - probe-path / package 装载
    - session 创建
    - capability 路由
    - headless 预览/最小 smoke test 驱动

External Core Repo A (managed-dotnet)
    manifest + dll + tests + pack scripts

External Core Repo B (native-cabi)
    manifest + dylib/dll/so + native bridge + tests + pack scripts

App startup
    无核心也能进 UI
    有核心则列出可用核心
    有默认核心才创建会话
```

这比“代码解耦”更严格，但它才真正接近“核心只是挂件”。

更完整的目标拓扑、模块边界和 phase 计划请以
[FC-REVOLUTION-PLUGGABLE-CORE-ARCHITECTURE-PLAN.md](/Users/pxm/Desktop/Cs/FC/FC-Revolution/docs/refactor-plans/FC-REVOLUTION-PLUGGABLE-CORE-ARCHITECTURE-PLAN.md)
为准。

## 8. 推荐的改造顺序（摘要）

这里保留结论性顺序，详细 roadmap 统一放在方案文档中：

### Phase A：把共享运行时边界抽出来（部分完成）

优先做：

1. 把目录探测、程序集检查、manifest inspection 从 UI 收口到共享 Host Runtime
2. 继续把预览生成周边的编码/资产处理与 legacy 预览文件流程从 UI 宿主工作流中与共享运行时边界清晰分层
3. 明确主程序与 `Core Workbench` 共用同一套加载与会话创建逻辑

当前状态：

1. package/probe/catalog/inspection/session 创建已经集中到 [ManagedCoreRuntime.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Host/ManagedCoreRuntime.cs)，并且 probe/package 入口已通过 internal loader registry 收口成按 `binaryKind` 分派的共享内部装载面。
2. 最小 smoke test 驱动已经沉到 [CoreSessionSmokeTester.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Host/CoreSessionSmokeTester.cs)，并由 [CoreCheckerCli.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/tools/FC-Revolution.Core.Checker/CoreCheckerCli.cs) 复用。
3. headless preview driver 也已沉到 [CorePreviewFrameCaptureService.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Host/CorePreviewFrameCaptureService.cs)，而 timeline storage path 也已沉到 [TimelineStoragePaths.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Storage/TimelineStoragePaths.cs) 通用层。
4. 但 preview 编码/legacy 预览文件处理，以及 timeline repository 实现 / replay 渲染 / mapper 适配仍在 UI 或显式 FC adapter provider 包，因此这一阶段还不能算完成。

### Phase B：消除编译时核心依赖并支持零核心启动（大部完成）

优先做：

1. 去掉 `UI/Host -> NES Managed Core` 的直接项目引用
2. 把 bundled core 定位为发行策略，而不是宿主架构前提
3. 让 `EmulatorCoreHost` 支持 empty catalog
4. 让 UI 在零核心状态进入空态，而不是强制创建会话

当前状态：

1. `Emulation.Host` 已不再直接 `ProjectReference` NES managed core，`EmulatorCoreHost` 也支持 empty catalog。
2. bundled NES core 已降级为 bundled package / 发行策略，而不是 Host 架构前提。
3. UI 主会话链路在零核心场景已能退回 unavailable session。
4. `FC-Revolution.UI -> FC-Revolution.Core.FC` 的直接项目引用已经移除，`FC-Revolution.UI -> FCRevolution.Core.*` 的显式 FC 兼容桥也已迁出 `Core.*` 工程树；`MainWindow` 与 `GameWindow` 输入绑定、运行时输入写入、远控入口和回放日志头部也已经切到 `portId` / `actionId` 驱动。但 legacy timeline / replay / mapper 仍通过显式 FC adapter/provider 层接入，因此“完全系统无关的 UI”还没彻底完成。

### Phase C：落地独立测试工具与外置核心仓库试点（已启动，未完成）

优先做：

1. 新建图形化 `Core Workbench`
2. 新建 CLI checker 作为 CI / 打包 smoke test 入口
3. 先把一个参考核心迁移为外部仓库试点，验证独立 build / test / pack / install / run 闭环

当前状态：

1. 轻量 CLI checker 首版已可用，见 [FC-Revolution.Core.Checker.csproj](/Users/pxm/Desktop/Cs/FC/FC-Revolution/tools/FC-Revolution.Core.Checker/FC-Revolution.Core.Checker.csproj) 与 [CoreCheckerCli.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/tools/FC-Revolution.Core.Checker/CoreCheckerCli.cs)。
2. 图形化 `Core Workbench` MVP 已可用，见 [FC-Revolution.Core.Workbench.csproj](/Users/pxm/Desktop/Cs/FC/FC-Revolution/tools/FC-Revolution.Core.Workbench/FC-Revolution.Core.Workbench.csproj) 与 [CoreWorkbenchViewModel.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/tools/FC-Revolution.Core.Workbench/ViewModels/CoreWorkbenchViewModel.cs)；它已通过共享 [CoreRuntimeWorkspace.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Host/CoreRuntimeWorkspace.cs) 复用主程序/CLI 同一套 package/probe/runtime 逻辑。
3. 外部核心仓库试点尚未启动，因此这个阶段仍未完成。

### Phase D：补 native-cabi loader（已启动，MVP 已落地）

优先做：

1. 已新增 [FC-Revolution.CoreLoader.Native.csproj](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.CoreLoader.Native/FC-Revolution.CoreLoader.Native.csproj)。
2. 已实现 package-first native entrypoint 装载、`FCR_GetCoreApi` 解析与 ABI version 校验。
3. 已给 native core 建最小 smoke test，见 [CoreCheckerCliTests.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/tests/FC-Revolution.Core.Checker.Tests/CoreCheckerCliTests.cs)。
4. 当前剩余的是 loose probe-dir native 发现、 richer capability bridge，以及外部核心仓库样板。

## 9. 我的意见汇总

### 9.1 哪些地方与你的想法一致

1. “核心应该通过中间层对接”是正确方向
2. “未来会有两种装载端口，分别承接 managed 与 native 核心”是正确方向
3. “核心必须能独立打包，宿主应允许无核心启动”是正确方向
4. “核心应尽量移出主程序仓库，并有独立测试工具”也是正确方向

### 9.2 哪些地方建议转换表述

建议把：

- “支持 C# / C++ 两种代码核心”

转换为：

- “支持 `managed-dotnet` 与 `native-cabi` 两种 loader port”

建议把：

- “有一个中间层”

转换为：

- “有一套分层中间边界：session 主接口 + capability 扩展 + loader/package 层”

建议把：

- “给测试工具单独做一套加载逻辑”

转换为：

- “主程序与 `Core Workbench` 共用一套共享 Host Runtime，只在前端工作流上分化”

### 9.3 当前最不一致的地方

如果以你的目标为标准，当前最不一致的 5 个点是：

1. `FC-Revolution.UI` 已改为通过显式 FC adapter/provider 层接入 legacy timeline / replay / mapper，并且这层已迁出 `Core.*` 工程树；其中 timeline storage path、replay log 文件格式、snapshot base-frame 读取，以及 timeline bridge DTO / repository surface 已下沉到通用层，bridge loader 也不再硬编码 FC 类型名，但 repository 实现 / replay 渲染 / mapper 这些能力本身仍是 FC/NES 专用实现，尚未进一步抽成真正系统无关的 capability 服务。
2. preview 编码、preview asset 管理与 legacy 预览文件处理仍主要驻留在 UI，Shared Host Runtime 还没完全抽干净。
3. sample managed core 已不再随 `FC-Revolution.UI` 构建直接复制到输出目录，这条历史性耦合已经清除；当前剩余问题不再是“UI 输出是否偷偷内置 sample core”，而是 shared runtime/package/discovery 虽已具备 internal loader dispatch，installed package schema 也已兼容通用 `entryPath/activationType` 并允许 native package 安装/列目录，但 loose export helper、真正的 native loader 与外部核心仓库试点尚未落地。
4. 图形化 `Core Workbench` 已作为 MVP 落地，但仍未扩展到 preview/export 等更完整的核心开发工作流，而且外部核心仓库试点也还没有验证。
5. `native-cabi` 已经成为代码能力，但当前只完成了 package-first 最小 loader/session/smoke 闭环，离完整的通用 native capability 端口还有距离。

## 10. 推荐的下一步

如果要把这个方向继续落地，我建议下一轮优先做的不是“再抽一个接口”，而是：

1. 先继续把 legacy timeline / NES 辅助逻辑从“显式 NES adapter 包”推进到更稳定的 capability / shared runtime 边界；timeline storage path 已经完成通用化，下一步应优先处理 replay/export 与 repository 契约，避免 adapter 层继续膨胀成新的半公共层。
2. 继续把 preview 编码/preview asset 流程与 Shared Host Runtime 的捕帧服务边界拉直，让主程序、CLI checker 与未来 `Core Workbench` 共用同一套会话驱动。
3. 在已经落地的 CLI checker + `Core Workbench` 基础上，补 preview/export 等更完整的 shared-runtime 工具工作流，并拿一个外部核心仓库做独立 build / test / pack / install 试点。
4. 继续把 native loader 从当前 package-first MVP 扩到 loose probe-dir、 richer capability bridge 与跨仓库样板。

原因很简单：

- 只有主程序、CLI checker 和未来 `Core Workbench` 都能共用同一套运行时，才能证明“核心接入方式”已经稳定。
- 只有 UI 彻底不再编译依赖 `FCRevolution.Core.*` 私有实现，才能证明核心真的不再是主程序的一部分。
- 在那之前，哪怕运行时入口已经 package-first，也还只是“主程序中残留部分 NES/FC 私有实现的半独立核心体系”。
