# FC-Revolution 当前整体运行流程图

本文档基于当前仓库实现，描述桌面应用默认运行路径，包括：

- 程序启动与主窗口延迟启动
- package-first 的核心发现与会话创建
- 从 ROM 选择到游戏窗口启动
- LAN Arcade 嵌入式后端与远控回流

当前架构下，默认核心加载已经是 package-first：宿主会先确保 bundled core package 已安装，再从 package registry 和 probe path 解析可用核心。bundled NES core 仍是默认核心，但它已经作为一个 bundled package 被挂载，而不是 UI/Host 的硬编码后门。

## 1. 整体运行总览

```mermaid
flowchart TD
    User["用户启动程序"] --> Program["Program.Main"]
    Program --> InitCatalog["InitializeManagedCoreCatalog()"]
    InitCatalog --> Bootstrap["BundledManagedCoreBootstrapper.EnsureBundledCorePackages()"]
    InitCatalog --> HostCatalog["DefaultManagedCoreModuleCatalog + DefaultEmulatorCoreHost"]
    Program --> Avalonia["BuildAvaloniaApp().StartWithClassicDesktopLifetime()"]
    Avalonia --> App["App.OnFrameworkInitializationCompleted()"]
    App --> MainWindow["MainWindow"]
    App --> MainVM["MainWindowViewModel(deferStartupWork: true)"]
    MainWindow --> Opened["MainWindow.OnOpened()"]
    Opened --> Startup["MainWindowViewModel.RunStartupSequenceAsync()"]
    Startup --> RomLibrary["RefreshRomLibrary()"]
    Startup --> PreviewWarmup["WarmPreviewFramesAsync()"]
    Startup --> LanState["ApplyLanArcadeServerStateAsync()"]

    MainVM --> MainCore["主窗口长期 CoreSession + EmuThreadLoop"]
    MainVM --> SessionSvc["GameSessionService"]
    SessionSvc --> SessionRegistry["GameSessionRegistry"]
    SessionRegistry --> GameVM["GameWindowViewModel"]
    GameVM --> GameWindow["GameWindow"]
    GameVM --> SessionLoop["GameWindowSessionLoopHost"]

    MainVM --> RuntimeBundle["MainWindowRuntimeDependencyBundle"]
    RuntimeBundle --> ArcadeAdapter["ArcadeRuntimeContractAdapter"]
    RuntimeBundle --> LanService["LanArcadeService"]
    LanService --> BackendHost["BackendHostService"]
    Browser["浏览器 / 手机端"] --> BackendHost
    BackendHost --> Bridge["EmbeddedBackendRuntimeBridge"]
    Bridge --> ArcadeAdapter
    ArcadeAdapter --> SessionSvc
```

## 2. 程序启动与主窗口延迟启动

```mermaid
sequenceDiagram
    participant User as 用户
    participant Program as Program.Main
    participant Bootstrap as BundledManagedCoreBootstrapper
    participant Catalog as ManagedCoreCatalog
    participant App as Avalonia App
    participant Window as MainWindow
    participant VM as MainWindowViewModel
    participant Preview as PreviewWarmup
    participant LAN as LanArcadeService

    User->>Program: 启动桌面程序
    Program->>Program: StartupDiagnostics / GeometryDiagnostics 初始化
    Program->>Program: TryRunLanProbeHost(args)
    alt CLI 探针模式
        Program->>LAN: BackendHostService.StartAsync()
        LAN-->>Program: CLI 模式结束后退出
    else 桌面模式
        Program->>Program: InitializeManagedCoreCatalog()
        Program->>Bootstrap: EnsureBundledCorePackages(resourceRoot)
        Program->>Catalog: 注册 AppDomain / package registry / probe path source
        Program->>Catalog: DefaultEmulatorCoreHost.Create().GetInstalledCoreManifests()
        Program->>App: BuildAvaloniaApp()
        App->>Window: 创建 MainWindow
        App->>VM: new MainWindowViewModel(deferStartupWork: true)
        Window->>VM: OnHostWindowOpenedAsync()
        VM->>VM: RunStartupSequenceAsync()
        VM->>VM: RefreshRomLibrary()
        VM->>Preview: WarmPreviewFramesAsync(_romLibrary, CurrentRom)
        alt 启用 LAN Arcade
            VM->>VM: RefreshLanFirewallStatus()
            VM->>LAN: ApplyLanArcadeServerStateAsync()
        else 关闭 LAN Arcade
            VM-->>Window: 跳过后台服务启动
        end
    end
```

启动时有两个并行感比较强的层次：

- `Program.Main` 先把可用核心目录准备好，再交给 Avalonia 建壳。
- `MainWindowViewModel` 构造时只搭服务图和长期资源，真正重活放到窗口 `Opened` 之后的 deferred startup sequence。

