# FC-Revolution 独立核心宿主评审

本文档用于对照当前仓库实现，评审下面四个核心判断是否成立，以及它们应该如何进一步落地：

1. 模拟器核心是否应通过中间层与 UI / Host / Backend 对接
2. 未来是否应同时支持 `C#` 与 `C++` 两类核心
3. 核心是否应能独立打包，并且宿主在“没有任何核心”的情况下仍可正常启动
4. 核心是否应逐步移出主程序仓库，并通过共享运行时接入主程序与独立测试工具

结论先行：

- 你的方向整体是对的。
- 当前仓库已经做出了一部分中间层和 package-first 形态，但还没有真正走到“核心只是挂件”。
- 当前最大的剩余差距已经从“宿主是否还能启动”转移到“UI 仍保留少量 FC/NES 编译期适配层、共享运行时最后一段 preview/legacy adapter 边界尚未完全抽干、CLI checker 虽已落地但独立核心工具链仍未完成、native loader 仍未落地、外部核心仓库试点尚未验证”这五件事。
- 本轮之后，UI 本地输入、运行时输入和 NES alias 处理已经改为 `InputSchema` 元数据驱动，`NesInputAdapter` 与 NES 位掩码不再是 UI 生产代码的真相来源；backend WebSocket / heartbeat 内部租约也已切到 `portId-first`，剩余输入残留主要集中在 contracts、HTTP `/buttons` / WebPad 兼容壳与配置命名空间还保留 `p1/p2` / `Player1` / `Player2` 过渡形态。

## 1. 当前判断总览

| 主题 | 你的想法 | 当前仓库状态 | 结论 |
| --- | --- | --- | --- |
| 中间层 | 核心应通过中间层与宿主交换数据 | 已有中间层，但仍有 NES 偏置和扩展缺口 | 方向正确，需要继续强化 |
| C# / C++ 双核心 | 未来会同时存在两类实现 | 方案已考虑，代码只落地了 managed | 方向正确，但 native 仍未落地 |
| 核心独立打包 | 核心应可独立打包为 DLL/组件 | managed package / install / export / uninstall / zero-core 启动已支持，但 UI 仍保留少量 FC/NES 编译期适配层 | 方向正确，运行时前提已基本独立，编译边界还需继续收口 |
| 外部核心仓库 + 测试工具 | 核心应可独立设计、独立测试、独立打包，并由独立工具验证 | 已有共享运行时雏形，CLI checker 首版已可用，但 `Core Workbench`、外部核心仓库试点与预览驱动共享化仍未完成 | 方向正确，应继续收口为“共享 Host Runtime + CLI checker + Core Workbench” |

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

- `IManagedCoreModule`
- `IEmulatorCoreFactory`
- `IEmulatorCoreSession`
- `CoreManifest`
- `CoreCapabilitySet`
- `IInputSchema`

也就是说，当前“中间层”已经存在，而且已经是宿主的主通道，不再只是设想。

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

- `IManagedCoreModule`
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

4. 这一轮之后，`FC-Revolution.UI` 已不再直接引用 [FC-Revolution.Core.csproj](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Core/FC-Revolution.Core.FC/FC-Revolution.Core.csproj)，显式 FC 兼容桥也已迁出 `Core.*` 工程树，现位于 [FC-Revolution.FC.LegacyAdapters.csproj](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.FC.LegacyAdapters/FC-Revolution.FC.LegacyAdapters.csproj)。
   这说明“非核心项目直接编译依赖 `FCRevolution.Core.*` 私有实现”这件事已经继续收口，但 legacy timeline / replay / mapper 仍然还是 FC/NES 专用逻辑，只是被限制在显式 adapter 包后面。

5. 共享运行时已经能承担 package/probe/catalog/session 与最小 smoke test，但图形化 workbench 和外部核心仓库试点还没完成，因此“独立打包”仍主要停留在 managed package 与主仓内验证阶段。
现状见 [ManagedCoreRuntime.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Host/ManagedCoreRuntime.cs) 与 [CoreSessionSmokeTester.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Host/CoreSessionSmokeTester.cs)。

这几件事叠加起来意味着：

- 运行时启动模型已经基本摆脱“NES 是必需基础设施”的前提
- 但旧时间线、mapper 与 replay 工具链仍没有摆脱 FC/NES 私有实现，只是已经被收口到显式 adapter 层

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
   - 已具备按 `coreId` 去重与按 `entryAssemblyPath/factoryType` 实例化的共享装载雏形
2. [ManagedCoreRuntime.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Host/ManagedCoreRuntime.cs)
   - 已具备按 DLL 检查 manifest、独立 inspection `AssemblyLoadContext` 读取来源信息、目录枚举与 catalog 汇总等共享能力
   - UI catalog controller 现在主要只负责展示映射，这部分已不再是必须继续从 UI 下沉的主阻塞
3. [MainWindowPreviewGenerationController.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.UI/ViewModels/MainWindow/MainWindowPreviewGenerationController.cs)
   - 预览生成已经主要依赖 `IEmulatorCoreSession`
   - 这说明离线跑帧、取帧、做 smoke test 的基础并不依赖 NES 专有类型

结论：

- 当前仓库已经具备“共享 Host Runtime”的雏形。
- 但它还没有整理成一个可同时服务主程序和独立测试工具的稳定边界。

