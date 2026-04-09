# FC-Revolution.UI / LAN / 预览治理任务清单

说明：
- 任务节点统一使用 `「1.x」` 格式。
- 节点完成后，在节点末尾补充 `✅`。
- 本文档聚焦 `FC-Revolution.UI`、内嵌 LAN 运行时适配层，以及预览播放链路。

## 当前进度

- 截至 `2026-04-09`
- 已完成：`8 / 8`（`1.1`、`1.2`、`1.3`、`1.4`、`1.5`、`1.6`、`1.7`、`1.8`）
- 进行中：无
- 最新收口：`1.5` 已补上 `GameWindowRomLoadHandler`、`GameWindowProfileTrustHandler`、`GameWindowDisposeHandler`、`BranchGalleryBranchWorkflowController`、`BranchGallerySelectionEntryController`、`BranchGalleryViewportWorkflowController`、`MainWindowInputBindingWorkflowController` 与 `MainWindowActiveInputWorkflowController.RefreshActiveInputState(...)` 一站式编排；当前 workspace focused tests `69/69` 通过，`dotnet build FC-Revolution.slnx -nologo` 通过（`0 warnings / 0 errors`）。
- 待开始：无

## 任务节点

「1.1」将 `MainWindowViewModel` 从 God Object 收缩为页面编排壳层 ✅

「1.2」抽离 ROM 库、布局和资源管理子模块 ✅

「1.3」抽离预览协调器并统一预览生命周期 ✅

「1.4」清理预览链路中的同步阻塞与无界内存播放 ✅

「1.5」拆分 `GameWindowViewModel` 的会话、渲染、时间线、远控状态 ✅

「1.6」将 LAN 适配层从 UI 线程编排逻辑中抽离 ✅

「1.7」整理 `DebugViewModel` 的刷新、内存编辑和布局配置边界 ✅

「1.8」补齐 `UI` 侧预览、LAN、资源清理的测试护栏 ✅

## 节点说明

### 「1.1」将 `MainWindowViewModel` 从 God Object 收缩为页面编排壳层

需要优化的文件：
- `src/FC-Revolution.UI/ViewModels/MainWindowViewModel.cs`
- `src/FC-Revolution.UI/ViewModels/MainWindow/*.cs`

大致内容：
- 当前 `MainWindowViewModel` 虽然已拆成多个 partial 文件，但核心类型仍同时承担服务创建、布局切换、预览调度、LAN 控制、通知聚合、资源清理和模拟器生命周期。
- 目标是让主窗口只保留页面级状态、命令路由和模块编排，把业务职责搬离主类型。
- 建议优先抽出 `LibraryCatalog`、`PreviewCoordinator`、`LanArcadePanel`、`TaskMessageCenter`、`ResourceManager`、`InputBindings`。

### 「1.2」抽离 ROM 库、布局和资源管理子模块

需要优化的文件：
- `src/FC-Revolution.UI/ViewModels/MainWindowViewModel.cs`
- `src/FC-Revolution.UI/ViewModels/MainWindow/MainWindowViewModel.ResourceManagement.cs`
- `src/FC-Revolution.UI/Models/RomLibraryItem.cs`

