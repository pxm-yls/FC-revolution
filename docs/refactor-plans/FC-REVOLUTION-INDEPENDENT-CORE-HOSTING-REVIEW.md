# FC-Revolution 独立核心宿主评审

本文档用于对照当前仓库实现，评审下面三个核心判断是否成立，以及它们应该如何进一步落地：

1. 模拟器核心是否应通过中间层与 UI / Host / Backend 对接
2. 未来是否应同时支持 `C#` 与 `C++` 两类核心
3. 核心是否应能独立打包，并且宿主在“没有任何核心”的情况下仍可正常启动

结论先行：

- 你的方向整体是对的。
- 当前仓库已经做出了一部分中间层和 package-first 形态，但还没有真正走到“核心只是挂件”。
- 最大的剩余差距不在“代码是否分层”，而在“编译依赖、启动默认假设、native loader 缺位、empty catalog 启动能力”这四件事。

## 1. 当前判断总览

| 主题 | 你的想法 | 当前仓库状态 | 结论 |
| --- | --- | --- | --- |
| 中间层 | 核心应通过中间层与宿主交换数据 | 已有中间层，但仍有 NES 偏置和扩展缺口 | 方向正确，需要继续强化 |
| C# / C++ 双核心 | 未来会同时存在两类实现 | 方案已考虑，代码只落地了 managed | 方向正确，但 native 仍未落地 |
| 核心独立打包 | 核心应可独立打包为 DLL/组件 | managed package 已支持，但宿主仍假定至少有一个 bundled NES core | 方向正确，但启动模型仍未独立 |

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

1. `Emulation.Host` 仍直接引用 NES managed core 项目。
见 [FC-Revolution.Emulation.Host.csproj](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Host/FC-Revolution.Emulation.Host.csproj)。

2. 启动时仍强制 bootstrap bundled NES core。
见 [Program.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.UI/Program.cs) 和 [BundledManagedCoreBootstrapper.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Host/Adapters/Nes/BundledManagedCoreBootstrapper.cs)。

3. `DefaultEmulatorCoreHost.Create()` 仍隐式假设“先保证至少有一个 NES bundled core 已安装”。
见 [EmulatorCoreHost.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Emulation.Host/EmulatorCoreHost.cs)。

4. `EmulatorCoreHost` 本身不允许零核心状态。
构造函数中 `managedModules.Count == 0` 会直接抛异常。

5. 主窗口构造阶段就会创建主核心会话。
见 [MainWindowViewModel.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.UI/ViewModels/MainWindowViewModel.cs) 中的 `_coreSession = CreateMainCoreSession();`

这几件事叠加起来意味着：

- 现在的宿主还不能在“完全没有核心”的情况下优雅启动
- 当前仍然默认把 NES 当作必备基础设施，而不是可选挂载核心

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

## 6. 建议的目标形态

你想要的最终形态，我建议定义成下面这样：

```text
UI / Backend / Host
    只依赖 Emulation.Abstractions + Loader + Package Catalog

Core Package A (C#)
    manifest + dll + adapter

Core Package B (C++)
    manifest + dylib/dll/so + native-cabi bridge

App startup
    无核心也能进 UI
    有核心则列出可用核心
    有默认核心才创建会话
```

这比“代码解耦”更严格，但它才是真正接近“核心只是挂件”。

## 7. 推荐的改造顺序

### Phase A：把中间层补到可扩展

优先做：

1. 扩展媒体载入抽象
2. 审视调试地址模型是否需要泛化
3. 把高级渲染元数据继续下沉为 capability
4. 明确哪些 capability 是跨系统的，哪些只能留在 adapter 层

### Phase B：消除宿主对具体核心的编译时依赖

优先做：

1. 去掉 `Emulation.Host -> NES Managed Core` 的直接项目引用
2. 把 bundled core 变成发行层预置，而不是 host 架构前提
3. 让 `DefaultEmulatorCoreHost` 不再内置 NES bootstrap 假设

### Phase C：支持零核心启动

优先做：

1. `EmulatorCoreHost` 支持 empty catalog
2. `MainWindowViewModel` 不再构造期强制创建主核心会话
3. UI 增加“未安装核心”状态和引导入口
4. 默认核心配置允许为空

### Phase D：引入 native-cabi loader

优先做：

1. 新建 `FC-Revolution.CoreLoader.Native`
2. 实现 manifest 到 native entrypoint 的装载链
3. 给 native core 建最小 smoke test
4. 用一个假的 native core 跑通 install/load/session 闭环

## 8. 我的意见汇总

### 8.1 哪些地方与你的想法一致

1. “核心应该通过中间层对接”是正确方向
2. “未来会有 C# / C++ 两类核心”是正确方向
3. “核心必须能独立打包，宿主应允许无核心启动”是正确方向

### 8.2 哪些地方建议转换表述

建议把：

- “支持 C# / C++ 两种代码核心”

转换为：

- “支持 `managed-dotnet` 与 `native-cabi` 两种 loader port”

建议把：

- “有一个中间层”

转换为：

- “有一套分层中间边界：session 主接口 + capability 扩展 + loader/package 层”

### 8.3 当前最不一致的地方

如果以你的目标为标准，当前最不一致的 4 个点是：

1. 宿主仍直接编译依赖具体 NES core
2. 启动链仍强制 bundled NES bootstrap
3. 宿主当前不接受 empty catalog
4. native-cabi 路线还只是方案，不是代码能力

## 9. 推荐的下一步

如果要把你的想法继续落地成代码，我建议下一轮优先做的不是“再抽一个接口”，而是：

1. 先做“无核心启动”改造设计
2. 再拆掉 `Emulation.Host` 对 NES managed core 的直接项目引用
3. 然后再补 native loader skeleton

原因很简单：

- 只有宿主真正允许“零核心存在”，才能证明核心已经不是宿主的一部分
- 在那之前，哪怕代码接口再漂亮，也还只是“被默认绑定的内置核心”
