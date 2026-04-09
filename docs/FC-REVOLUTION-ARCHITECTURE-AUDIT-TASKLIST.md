# FC-Revolution 架构审计任务总表

说明：
- 本文档基于 2026-04-02 的项目结构审计结果整理。
- 任务节点统一使用 `「0.x」` 格式。
- 节点完成后，在节点末尾补充 `✅`。
- 本文档负责总控、排序与串联，不替代各方向的细化任务单。

## 当前进度

- 截至 `2026-04-02`
- 已完成：`7 / 7`（`0.1`、`0.2`、`0.3`、`0.4`、`0.5`、`0.6`、`0.7`）
- 进行中：无
- 待开始：无

## 执行顺序建议

1. 先收缩 `FC-Revolution.UI` 的 God Object 和预览链路。
2. 再整理 `FC-Revolution.Backend.Hosting` 的并发读模型与 WebSocket 热路径。
3. 然后拆 `FC-Revolution.Core` 中不直接位于时序热环的时间线持久化与缓存层。
4. 再推进 `FC-Revolution.Rendering` / `FC-Revolution.Storage` 的模块边界整理。
5. 最后处理 `NesConsole`、`Ppu2C02`、`Cpu6502` 这类时序敏感的大文件。
6. 全程同步补测试，不把测试强化放到最后一次性处理。

## 方向文档

- UI / LAN / 预览方向：
  - `docs/FC-REVOLUTION-UI-LAN-PREVIEW-TASKLIST.md`
- Backend / Hosting / WebSocket 方向：
  - `docs/FC-REVOLUTION-BACKEND-HOSTING-TASKLIST.md`
- Core / Timeline / Timing 方向：
  - `docs/FC-REVOLUTION-CORE-REFINEMENT-TASKLIST.md`
- Rendering / Storage 方向：
  - `docs/FC-REVOLUTION-RENDERING-STORAGE-TASKLIST.md`
- 测试与回归护栏方向：
  - `docs/FC-REVOLUTION-TEST-HARDENING-TASKLIST.md`

## 总任务节点

「0.1」建立本轮重构基线与模块优先级 ✅

「0.2」优先处理 `UI` 协调层和预览性能热点 ✅

「0.3」收敛 `Backend.Hosting` 的共享状态与流媒体热路径 ✅

「0.4」拆分 `Core` 的时间线持久化与快照缓存职责 ✅

「0.5」整理 `Rendering` / `Storage` 的工程边界与安全边界 ✅

「0.6」在进入时序敏感核心重构前补齐测试护栏 ✅

「0.7」再处理 `NesConsole` / `Ppu2C02` / `Cpu6502` 的结构性瘦身 ✅

## 节点说明

### 「0.1」建立本轮重构基线与模块优先级

大致内容：
- 统一将后续任务分成 `UI`、`Backend`、`Core`、`Rendering/Storage`、`Tests` 五个方向推进。
- 避免继续沿用“单文件过大就拆”的粗粒度标准，改为以职责边界、热路径和测试护栏为优先级依据。
- 将本轮工作从“功能开发”切换为“可维护性与性能治理”。

### 「0.2」优先处理 `UI` 协调层和预览性能热点

大致内容：
- `MainWindowViewModel` 与 `GameWindowViewModel` 仍是当前维护成本最高的两个入口。
- 预览加载、平滑播放、后台解码与 UI 线程切换已经构成真实的运行时风险。
- 这部分最容易继续膨胀，所以应排在所有结构治理任务的最前面。

### 「0.3」收敛 `Backend.Hosting` 的共享状态与流媒体热路径

大致内容：
- 后端宿主已经不再是单一巨型类，但共享可变读模型与 WebSocket 热路径仍然存在。
- 这一层的主要目标不是“继续拆更多文件”，而是把边界从“可运行”收缩到“可并发、可替换、可测”。

### 「0.4」拆分 `Core` 的时间线持久化与快照缓存职责

大致内容：
- 这部分不在逐指令执行热环上，适合先进行低风险结构性重构。
- 优先将持久化、缓存、序列化从大仓库式类中抽离，降低后续回归风险。

### 「0.5」整理 `Rendering` / `Storage` 的工程边界与安全边界

大致内容：
- `Rendering` 当前把通用渲染与平台后端放在同一工程里，扩大了构建和部署耦合。
- `Storage` 当前把资源布局、路径策略、遗留迁移和文件系统实现压在一个入口上，也缺少路径约束验证。

### 「0.6」在进入时序敏感核心重构前补齐测试护栏

大致内容：
- `UI` 对 LAN、预览源、资源清理的保护不足。
- `Backend` 缺少并发、异常、慢客户端、断连回收等测试。
- `Core` 缺少 `NesConsole` 多执行路径等价性与时序相关测试。

### 「0.7」再处理 `NesConsole` / `Ppu2C02` / `Cpu6502` 的结构性瘦身

大致内容：
- 这类文件虽然长，但不能按普通业务代码的方式直接拆。
- 在没有足够测试护栏前，只做读写分区、partial 化、帮助类型提取等低行为风险整理。
- 当前进展：`NesConsole` 已完成非 DMA 指令骨架统一、DMA gate 收口，以及 PPU 三拍 / NMI / `FrameComplete` 聚合统一；`Ppu2C02` 已完成寄存器 / VRAM / 地址镜像、快照 / 序列化、背景 pipeline、精灵评估等结构性 `partial` 拆分；`Cpu6502` 已完成寻址模式 / dispatch、Load/Store/Transfer/Stack、ALU/Compare/Inc-Dec、shifts / jump-branch / flags / misc 等结构性 `partial` 拆分。
- 涉及时序合并和执行流程统一的改动，必须在前置任务完成后进行。
