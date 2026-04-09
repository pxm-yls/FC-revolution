# FC-Revolution.Rendering / Storage 整理任务清单

说明：
- 任务节点统一使用 `「4.x」` 格式。
- 节点完成后，在节点末尾补充 `✅`。
- 本文档聚焦渲染工程边界、运行时分配热点，以及存储布局与安全边界。

## 当前进度

- 截至 `2026-04-02`
- 已完成：`5 / 5`
- 当前状态：已收口

## 任务节点

「4.1」将 `Rendering` 拆成平台无关管线与平台后端 ✅

「4.2」降低 layered frame 提取与构建的热路径分配 ✅

「4.3」将 reference renderer / diff 工具与运行时渲染表面解耦 ✅

「4.4」拆分 `AppObjectStorage` 的布局策略与文件系统实现 ✅

「4.5」为 `FileSystemObjectStorage` 增加路径约束与安全测试 ✅

## 节点说明

### 「4.1」将 `Rendering` 拆成平台无关管线与平台后端

需要优化的文件：
- `src/FC-Revolution.Rendering/FC-Revolution.Rendering.csproj`
- `src/FC-Revolution.Rendering/Common/*.cs`
- `src/FC-Revolution.Rendering/Metal/*.cs`

大致内容：
- 当前工程同时包含平台无关的数据提取与 macOS Metal 实现，扩大了构建和部署耦合。
- 建议拆成 `Rendering.Abstractions`、`Rendering.Pipeline`、`Rendering.Reference`、`Rendering.Metal`。
- 让普通消费者不再被平台后端和原生运行时依赖拖入。

### 「4.2」降低 layered frame 提取与构建的热路径分配

需要优化的文件：
- `src/FC-Revolution.Rendering/Common/RenderDataExtractor.cs`
- `src/FC-Revolution.Rendering/Common/LayeredFrameBuilder.cs`
- `src/FC-Revolution.Rendering/Common/VisibleTileResolver.cs`

大致内容：
- 当前提取流程会频繁创建 sprite、tile、motion vector、atlas、palette 副本等对象。
- 需要评估对象池、缓冲区复用、增量提取或延迟物化策略。
- 目标是降低游戏窗口 layered render 路径上的内存分配与复制成本。

### 「4.3」将 reference renderer / diff 工具与运行时渲染表面解耦

需要优化的文件：
- `src/FC-Revolution.Rendering/Common/ReferenceFrameRenderer.cs`
- `src/FC-Revolution.Rendering/Common/PixelDiff.cs`

大致内容：
- 这些类更偏向验证、诊断和回归对比工具，不应继续和运行时渲染能力混在同一表面层级。
- 建议移动到 `Reference` / `Diagnostics` 方向的模块中，保留测试与调试用途。
- 这样可以让运行时模块边界更清晰，也便于以后单独做性能治理。

### 「4.4」拆分 `AppObjectStorage` 的布局策略与文件系统实现

需要优化的文件：
- `src/FC-Revolution.Storage/AppObjectStorage.cs`
- `src/FC-Revolution.Storage/FileSystemObjectStorage.cs`

大致内容：
- 当前 `AppObjectStorage` 同时承担资源根解析、遗留迁移、bucket 目录布局、对象 key 规则和 bootstrap。
- 建议拆成 `ResourceRootResolver`、`StorageLayout`、`ObjectKeyPolicy`、`FileSystemObjectStorage`。
- 让 `Storage` 更接近可替换、可测的基础设施模块。

### 「4.5」为 `FileSystemObjectStorage` 增加路径约束与安全测试

需要优化的文件：
- `src/FC-Revolution.Storage/FileSystemObjectStorage.cs`
- `tests/FC-Revolution.UI.Tests/AppObjectStorageTests.cs`
- 建议新增 `tests/FC-Revolution.Storage.Tests/*`

大致内容：
- 当前 `GetObjectPath` 只做分隔符规范化和拼接，没有明确保证结果仍位于 bucket root 内。
- 需要增加 path traversal、相对路径回退、round-trip object key、bucket containment 等测试。
- 若后续拆出独立存储测试工程，可将 UI 测试里对 Storage 的依赖移出。
