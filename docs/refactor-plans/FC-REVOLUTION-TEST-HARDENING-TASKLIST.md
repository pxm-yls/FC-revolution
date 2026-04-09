# FC-Revolution 测试护栏强化任务清单

说明：
- 任务节点统一使用 `「5.x」` 格式。
- 节点完成后，在节点末尾补充 `✅`。
- 本文档专门覆盖“当前测试是绿的，但对后续重构保护不足”的区域。

## 当前进度

- 截至 `2026-04-03`
- 已完成：`7 / 7`
- 当前状态：`5.7` 已完成，当前清单收口

## 任务节点

「5.1」补齐 `UI` 预览链路和资源清理测试 ✅

「5.2」补齐 `UI` LAN 生命周期与远控适配测试 ✅

「5.3」补齐 `Backend` 并发读写和异常路径测试 ✅

「5.4」补齐 `Core` stepping / DMA / IRQ / PPU 时序测试 ✅

「5.5」补齐 `Rendering` 分配预算与回归对比测试 ✅

「5.6」补齐 `Storage` 路径安全与对象键一致性测试 ✅

「5.7」隔离 `UI` 测试中的 `AppObjectStorage` / 环境变量共享状态 ✅

## 节点说明

### 「5.1」补齐 `UI` 预览链路和资源清理测试

需要优化的文件：
- `tests/FC-Revolution.UI.Tests/Models/StreamingPreviewTests.cs`
- `tests/FC-Revolution.UI.Tests/ViewModels/MainWindow/MainWindowViewModelPreviewTests.cs`
- 建议新增 `tests/FC-Revolution.UI.Tests/Models/PreviewSources/*.cs`

大致内容：
- 增加 FFmpeg 预览源行为、预加载窗口、平滑播放切换、缓存淘汰、生命周期释放测试。
- 补上 `MainWindow` 中预览队列和资源清理的行为测试，而不只验证文件路径解析。

### 「5.2」补齐 `UI` LAN 生命周期与远控适配测试

需要优化的文件：
- 建议新增 `tests/FC-Revolution.UI.Tests/Application/ArcadeRuntimeContractAdapterTests.cs`
- 建议新增 `tests/FC-Revolution.UI.Tests/Application/LanArcadeServiceTests.cs`

大致内容：
- 验证位图编码、会话查询、远控申请/释放、宿主启动/停止、Dispose 路径。
- 覆盖 UI 线程调度边界，减少后续 LAN 抽离时的黑箱区域。

### 「5.3」补齐 `Backend` 并发读写和异常路径测试

需要优化的文件：
- `tests/FC-Revolution.Backend.Hosting.Tests/Endpoints/*.cs`
- `tests/FC-Revolution.Backend.Hosting.Tests/Streaming/*.cs`
- `tests/FC-Revolution.Backend.Hosting.Tests/WebSockets/*.cs`

大致内容：
- 增加快照同步并发测试、控制通道断连测试、无效 JSON / 未知动作测试、慢客户端测试。
- 增加流订阅为空、音视频发送中断、增强模式切换等非 happy path 场景。

### 「5.4」补齐 `Core` stepping / DMA / IRQ / PPU 时序测试

需要优化的文件：
- `tests/FC-Revolution.Core.Tests/*.cs`

大致内容：
- 对 `NesConsole` 建立多执行路径一致性测试。
- 补上 DMA stall、IRQ/NMI 传播、页跨越和 PPU 关键时序测试。
- 降低未来整理 `NesConsole` / `Ppu2C02` / `Cpu6502` 时的盲区。

### 「5.5」补齐 `Rendering` 分配预算与回归对比测试

需要优化的文件：
- `tests/FC-Revolution.Rendering.Tests/*.cs`

大致内容：
- 在现有正确性测试之外，补一些分配预算或大批量帧处理的压力测试。
- 覆盖 layered frame 提取和 reference renderer 的大帧量回归场景。

### 「5.6」补齐 `Storage` 路径安全与对象键一致性测试

需要优化的文件：
- `tests/FC-Revolution.UI.Tests/AppObjectStorageTests.cs`
- 建议新增 `tests/FC-Revolution.Storage.Tests/*.cs`

大致内容：
- 增加对象键往返、路径穿越、非法键、bucket 越界等测试。
- 将当前偏“配置路径选择”的测试边界扩展到真正的存储安全与一致性验证。

### 「5.7」隔离 `UI` 测试中的 `AppObjectStorage` / 环境变量共享状态

需要优化的文件：
- `tests/FC-Revolution.UI.Tests/DebugViewModelTests.cs`
- `tests/FC-Revolution.UI.Tests/DebugModifiedMemoryProfileControllerTests.cs`
- `tests/FC-Revolution.UI.Tests/AppObjectStorageTests.cs`
- `tests/FC-Revolution.UI.Tests/RomConfigProfileTests.cs`
- 以及其他会修改 `AppObjectStorage.ConfigureResourceRoot(...)` 或 `FC_REVOLUTION_RESOURCE_ROOT` 的 `UI` 测试

大致内容：
- 当前若干 `UI` 测试会修改进程级 `AppObjectStorage` root 和资源目录环境变量，但并未统一进入同一 xUnit collection，单跑可绿、合跑时存在共享状态竞态。
- 建议将这些测试统一串行到共享 collection，或引入统一 fixture 管理测试 root，避免“写 profile -> 读 profile”过程中被其他测试切走全局根目录。
- 本节点聚焦测试编排与隔离，不需要改动产品逻辑。
