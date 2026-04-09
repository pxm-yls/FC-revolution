# FC-Revolution.Core 结构整理任务清单

说明：
- 任务节点统一使用 `「3.x」` 格式。
- 节点完成后，在节点末尾补充 `✅`。
- 本文档聚焦 `Core` 里的两类问题：低风险结构拆分，以及高风险时序敏感整理。

## 当前进度

- 截至 `2026-04-02`
- 已完成：`6 / 6`（`3.1`、`3.2`、`3.3`、`3.4`、`3.5`、`3.6`）
- 进行中：无
- 待开始：无
- 下一步建议：回到仍未收口的 `UI` / `LAN` / 预览任务

## 任务节点

「3.1」优先拆分时间线持久化仓库的多职责边界 ✅

「3.2」优化时间线缓存与快照序列化分配 ✅

「3.3」统一 `NesConsole` 的多执行路径调度逻辑 ✅

「3.4」对 `Ppu2C02` 做结构性 partial 化整理 ✅

「3.5」对 `Cpu6502` 做结构性 partial 化整理 ✅

「3.6」补齐核心时序与 stepping 测试 ✅

## 节点说明

### 「3.1」优先拆分时间线持久化仓库的多职责边界

需要优化的文件：
- `src/FC-Revolution.Core/Timeline/Persistence/TimelineRepository.cs`

大致内容：
- 当前仓库类同时负责 manifest 读写、quick save、branch point、preview node、branch tree 回填等职责。
- 建议拆成 manifest store、snapshot store、preview node store、branch tree loader 等更小组件。
- 这是 `Core` 里当前最适合先动手的低风险结构重构点。

### 「3.2」优化时间线缓存与快照序列化分配

需要优化的文件：
- `src/FC-Revolution.Core/Timeline/TimelineCache.cs`
- `src/FC-Revolution.Core/State/StateSnapshotSerializer.cs`
- `src/FC-Revolution.Core/State/StateSnapshotData.cs`

大致内容：
- 清理 warm cache 的线性淘汰、重复压缩分配和恢复解压分配。
- 评估是否引入环形 warm 区、池化缓冲区或更清晰的热/温快照策略。
- 目标是降低回溯、时间线拖拽和快照恢复时的 GC 压力。

### 「3.3」统一 `NesConsole` 的多执行路径调度逻辑

需要优化的文件：
- `src/FC-Revolution.Core/NesConsole.cs`

大致内容：
- 当前 `RunFrame`、`StepClock`、`StepInstruction` 存在重复调度逻辑，且 CPU cycle 记账路径已出现分叉。
- 先抽出统一的内部调度核心，再让三种公开 API 复用同一套执行骨架。
- 当前进展：已完成非 DMA instruction skeleton 统一、`RunFrame` / `StepInstruction` 的 DMA gate 收口，以及 PPU 三拍 / NMI / `FrameComplete` 聚合 helper 统一。`StepClock` 剩余差异主要体现为已被测试锁定的公开 API 语义，不再继续作为 `3.3` 的独立收口片。
- 该任务高风险，必须排在测试强化之后。

### 「3.4」对 `Ppu2C02` 做结构性 partial 化整理

需要优化的文件：
- `src/FC-Revolution.Core/PPU/Ppu2C02.cs`

大致内容：
- 该类虽然很长，但仍然是单一领域，不建议先做行为重写。
- 可按寄存器访问、VRAM 读写、背景抓取、精灵评估、像素输出、调试/序列化拆分到多个 partial 或帮助类型。
- 当前进展：已完成寄存器访问 / VRAM / 地址镜像第一片、调试快照 / 状态序列化第二片、背景抓取 / scroll / shifter pipeline 第三片，以及精灵评估第四片 `partial` 拆分；`Clock()` 主体保留为单一热路径入口。
- 同时减少热路径里重复访问适配器属性的次数。

### 「3.5」对 `Cpu6502` 做结构性 partial 化整理

需要优化的文件：
- `src/FC-Revolution.Core/CPU/Cpu6502.cs`

大致内容：
- 保持 `ExecuteStep`、中断入口和主状态集中，其余部分按寻址模式、指令实现、opcode 初始化拆开。
- 当前进展：已完成寻址模式 helpers 与 opcode lookup / dispatch 的第一片 `partial` 拆分，Load/Store/Transfer/Stack 指令分组的第二片 `partial` 拆分，ALU/Compare/Inc-Dec 指令分组的第三片 `partial` 拆分，以及 shifts / jump-branch / flags / misc 的第四片 `partial` 拆分；`ExecuteStep`、中断入口、状态序列化和基础 helpers 保留在主文件。
- 目标是降低文件密度与回归面，而不是先追求 CPU 执行性能重写。

### 「3.6」补齐核心时序与 stepping 测试

需要优化的文件：
- `tests/FC-Revolution.Core.Tests/*.cs`
- 建议新增 `tests/FC-Revolution.Core.Tests/NesConsoleSteppingTests.cs`
- 建议新增 `tests/FC-Revolution.Core.Tests/PpuTimingTests.cs`
- 建议新增 `tests/FC-Revolution.Core.Tests/CpuOpcodeCoverageTests.cs`

大致内容：
- 补上 `RunFrame` / `StepClock` / `StepInstruction` 等价性与差异性测试。
- 增加 DMA stall、IRQ/NMI 顺序、PPU vblank / odd frame / sprite 行为、CPU 页跨越和中断边界测试。
- 让后续核心整理有可依赖的回归护栏。