大致内容：
- 把 ROM 列表构建、Shelf/Carousel/Kaleidoscope 布局计算、已加载资源回收逻辑拆开。
- 避免主 ViewModel 既关心“显示什么”，又关心“何时销毁预览、何时释放位图、何时清理缓存”。
- 让资源释放策略能够独立测试，而不是继续绑死在窗口对象上。
- 当前进展：已抽出 `MainWindowLibraryCatalogController`、`MainWindowLibraryNavigationController`、`MainWindowResourceCleanupWorkflowController`、`MainWindowRomResourceImportWorkflowController`、`MainWindowRomDeleteWorkflowController`、`MainWindowResourceRootWorkflowController`、`MainWindowRomAssociatedResourceController`、`MainWindowResourceCleanupSelectionController`、`MainWindowResourceLayoutSummaryController`、`MainWindowGlobalInputConfigLoaderController` 与 `MainWindowGlobalInputConfigSaverController` 十一片切分，分别承接 `RefreshRomLibrary` 中的 catalog snapshot 构建、可见 ROM 选择、preferred rom 判定、library summary / empty-state 文案编排，可见 ROM 集合上的邻居导航、shelf 页码摘要、kaleidoscope 翻页保留选中 / 默认选中 / 页码同步等纯决策，资源清理面板的选择校验 / 清理结果 / 资源统计文案汇总 / “是否需要刷新 ROM 库” 判定，ROM 预览视频 / 封面图 / 附加图片导入后的资源导入委托 / preview ready 编排 / 状态文案生成，删除确认后的资源摘要透传与 ROM / 关联资源删除执行编排，资源根目录应用后的配置保存 / ROM 库刷新 / 当前 ROM 展示同步工作流，ROM 关联资源摘要 / preview artifact 清理 / registered object 删除 / profile / legacy profile 清理与 timeline 目录删除边界，cleanup 目标选择状态的 has-selection / select-all / clear-all / selection 投影，resource layout summary 的纯字符串拼装，global input config 的默认键回退 / `InputBindingEntry` 构造 / layout 应用 / `ExtraInputBindingProfile -> ExtraInputBindingEntry` 投影，以及 `PlayerInputOverrides` / `ExtraInputBindings` / `ShortcutBindings` / `InputBindingLayout` 的保存态组装；`MainWindowViewModel` 的 ROM catalog、导航、资源清理、导入、删除确认后与资源根目录变更后的工作流逻辑已明显变薄，并补上 preferred ROM 保留、搜索空态、总数/筛选数摘要、导航/分页决策、资源清理工作流空选择 / 成功路径、资源导入工作流委托 / 文案、删除工作流摘要 / delete-rom-only / delete-with-resources、关联资源摘要 / 删除 / cleanup selection / resource layout summary 与资源根目录工作流顺序 / 状态文案，以及 global input config loader / saver 的最小测试。本节点按当前结构治理目标收口，后续更深的资源释放问题不再作为 `1.2` 继续展开。

### 「1.3」抽离预览协调器并统一预览生命周期

需要优化的文件：
- `src/FC-Revolution.UI/ViewModels/MainWindowViewModel.cs`
- `src/FC-Revolution.UI/Models/StreamingPreview.cs`
- `src/FC-Revolution.UI/Models/RomLibraryItem.cs`
- `src/FC-Revolution.UI/Models/Previews/*.cs`

大致内容：
- 当前预览播放策略分散在 `MainWindowViewModel`、`RomLibraryItem`、`StreamingPreview`、`RawFramePreviewSource`、`FFmpegVideoPreviewSource` 中。
- 需要引入统一的预览协调器，负责加载、预热、平滑播放、缓存回收、UI 线程切换和后台解码调度。
- `RomLibraryItem` 应尽量退回为状态承载对象，不再直接主导预览内存策略。
- 当前进展：已完成 warmup、load、tick、selection、asset-ready、cleanup、warmup-item、warmup-request、startup-preview、playback-shell、queue-shell、warmup-shell、startup-shell、preview-sync、preview-entrypoints、preview-tail、preview-paths、preview-settings、preview-item-state、preview-source-factory、preview-preload-setting 共二十一片切分；`MainWindowViewModel` 侧的预览主干编排、路径解析和主要命令入口已基本抽离，`RomLibraryItem` 内部预览状态也已迁入独立 helper，`StreamingPreview` 的源选择与 preload 配置入口也已独立；本节点按结构治理目标收口，剩余问题转入 `1.4` 处理。

### 「1.4」清理预览链路中的同步阻塞与无界内存播放

需要优化的文件：
- `src/FC-Revolution.UI/ViewModels/MainWindowViewModel.cs`
- `src/FC-Revolution.UI/Models/Previews/FFmpegVideoPreviewSource.cs`
- `src/FC-Revolution.UI/Models/Previews/RawFramePreviewSource.cs`
- `src/FC-Revolution.UI/Models/RomLibraryItem.cs`