## 3. 核心发现与会话创建

```mermaid
flowchart TD
    CallerA["MainWindowViewModel.CreateMainCoreSession()"] --> HostCreate["DefaultEmulatorCoreHost.Create(defaultCoreId?)"]
    CallerB["MainWindowViewModel.CreatePreviewCoreSession()"] --> HostCreate
    CallerC["GameSessionRegistry.StartSessionWithInputBindings()"] --> HostCreate

    HostCreate --> EnsureBundled["BundledManagedCoreBootstrapper.EnsureBundledCorePackages(resourceRoot)"]
    HostCreate --> RegistrySource["RegistryManagedCoreModuleRegistrationSource.LoadModules()"]
    RegistrySource --> ModuleCatalog["DefaultManagedCoreModuleCatalog.CreateModules(...)"]
    ModuleCatalog --> Host["EmulatorCoreHost"]
    Host --> SelectCore["Resolve requested coreId / default coreId"]
    SelectCore --> Module["IManagedCoreModule"]
    Module --> Factory["module.CreateFactory()"]
    Factory --> Session["CreateSession(CoreSessionLaunchRequest)"]
    Session --> CoreSession["IEmulatorCoreSession"]

    Probe["可选：DirectoryManagedCoreModuleRegistrationSource"] --> ModuleCatalog
    Assembly["可选：AssemblyManagedCoreModuleRegistrationSource"] --> ModuleCatalog
```

这条链路说明了当前“核心只是挂件”的关键事实：

- UI、Host、后端调用的是 `IEmulatorCoreSession`、`CoreManifest`、`CoreSessionLaunchRequest` 这类通用抽象。
- 默认核心仍可能落到 bundled NES package，但装载入口已经是统一的 package/module/factory 路径。
- 同一条装载链同时服务主窗口长期会话、预览生成会话和独立游戏窗口会话。

## 4. 从 ROM 选择到游戏窗口启动

```mermaid
flowchart TD
    User["用户在主界面选择 ROM 并点击开始"] --> Play["MainWindowViewModel.PlaySelectedRom()"]
    Play --> Start["StartGameSession(rom)"]
    Start --> LaunchCtl["MainWindowSessionLaunchController.Launch(...)"]
    LaunchCtl --> GameSvc["IGameSessionService.StartSessionWithInputBindings(...)"]
    GameSvc --> Registry["GameSessionRegistry.StartSessionWithInputBindings(...)"]
    Registry --> Host["DefaultEmulatorCoreHost.Create().CreateSession(...)"]
    Host --> CoreSession["IEmulatorCoreSession"]
    Registry --> GameVM["new GameWindowViewModel(..., coreSession)"]
    GameVM --> Capabilities["CoreSessionCapabilityResolver.Resolve*()"]
    GameVM --> Timeline["LegacyTimelineSessionAdapter.Initialize(...)"]
    GameVM --> LoadRom["LoadRom(_romPath)"]
    GameVM --> Loop["GameWindowSessionLoopHost.Start()"]
    Registry --> GameWindow["new GameWindow { DataContext = GameVM }"]
    Registry --> SessionItem["ActiveGameSessionItem"]
    GameWindow --> Visible["ShowWindow() + Activate()"]
```

游戏窗口启动后的核心职责大致如下：

- `GameSessionRegistry` 负责创建独立窗口、生命周期管理、窗口关闭清理和前台激活。
- `GameWindowViewModel` 负责 capability 解析、ROM 载入、帧呈现、音频、存档、时间线、远控与输入路由。
- 每个游戏窗口都有自己的 `IEmulatorCoreSession`，互不共享运行态。

## 5. LAN Arcade / 嵌入式后端运行链路

```mermaid
flowchart TD
    MainVM["MainWindowViewModel"] --> Bundle["MainWindowRuntimeDependencyBundle.Create(...)"]
    Bundle --> Adapter["ArcadeRuntimeContractAdapter"]
    Bundle --> Lan["LanArcadeService"]
    Bundle --> Mirror["BackendStateMirror"]

    Lan --> Lifecycle["LanBackendHostLifecycleService"]
    Lifecycle --> Host["BackendHostService"]
    Host --> Endpoints["HTTP API + WebSocket Endpoint"]
    Endpoints --> Bridge["EmbeddedBackendRuntimeBridge"]
    Bridge --> Adapter

    Adapter --> Query["SessionQueryService"]
    Adapter --> Remote["SessionRemoteControlService"]
    Adapter --> SessionLifecycle["SessionLifecycleService"]

    SessionLifecycle --> GameSessionSvc["IGameSessionService"]
    Query --> GameSessionSvc
    Remote --> GameSessionSvc

    Client["浏览器 / 手机端"] --> Endpoints
    Endpoints --> Stream["SessionStreamBroadcaster / 预览与音频流"]
```

