# FC-Revolution.UI 优化任务清单

说明：
- 任务节点统一使用 `「1.x」` 格式。
- 节点完成后，在节点末尾补充 `✅`。
- 本文档不包含工时，仅用于拆分任务与说明优化范围。

## 任务节点

「1.1」清理 `FC-Revolution.UI` 体量基线与构建产物 ✅

「1.2」拆分 `MainWindowViewModel` 的职责边界 ✅

「1.3」把 `MainWindow.axaml` 组件化 ✅

「1.4」收缩 `MainWindow.axaml.cs` 的窗口事件代码 ✅

「1.5」拆分 `GameWindowViewModel` ✅

「1.6」拆分 `StreamingPreview` 及预览源实现 ✅

「1.7」整理样式、资源字典与设置页结构 ✅

「1.8」重构并细化 UI 测试边界 ✅

「1.9」拆分 `BackendHostService` 的接口与推流职责 ✅

「1.10」整理 `FC-Revolution.Core` 核心类的内部结构

## 节点说明

### 「1.1」清理 `FC-Revolution.UI` 体量基线与构建产物

需要优化的文件：
- `src/FC-Revolution.UI/FC-Revolution.UI.csproj`
- `src/FC-Revolution.UI/bin`
- `src/FC-Revolution.UI/obj`

大致内容：
- 区分源码体量与构建产物体量，避免把 `bin` 和 `obj` 的膨胀误判为源码复杂度问题。
- 明确哪些资源应该保留在项目输出流程里，哪些只应存在于构建中间目录。
- 为后续 UI 重构建立可对比的基线，便于判断优化是否真正降低维护成本。

### 「1.2」拆分 `MainWindowViewModel` 的职责边界

需要优化的文件：
- `src/FC-Revolution.UI/ViewModels/MainWindowViewModel.cs`

大致内容：
- 将主窗口 ViewModel 中的 ROM 库管理、预览生成、输入绑定、快捷键、消息中心、局域网、资源清理、游戏会话管理拆分为独立子 ViewModel 或协调器。
- 让主窗口 ViewModel 只保留页面级状态、命令转发和模块间编排。
- 避免继续在同一个 God Object 中叠加新功能。

### 「1.3」把 `MainWindow.axaml` 组件化

需要优化的文件：
- `src/FC-Revolution.UI/Views/MainWindow.axaml`
- 建议新增 `src/FC-Revolution.UI/Views/MainWindow/*.axaml`

大致内容：
- 将主界面拆成多个 `UserControl`，至少拆出库头部、Shelf 视图、Carousel 视图、Kaleidoscope 视图、消息面板、设置面板、快速 ROM 输入编辑面板。
- 把当前内联的大段 `DataTemplate` 与 `ItemsControl` 模板拆开，降低单文件长度和阅读成本。
- 让每个子视图只负责一块明确的界面区域。

### 「1.4」收缩 `MainWindow.axaml.cs` 的窗口事件代码

需要优化的文件：
- `src/FC-Revolution.UI/Views/MainWindow.axaml.cs`

大致内容：
- 将滚动同步、快捷键路由、面板自动隐藏、卡片点击/双击、输入拖拽等行为拆到更小的行为类、帮助类或控制器中。
- 保留必须依赖视图树和窗口生命周期的代码，减少 code-behind 直接承载业务流程。
- 降低主窗口行为代码与主窗口 ViewModel 的耦合度。

### 「1.5」拆分 `GameWindowViewModel`

需要优化的文件：
- `src/FC-Revolution.UI/ViewModels/GameWindowViewModel.cs`

大致内容：
- 把模拟器线程与 ROM 生命周期、渲染显示、时间线与分支画廊、远程控制状态、会话快捷键等职责拆开。
- 让 `GameWindowViewModel` 主要承担窗口绑定和状态聚合，不再直接承载全部会话逻辑。
- 降低游戏窗口后续扩展时的回归风险。

### 「1.6」拆分 `StreamingPreview` 及预览源实现