大致内容：
- 清理 `Wait()`、同步释放和全量帧物化等阻塞行为。
- 将“当前播放帧缓存”和“后台预取窗口”从实现细节提升为显式策略，设置内存上限与淘汰规则。
- 避免后台线程直接碰触需要 UI 线程语义的对象状态。
- 当前进展：已完成 `preview-caching-policy`、`decoder-cleanup`、`window-policy`、`window-cache-budget`、`preload-try-lock`、`decode-lock-policy`、`window-eviction-policy`、`window-frame-normalizer` 与 `ffmpeg-foreground-miss-lock-inversion-fix` 九片切分；raw preview 的 full-frame cache 已有显式大小上限，`StreamingPreview` / `FFmpegVideoPreviewSource` 的 preload 配置、预取窗口、窗口字节预算，以及 active/prefetched window 的预算分配 / promotion / 替换淘汰决策都已独立成策略入口，`FFmpegVideoPreviewSource.Dispose()` 中的同步等待已改为取消后延迟清理解码器，后台 prefetch 在 decode 锁争用时会直接跳过当前轮次而不再排队阻塞，前台阻塞解码与后台 try-lock 预取已收敛到独立的 `VideoPreviewDecodeLockPolicy`，miss 后窗口内缺帧补齐 / 前导补齐 / 尾段延用最后已知帧的归一化逻辑也已下沉到 `VideoPreviewWindowFrameNormalizer`，前台 cache-miss 解码也已改为在锁外等待 decode 锁并在回锁后提交 active window，从而消除了与后台预取 `decodeLock -> _syncRoot` 的锁顺序反转；本轮又把 `FFmpegVideoPreviewSource.ScheduleDecoderCleanup()` 的后台等待改成 `WaitAsync` continuation，去掉 fire-and-forget 线程阻塞式 `_decodeGate.Wait()`，并为 `RawFramePreviewSource` 增补了 `_prefetchCts + generation` 驱动的可取消预取生命周期，避免 `EnableMemoryPlayback()` / `DisableMemoryPlayback()` / `Dispose()` 之后旧 prefetch 迟到发布覆盖新状态，同时补上“decode gate 占用下 dispose 仍能快速返回并在 gate 释放后完成清理”和 raw prefetch 生命周期清空的最小测试。本节点按预览链路治理目标收口。

### 「1.5」拆分 `GameWindowViewModel` 的会话、渲染、时间线、远控状态

需要优化的文件：
- `src/FC-Revolution.UI/ViewModels/GameWindowViewModel.cs`
- `src/FC-Revolution.UI/ViewModels/GameWindowViewModel.Rendering.cs`
- `src/FC-Revolution.UI/ViewModels/GameWindowViewModel.Session.cs`
- `src/FC-Revolution.UI/ViewModels/GameWindowViewModel.Timeline.cs`
- `src/FC-Revolution.UI/ViewModels/GameWindowViewModel.Input.cs`