对应的请求回流可以理解为：

```mermaid
sequenceDiagram
    participant Client as 浏览器/手机端
    participant Host as BackendHostService
    participant Bridge as EmbeddedBackendRuntimeBridge
    participant Adapter as ArcadeRuntimeContractAdapter
    participant Lifecycle as SessionLifecycleService
    participant Remote as SessionRemoteControlService
    participant GameSvc as IGameSessionService
    participant GameVM as GameWindowViewModel

    Client->>Host: POST /api/sessions
    Host->>Bridge: StartSessionAsync(request)
    Bridge->>Adapter: StartSessionAsync(request)
    Adapter->>Lifecycle: StartSession(request)
    Lifecycle->>GameSvc: StartSessionWithInputBindings(...)
    GameSvc-->>Client: 返回 sessionId

    Client->>Host: WebSocket / 输入请求
    Host->>Bridge: SetInputStateAsync(sessionId, portId, actionId, value)
    Bridge->>Adapter: SetInputStateAsync(...)
    Adapter->>Remote: SetInputState(sessionId, request)
    Remote->>GameSvc: TrySetRemoteInputState(...)
    GameSvc->>GameVM: 写入对应端口与 actionId

    Client->>Host: 预览 / 串流订阅
    Host->>Bridge: GetSessionPreviewAsync / SubscribeStreamAsync
    Bridge->>Adapter: 查询活动会话画面与音频
```

这部分的关键点是：

- 后端本身并不直接操纵 NES 类型，而是通过 `IBackendRuntimeBridge` 和通用 contract 回调 UI 运行时。
- 远控输入已经走 `portId` / `actionId` 语义，而不是要求后端知道某个核心的具体按钮枚举。
- `BackendHostService` 只负责托管 HTTP / WebSocket；实际 ROM、会话和输入逻辑都回流到 `ArcadeRuntimeContractAdapter` 及其下游服务。

## 6. 当前运行时的职责分层

| 层级 | 当前主要职责 | 代表实现 |
| --- | --- | --- |
| 程序入口层 | 诊断初始化、CLI 分支、Avalonia 启动、核心目录预注册 | `Program` |
| 应用壳层 | 创建桌面主窗口与主 ViewModel | `App`, `MainWindow` |
| 主窗口运行时 | ROM 库、预览、输入设置、LAN 配置、后台线程、状态同步 | `MainWindowViewModel` |
| 核心装载层 | 从 package/assembly/probe path 解析模块并创建会话 | `DefaultEmulatorCoreHost`, `EmulatorCoreHost`, `DefaultManagedCoreModuleCatalog` |
| 独立会话层 | 独立游戏窗口、会话生命周期、远控归属、预览快照 | `GameSessionService`, `GameSessionRegistry`, `GameWindowViewModel` |
| 嵌入式后端层 | HTTP/WebSocket 暴露、会话创建/关闭、远控输入、流订阅 | `LanArcadeService`, `BackendHostService`, `EmbeddedBackendRuntimeBridge`, `ArcadeRuntimeContractAdapter` |

## 7. 主要源码入口

- `src/FC-Revolution.UI/Program.cs`
- `src/FC-Revolution.UI/App.axaml.cs`
- `src/FC-Revolution.UI/Views/MainWindow.axaml.cs`
- `src/FC-Revolution.UI/ViewModels/MainWindowViewModel.cs`
- `src/FC-Revolution.UI/ViewModels/MainWindow/MainWindowViewModel.StartupSequence.cs`
- `src/FC-Revolution.UI/ViewModels/MainWindow/MainWindowViewModel.PreviewWarmup.cs`
- `src/FC-Revolution.UI/ViewModels/MainWindow/MainWindowViewModel.LanAndSessions.cs`
- `src/FC-Revolution.Emulation.Host/EmulatorCoreHost.cs`
- `src/FC-Revolution.UI/Application/GameSessionService.cs`
- `src/FC-Revolution.UI/Infrastructure/GameSessionRegistry.cs`
- `src/FC-Revolution.UI/ViewModels/GameWindowViewModel.cs`
- `src/FC-Revolution.UI/ViewModels/MainWindow/MainWindowRuntimeDependencyBundle.cs`
- `src/FC-Revolution.UI/Application/ArcadeRuntimeContractAdapter.cs`
- `src/FC-Revolution.UI/Application/SessionLifecycleService.cs`
- `src/FC-Revolution.UI/Application/LanArcadeService.cs`
- `src/FC-Revolution.UI/Application/EmbeddedBackendRuntimeBridge.cs`
- `src/FC-Revolution.Backend.Hosting/BackendHostService.cs`
