# FC-Revolution.Backend.Hosting 治理任务清单

说明：
- 任务节点统一使用 `「2.x」` 格式。
- 节点完成后，在节点末尾补充 `✅`。
- 本文档聚焦 `Backend`、`Backend.Hosting`、`Backend.Abstractions`、`Contracts` 相关边界。

## 当前进度

- 截至 `2026-04-02`
- 已完成：`6 / 6`
- 当前状态：已收口

## 任务节点

「2.1」将 `BackendRuntimeState` 改为原子切换的只读快照模型 ✅

「2.2」拆分 `IBackendRuntimeBridge` 的过宽职责 ✅

「2.3」拆分视频/音频推流编码与 WebSocket 编排 ✅

「2.4」拆分控制通道的 claim 协调与消息编解码 ✅

「2.5」收缩 `BackendEndpointMapper` 的组合根职责 ✅

「2.6」补齐并发、断连、异常路径测试 ✅

## 节点说明

### 「2.1」将 `BackendRuntimeState` 改为原子切换的只读快照模型

需要优化的文件：
- `src/FC-Revolution.Backend.Hosting/BackendRuntimeState.cs`
- `src/FC-Revolution.Backend.Hosting/BackendContractFacade.cs`
- `src/FC-Revolution.Backend.Hosting/Endpoints/BackendEndpointMapper.cs`

大致内容：
- 当前 ROM 列表和会话列表通过共享 `List<>` 原地替换，存在并发读取与同步写入冲突的风险。
- 建议改为不可变快照或复制后整体替换的读模型，避免 API 请求持有可变集合引用。
- 同时补充读写竞态一致性测试。

### 「2.2」拆分 `IBackendRuntimeBridge` 的过宽职责

需要优化的文件：
- `src/FC-Revolution.Backend.Abstractions/IBackendRuntimeBridge.cs`
- `src/FC-Revolution.Backend.Hosting/BackendHostService.cs`
- `src/FC-Revolution.Backend.Hosting/BackendContractFacade.cs`

大致内容：
- 当前桥接接口同时承载会话创建、预览、控制、心跳、按钮输入和流订阅。
- 建议拆成查询、控制、预览、流订阅等更小的接口，减少 transport 细节泄漏到统一抽象。
- 明确读模型由谁提供，避免宿主层存在双重事实来源。

### 「2.3」拆分视频/音频推流编码与 WebSocket 编排

需要优化的文件：
- `src/FC-Revolution.Backend.Hosting/Streaming/BackendStreamWebSocketHandler.cs`
- `src/FC-Revolution.Backend.Hosting/PixelEnhancer.cs`

大致内容：
- 当前流处理器同时负责订阅、缩放、增强、JPEG 编码、PCM 重采样、协议封包和 WebSocket 发送。
- 建议拆出 `VideoFrameEncoder`、`AudioChunkEncoder`、`StreamPacketWriter`，保留处理器为薄协调层。
- 优先减少每帧重复复制和过多临时分配。

### 「2.4」拆分控制通道的 claim 协调与消息编解码

需要优化的文件：
- `src/FC-Revolution.Backend.Hosting/WebSockets/BackendControlWebSocketHandler.cs`

大致内容：
- 当前控制通道同时处理 socket 生命周期、claim 规则、动作校验、JSON 读取和响应发送。
- 建议拆出 `RemoteControlLeaseCoordinator` 与 `ControlMessageCodec`。
- 目标是降低每条消息的解析分配，并让断连释放、无效动作、占用切换等行为可单测。

### 「2.5」收缩 `BackendEndpointMapper` 的组合根职责

需要优化的文件：
- `src/FC-Revolution.Backend.Hosting/Endpoints/BackendEndpointMapper.cs`
- `src/FC-Revolution.Backend.Hosting/Endpoints/BackendStaticResponseWriter.cs`
- `src/FC-Revolution.Backend.Hosting/WebPad/*.cs`

大致内容：
- 当前 endpoint mapper 同时注册流量日志、中间件、WebPad、调试页、REST API、WebSocket 端点。
- 建议将 WebPad / Debug / API / WebSocket 分为独立映射模块，减少单入口的装配膨胀。
- 静态资源输出也应避免每次请求重复做无意义编码工作。

### 「2.6」补齐并发、断连、异常路径测试

需要优化的文件：
- `tests/FC-Revolution.Backend.Hosting.Tests/Endpoints/*.cs`
- `tests/FC-Revolution.Backend.Hosting.Tests/Streaming/*.cs`
- `tests/FC-Revolution.Backend.Hosting.Tests/WebSockets/*.cs`

大致内容：
- 增加并发读写快照测试、断连释放测试、慢客户端测试、无效 JSON / 未知动作测试、空订阅测试。
- 将现有 happy path 集成测试扩展到更有重构保护力的生命周期测试。
- 对 enhancement mode、音频重采样和 endpoint feature toggle 补专门验证。