大致内容：
- 将游戏窗口拆成 `GameSessionRuntime`、`FramePresenter`、`TimelinePanelViewModel`、`RemoteControlState` 等边界。
- 把逐帧图像呈现和会话生命周期编排解耦，减少每帧都触发的 UI 级附带工作。
- 将时间线刷新节奏从显示节奏中隔离，降低 UI 线程抖动。
- 当前进展：已抽出 `GameWindowShortcutRouter`、`GameWindowRemoteControlStateController`、`GameWindowModifiedMemoryLockStateController`、`GameWindowStatusToastController`、`GameWindowInputBindingResolver`、`GameWindowViewportDiagnosticsController`、`GameWindowTimelineStateController`、`GameWindowLocalInputProjectionController`、`GameWindowAspectRatioProjectionController`、`GameWindowRewindSequencePlanner`、`GameWindowPreviewNodeFactory`、`GameWindowTimelineManifestSyncController`、`GameWindowTimelineRefreshCadenceController` 与 `GameWindowFpsStatusController` 十四片切分，并让 `GameWindowViewModel.Timeline`、`MainWindowViewModel`、`BranchGalleryViewModel` 统一通过 `ITimeTravelService` 消费 `CoreTimelineSnapshot` / `CoreBranchPoint`，把 legacy `FrameSnapshot` / `BranchPoint` 收口到 repository / bridge 边界；此前又陆续补上 `GameWindowSessionRuntimeController`、`GameWindowFramePresenterController`、`GameWindowLayeredFrameBuilderController`、`GameWindowRenderDiagnosticsStateController`、`GameWindowInputStateController`、`GameWindowRemoteControlWorkflowController`、`GameWindowRemoteControlRuntimeSlotStateController`、`GameWindowTimelinePersistenceController`、`BranchGalleryTimelineNavigationController`、`GameWindowRewindPlaybackController`、`BranchGalleryExportWorkflowController`、`BranchGalleryCanvasProjectionController`、`BranchGalleryPreviewNodeWorkflowController`、`BranchGalleryCanvasRefreshController`、`BranchGalleryTimelineNavigationExecutionController`、`BranchGalleryExportExecutionController`、`BranchGallerySelectionController`、`MainWindowActiveInputRuntimeController`、`MainWindowActiveInputWorkflowController`、`MainWindowInputKeyboardWorkflowController`、`GameWindowDebugWindowWorkflowController`、`GameWindowSaveStateWorkflowController`、`GameWindowDebugWindowOpenController`、`GameWindowSessionFailureHandler` 与 `GameWindowSessionCommandController`。本轮继续补上 `GameWindowRomLoadHandler`、`GameWindowProfileTrustHandler`、`GameWindowDisposeHandler`、`BranchGalleryBranchWorkflowController`、`BranchGallerySelectionEntryController`、`BranchGalleryViewportWorkflowController`、`MainWindowInputBindingWorkflowController`，并让 `MainWindowActiveInputWorkflowController` 提供一站式 `RefreshActiveInputState(...)` 编排。至此 `GameWindowViewModel.Session` 已退回到 handler forward 与 modified-memory apply 薄壳，`BranchGalleryViewModel` 已退回到命令入口、展示投影与最终 selection apply 薄壳，`MainWindowViewModel.InputAndShortcuts` 也已退回到输入绑定命令、active input apply 与 legacy mirror 同步薄壳；配套 focused tests 与整解构建已通过，本节点按当前结构治理目标收口。除此之外，demo core 之外仍未真正接入业务级第二核心、远控 contracts/输入状态机仍保留 `p1/p2 + NesButtonDto` compatibility shell，整组 UI tests 的长跑稳定性也仍待继续观察，但这些问题已不再作为 `1.5` 继续展开。

### 「1.6」将 LAN 适配层从 UI 线程编排逻辑中抽离

需要优化的文件：
- `src/FC-Revolution.UI/Application/ArcadeRuntimeContractAdapter.cs`
- `src/FC-Revolution.UI/Application/LanArcadeService.cs`
- `src/FC-Revolution.UI/Application/GameSessionService.cs`

大致内容：
- 当前 LAN 适配层混合了 UI 线程调度、会话读写、位图编码、流广播和宿主生命周期。
- 建议拆成 `SessionQueryService`、`PreviewAssetResolver`、`RemoteControlBroker`、`SessionStreamBroadcaster`、`LanHostLifecycleService`。
- 目标是让 UI 层只关心配置和展示，不直接承载远程服务边界。
- 当前进展：已抽出 `SessionQueryService`、`SessionRemoteControlService`、`PreviewAssetResolver`、`SessionLifecycleService`、`SessionStreamBroadcaster` 与 `LanBackendHostLifecycleService` 六片切分，分别承接 `GetSessionSummaries` / `GetSessionPreviewAsync` 的读侧投影与快照编码、`ClaimControl` / `ReleaseControl` / `RefreshHeartbeat` / `SetButtonState` 的远控写侧同步核心逻辑、ROM preview asset 的路径解析 / 媒体类型判定 / asset 解析、`StartSessionAsync` / `CloseSessionAsync` 的同步生命周期核心、video/audio stream 的订阅分发 / 音频重分块 / 空订阅者回收，以及内嵌 LAN backend host 的 start / stop / dispose 状态机与 client 生命周期；`ArcadeRuntimeContractAdapter` 与 `LanArcadeService` 的对外入口都已明显变薄，并补上 “session 不存在时返回 empty/null” 、`SetButtonStateAsync` generic-first（当 `ButtonStateRequest.ActionId` 存在时优先转 `SetInputStateRequest`；仅纯 legacy payload 才走 `SetButtonState` 兼容壳）、`GetRomPreviewAssetAsync` 解析已存在/缺失/不支持预览文件、`StartSessionAsync` / `CloseSessionAsync` 成功与缺失路径，以及 `GetLocalHealthAsync` 未启动提示的最小测试；`LanArcadeServiceTests` 与 `ArcadeRuntimeContractAdapterTests` 已复跑通过，本节点按结构治理目标收口。