### 6.2 当前还没达到“外置核心仓库 + 独立测试工具”的地方

主要差距有：

1. UI 主会话与默认核心列表已经不再依赖编译期 `NesManagedCoreModule`，`FC-Revolution.UI` 也不再直接引用 `FC-Revolution.Core.FC`；当前剩余的是 [FC-Revolution.FC.LegacyAdapters.csproj](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.FC.LegacyAdapters/FC-Revolution.FC.LegacyAdapters.csproj) 这一显式 FC adapter 层仍留在主仓内。
2. 目录探测、程序集检查和来源汇总已经大部分收口到 [ManagedCoreRuntime.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Host/ManagedCoreRuntime.cs)，但 [MainWindowPreviewGenerationController.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.UI/ViewModels/MainWindow/MainWindowPreviewGenerationController.cs) 的 headless 预览驱动仍在 UI。
3. 轻量 CLI checker 首版已完成，但图形化 `Core Workbench` 仍不存在，外部核心仓库试点也还没有验证独立 build / test / pack / install / run 闭环。
4. `native-cabi` 目前仍只是 manifest / binary kind 约定，没有真正的 loader、桥接层和 smoke test。

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
2. 把预览生成里依赖 `IEmulatorCoreSession` 的 headless 驱动部分沉到共享服务
3. 明确主程序与 `Core Workbench` 共用同一套加载与会话创建逻辑

当前状态：

1. package/probe/catalog/inspection/session 创建已经集中到 [ManagedCoreRuntime.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Host/ManagedCoreRuntime.cs)。
2. 最小 smoke test 驱动已经开始沉到 [CoreSessionSmokeTester.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Host/CoreSessionSmokeTester.cs)，并由 [CoreCheckerCli.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/tools/FC-Revolution.Core.Checker/CoreCheckerCli.cs) 复用。
3. 但 preview 生成与 legacy timeline 适配还在 UI，因此这一阶段还不能算完成。

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
4. `FC-Revolution.UI -> FC-Revolution.Core.FC` 的直接项目引用已经移除，`FC-Revolution.UI -> FCRevolution.Core.*` 的显式 FC 兼容桥也已迁出 `Core.*` 工程树；但 legacy timeline / replay / mapper 仍通过显式 FC adapter 层接入，因此“完全系统无关的 UI”还没彻底完成。

### Phase C：落地独立测试工具与外置核心仓库试点（已启动，未完成）

优先做：

1. 新建图形化 `Core Workbench`
2. 新建 CLI checker 作为 CI / 打包 smoke test 入口
3. 先把一个参考核心迁移为外部仓库试点，验证独立 build / test / pack / install / run 闭环

当前状态：

1. 轻量 CLI checker 首版已可用，见 [FC-Revolution.Core.Checker.csproj](/Users/pxm/Desktop/Cs/FC/FC-Revolution/tools/FC-Revolution.Core.Checker/FC-Revolution.Core.Checker.csproj) 与 [CoreCheckerCli.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/tools/FC-Revolution.Core.Checker/CoreCheckerCli.cs)。
2. `Core Workbench` 尚未建立。
3. 外部核心仓库试点尚未启动，因此这个阶段目前只能算刚起步。

### Phase D：补 native-cabi loader（未开始）

优先做：

1. 新建 `FC-Revolution.CoreLoader.Native`
2. 实现 manifest 到 native entrypoint 的装载链
3. 给 native core 建最小 smoke test
4. 用一个假的 native core 跑通 install/load/session 闭环

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

1. `FC-Revolution.UI` 已改为通过显式 FC adapter 层接入 legacy timeline / replay / mapper，并且这层已迁出 `Core.*` 工程树；但这些能力本身仍是 FC/NES 专用实现，尚未进一步抽成真正系统无关的 capability 服务。
2. headless 预览驱动与旧时间线桥接仍主要驻留在 UI，Shared Host Runtime 还没完全抽干净。
3. 图形化 `Core Workbench` 尚未存在，外部核心仓库试点也还没有验证。
4. `native-cabi` 路线还只是方案，不是代码能力。
5. 具体核心项目仍位于主仓内，尚未证明“主仓外独立 build / test / pack”流程。

## 10. 推荐的下一步

如果要把这个方向继续落地，我建议下一轮优先做的不是“再抽一个接口”，而是：

1. 先继续把 legacy timeline / NES 辅助逻辑从“显式 NES adapter 包”推进到更稳定的 capability / shared runtime 边界，避免 adapter 层继续膨胀成新的半公共层。
2. 把 headless 预览驱动继续从 UI 下沉到 Shared Host Runtime，让主程序、CLI checker 与未来 `Core Workbench` 共用同一套服务。
3. 在已经落地的 CLI checker 基础上补图形化 `Core Workbench`，并拿一个外部核心仓库做独立 build / test / pack / install 试点。
4. 最后再补 native loader skeleton。

原因很简单：

- 只有主程序、CLI checker 和未来 `Core Workbench` 都能共用同一套运行时，才能证明“核心接入方式”已经稳定。
- 只有 UI 彻底不再编译依赖 `FCRevolution.Core.*` 私有实现，才能证明核心真的不再是主程序的一部分。
- 在那之前，哪怕运行时入口已经 package-first，也还只是“主程序中残留部分 NES/FC 私有实现的半独立核心体系”。