需要优化的文件：
- `src/FC-Revolution.UI/Models/StreamingPreview.cs`
- 建议新增 `src/FC-Revolution.UI/Models/Previews/IPreviewSource.cs`
- 建议新增 `src/FC-Revolution.UI/Models/Previews/RawFramePreviewSource.cs`
- 建议新增 `src/FC-Revolution.UI/Models/Previews/FFmpegVideoPreviewSource.cs`

大致内容：
- 将 `StreamingPreview` 外观接口与不同预览源实现分文件管理。
- 把原始帧预览与 FFmpeg 视频预览从单一大文件中拆开，保留清晰边界。
- 在不改变行为的前提下，先完成低风险的结构性瘦身。

### 「1.7」整理样式、资源字典与设置页结构

需要优化的文件：
- `src/FC-Revolution.UI/Views/MainWindow.axaml`
- 建议新增 `src/FC-Revolution.UI/Styles/*.axaml`
- 建议新增 `src/FC-Revolution.UI/Views/Settings/*.axaml`

大致内容：
- 将当前 `Window.Styles` 中的大量内联样式迁移到独立资源字典。
- 按使用场景拆分设置页，例如游戏设置、快捷键、预览、局域网、输入配置。
- 减少样式重复和设置页模板堆积，提高复用性和可读性。

### 「1.8」重构并细化 UI 测试边界

需要优化的文件：
- `tests/FC-Revolution.UI.Tests/MainWindowViewModelTests.cs`
- `tests/FC-Revolution.UI.Tests/GameWindowViewModelTests.cs`
- `tests/FC-Revolution.UI.Tests/StreamingPreviewTests.cs`
- 建议新增按模块拆分的测试文件

大致内容：
- 将围绕巨型 ViewModel 的测试迁移到拆分后的边界上，例如预览生成、输入绑定、局域网、资源清理、游戏会话等。
- 让测试文件和生产代码保持一致的模块结构，避免后续继续集中到单一测试文件。
- 提高重构过程中的可验证性，降低模块拆分后的回归风险。

### 「1.9」拆分 `BackendHostService` 的接口与推流职责

需要优化的文件：
- `src/FC-Revolution.Backend.Hosting/BackendHostService.cs`
- 建议新增 `src/FC-Revolution.Backend.Hosting/Endpoints/*.cs`
- 建议新增 `src/FC-Revolution.Backend.Hosting/Streaming/*.cs`
- 建议新增 `src/FC-Revolution.Backend.Hosting/WebSockets/*.cs`

大致内容：
- 将 `BackendHostService` 中的 HTTP 路由注册、WebPad 静态资源输出、控制 WebSocket、视频推流、音频推流拆开。
- 让宿主服务只负责应用启动、停止和模块装配，不再直接承载所有端点行为。
- 将 JPEG 视频帧发送、PCM 音频发送、流协议头处理等流媒体逻辑移入独立组件，降低后续调试和替换编码策略的成本。
- 将控制通道的消息收发、连接状态、占用释放逻辑提取为独立 WebSocket 处理器，避免服务入口类继续膨胀。

### 「1.10」整理 `FC-Revolution.Core` 核心类的内部结构

需要优化的文件：
- `src/FC-Revolution.Core/PPU/Ppu2C02.cs`
- `src/FC-Revolution.Core/CPU/Cpu6502.cs`
- `src/FC-Revolution.Core/NesConsole.cs`
- 可选关注 `src/FC-Revolution.Core/APU/Apu2A03.cs`

大致内容：
- 这部分目前主要是“单一领域内代码较长”，不是 UI 那种跨业务域堆叠，因此优先级低于 UI 和 Hosting。
- `Ppu2C02` 可以按寄存器访问、VRAM 访问、背景抓取、精灵评估、像素输出、状态序列化拆成多个文件或内部帮助类型，提升可读性。
- `Cpu6502` 可以将寻址模式、指令实现、查表初始化拆分，保留 CPU 作为统一执行入口，但降低单文件密度。
- `NesConsole` 可以把帧执行协调、单步调试、快照装载等辅助路径进一步整理，保持主控制台类聚焦在系统装配与执行编排。
- 这类优化目标以可维护性和可测试性为主，不建议在没有测试护栏的前提下进行大规模行为改写。