### 「1.7」整理 `DebugViewModel` 的刷新、内存编辑和布局配置边界

需要优化的文件：
- `src/FC-Revolution.UI/ViewModels/DebugViewModel.cs`

大致内容：
- `DebugViewModel` 仍是大文件，但它的职责域相对集中，且已有测试保护，适合作为低风险拆分对象。
- 将刷新调度、内存检查、修改内存、界面布局配置拆成更小的 ViewModel / Service。
- 保持调试窗口功能不变，先做结构瘦身。
- 当前进展：已抽出 `DebugMemoryGridBuilder`、`DebugStateRowsBuilder`、`DebugDisplaySettingsController`、`DebugModifiedMemoryProfileController`、`DebugModifiedMemoryRuntimeSyncController`、`DebugModifiedMemoryListController`、`DebugLayoutStateController`、`DebugPageStateController`、`DebugMemoryInputController`、`DebugLiveRefreshOrchestrator`、`DebugMemoryWriteController`、`DebugMemoryReadController` 与 `DebugPageNavigationController` 十三片切分，分别承接内存页网格构建 / 原地更新 / locator 高亮计算、寄存器行 / PPU 行 / 反汇编行与 opcode 文本映射的纯展示构建逻辑、debug window display settings 的系统配置读取 / 持久化 / 默认值回退、modified-memory profile 的加载 / 保存 / 状态文案组合、runtime modified-memory entry 的构建 / 过滤 / 批量替换、modified-memory 列表的页数计算 / 页码回夹 / 可见页切片 / upsert-remove / load-replace 编排、布局 pane 可见性 / 列跨度 / 窗口宽高 / 字体尺寸与 pending display settings hint 的纯投影、memory/stack/zero-page/disasm 的页摘要 / 有界页切换 / memory page-number 回夹逻辑、地址 / 数值 / 跳页输入解析与标准错误消息、live tick gate / refresh capture-address-apply plan / locator fallback refresh 的纯编排、内存写成功后的页码 / 高亮 / 状态文案投影 / modified entry upsert runtime entry 构建 / lock toggle / remove 决策，以及内存读取成功后的值/页码/高亮/状态投影与各分页命令的纯导航决策；`DebugViewModel` 目前保留实际 `_readMemory/_writeMemory` 调用、异常处理、`HandleDebugFailure`、`Refresh()/ScheduleRefresh()`、Dispatcher 调度、rows 构建、副作用、runtime callback 执行顺序、profile import trust 对话框与最终字段赋值等宿主编排职责，并复跑 `DebugViewModelTests`、`DebugLayoutStateControllerTests`、`DebugPageStateControllerTests`、`DebugMemoryInputControllerTests`、`DebugLiveRefreshOrchestratorTests`、`DebugMemoryWriteControllerTests`、`DebugMemoryReadControllerTests` 与 `DebugPageNavigationControllerTests` 确认现有布局、页切换提示、输入解析、读写回调与刷新行为未回归；此外，调试 capability 边界已经切到 `ICoreDebugSurface -> CoreDebugState`，UI 不再直接依赖 legacy `DebugState`，但更系统无关的 panel / region / disassembly 模型仍待后续继续抽象。本节点按结构治理目标收口。

### 「1.8」补齐 `UI` 侧预览、LAN、资源清理的测试护栏

需要优化的文件：
- `tests/FC-Revolution.UI.Tests/ViewModels/MainWindow/*.cs`
- `tests/FC-Revolution.UI.Tests/ViewModels/GameWindow/*.cs`
- `tests/FC-Revolution.UI.Tests/Models/StreamingPreviewTests.cs`
- 建议新增 `tests/FC-Revolution.UI.Tests/Application/*.cs`

大致内容：
- 补上预览源行为测试、平滑播放切换测试、LAN 生命周期测试、资源清理与缓存淘汰测试。
- 减少对反射式 TestHost 的依赖，把测试边界对齐到拆分后的模块。
- 将“路径解析测试”提升为“行为与生命周期测试”。
