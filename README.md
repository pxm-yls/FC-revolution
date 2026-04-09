# FC-Revolution

FC-Revolution 是一个基于 `C# / .NET 10` 与 `Avalonia 11` 的 FC / NES 模拟器项目。  
它不只是一个单纯的桌面模拟器，而是朝着“`桌面宿主 + 可插拔核心 + 现代渲染 + 局域网点播系统 + 预览与资源管理`”方向演进的完整应用框架。

当前仓库仍在持续重构中，但已经具备比较完整的产品骨架：

- `NES / FC` 托管核心
- 桌面 UI 与 ROM 库管理
- 输入绑定与会话生命周期
- 预览资源与 FFmpeg 预览链路
- 局域网点播系统与网页手柄控制
- 状态快照、时间线、分支等能力抽象
- 面向未来多核心扩展的宿主架构

## 项目目标

这个项目的目标不是只把 `256x240` 像素画面放大显示，而是把经典 FC / NES 内容放进一个更现代的宿主体系里：

- 保留传统模拟器需要的核心准确性与可调试性
- 提供更完整的桌面产品能力，而不是只有“打开 ROM 然后运行”
- 为未来接入更多模拟器核心保留统一接口
- 在渲染层尝试更现代的 GPU 分层重绘思路
- 让本地运行、局域网点播、网页控制、资源管理处于同一套架构里

## 当前功能概览

### 1. NES / FC 模拟核心

当前核心实现位于 `src/FC-Revolution.Core/FC-Revolution.Core.FC`，已经包含这些主要模块：

- `CPU`：6502 指令与调度
- `PPU`：2C02 图形管线、背景与精灵渲染
- `APU`：音频生成
- `Bus`：总线读写与硬件联动
- `Input`：标准控制器输入
- `Mappers`：基于 family core 的 Mapper 架构
- `State / Replay / Timeline`：状态快照、回放、时间线相关基础设施
- `Debug`：调试态采集与内存读写能力

当前托管核心模块 `FC-Revolution.Core.Nes.Managed` 已通过统一宿主接口暴露以下能力：

- 视频帧输出
- 音频输出
- ROM 载入
- 输入 schema 与输入状态写入
- 存档 / 读档
- 时间旅行 / 时间线快照
- 调试内存、寄存器与反汇编能力
- NES 渲染状态导出

### 2. 桌面端 UI

桌面应用位于 `src/FC-Revolution.UI`，当前职责不只是“显示一帧画面”，还覆盖了相对完整的用户流程：

- ROM 库浏览与导航
- 启动 / 关闭模拟会话
- 输入绑定与键盘映射
- 预览播放与预热队列
- 资源导入、删除、清理与资源根目录管理
- 启动诊断、任务消息与通知
- 受管核心目录探测、注册与切换流程

从测试与模块拆分情况来看，`MainWindow` 已经演进成由多组 controller 协同完成的应用宿主，而不是把所有逻辑堆在单一 ViewModel 里。

### 3. 预览与多媒体链路

项目内置了 FFmpeg 运行时与工具资源，主要用于预览视频链路和相关媒体处理：

- 仓库内 `src/FC-Revolution.ffmpeg/runtimes` 保存应用所需运行时文件
- `src/FC-Revolution.ffmpeg/tools` 保存维护用途的 FFmpeg 工具文件
- UI 层有 `RawFrame` 与 `FFmpeg` 两条预览来源
- 预览链路包含预热、窗口缓存、清理、播放策略等控制点

### 4. 局域网点播系统与网页手柄

项目包含一套面向局域网场景的点播系统，配合网页手柄与内嵌后端运行，重点在 `FC-Revolution.Backend.Hosting` 与 `FC-Revolution.UI/Application`：

- 后端 API、会话查询与点播入口
- WebSocket 视频 / 音频串流传输
- 局域网房间 / LAN Arcade 运行时
- WebPad 网页前端与网页手柄接入
- 远程控制请求与控制权租约
- 会话预览图与 ROM 预览资源查询

其中独立后端入口 `src/FC-Revolution.Backend/Program.cs` 默认监听端口 `11778`。

### 5. 时间线、分支与状态快照

这个项目并不把“存档”只当成简单的 `save/load`：

- 核心抽象层提供 `CoreStateBlob`
- 时间旅行能力通过 `ITimeTravelService` 暴露
- 支持时间线缩略图、分支点、快照恢复
- 为后续 branch gallery、回溯和导出能力打基础

### 6. 可插拔多核心宿主

仓库已经不是写死为“只能运行一个 NES 核心”的形态，当前正在向可插拔多核心宿主演进：

- `FC-Revolution.Emulation.Abstractions` 定义统一核心接口
- `FC-Revolution.Emulation.Host` 负责核心发现、注册、探测和实例化
- 托管核心通过 `IManagedCoreModule` 暴露清单和工厂
- 当前默认核心是 `fc.nes.managed`
- 架构上已为未来 `native-cabi` 和多系统核心预留入口

这部分设计详见 `docs/FC-REVOLUTION-PLUGGABLE-CORE-ARCHITECTURE-PLAN.md`。

### 7. 现代渲染路线

项目的渲染目标不是传统“像素放大器”路径，而是尝试从 NES 元数据层做更现代的 GPU 重绘：

- 从 PPU 提取背景、精灵、调色板、运动信息
- 通过 `FC-Revolution.Rendering` 组织渲染抽象
- 当前重点实现平台是 macOS
- macOS 路线使用 `Metal + Avalonia NativeControlHost + P/Invoke`
- 后续规划包含 `MetalFX Spatial / Temporal`

这部分设计详见 `docs/RENDERING_PLATFORM_GUIDE.md` 与 `docs/MACOS_DEVELOPMENT_GUIDE.md`。

## 架构设计

整体上可以把当前工程理解为下面这几层：

```text
FC-Revolution.UI
    ├── 应用生命周期、界面、输入绑定、ROM 库、预览、局域网点播管理
    ├── 引用 Backend.Hosting / Rendering / Storage / Emulation.Host
    ▼
FC-Revolution.Backend.Hosting
    ├── HTTP / WebSocket / WebPad / 点播串流 / 远程控制
    ▼
FC-Revolution.Emulation.Host
    ├── 核心发现
    ├── 托管核心注册
    ├── 核心实例化
    ▼
FC-Revolution.Emulation.Abstractions
    ├── CoreManifest
    ├── IManagedCoreModule
    ├── IEmulatorCoreSession
    ├── CoreCapabilitySet
    ├── TimeTravel / Debug / Input 等抽象
    ▼
FC-Revolution.Core.* / 未来 Native Core
    ├── 当前 NES 托管核心
    └── 未来多系统核心
```

更细一点看，每个项目的职责大致如下：

- `FC-Revolution.UI`
  - Avalonia 桌面应用入口
  - 主窗口、游戏窗口、预览系统、输入与资源管理
- `FC-Revolution.Backend.Hosting`
  - 内嵌后端宿主
  - API、调试端点、局域网点播串流与 WebPad 页面资源
- `FC-Revolution.Backend`
  - 独立后端入口进程
- `FC-Revolution.Contracts`
  - UI 与后端之间共享的 DTO 与服务契约
- `FC-Revolution.Storage`
  - 资源根目录、对象存储、Bucket 布局与文件系统落盘策略
- `FC-Revolution.Emulation.Abstractions`
  - 核心能力、会话模型、状态模型、调试与时间旅行接口
- `FC-Revolution.Emulation.Host`
  - 托管核心探测、注册表、目录加载、默认核心宿主
- `FC-Revolution.Core.FC`
  - 当前 FC / NES 核心实现
- `FC-Revolution.Core.Nes.Managed`
  - 通过统一接口包装当前 NES 核心
- `FC-Revolution.Rendering`
  - 渲染抽象、PPU 元数据提取、平台渲染路径组织
- `FC-Revolution.Rendering.Metal`
  - macOS Metal 桥接与原生渲染支撑
- `FC-Revolution.ffmpeg`
  - 预览链路需要的 FFmpeg 运行时与工具资源

## 目录结构

```text
FC-Revolution/
├── docs/                          设计文档、任务清单、平台方案
├── src/
│   ├── FC-Revolution.UI/         桌面 UI
│   ├── FC-Revolution.Backend/    独立后端入口
│   ├── FC-Revolution.Backend.Hosting/
│   ├── FC-Revolution.Contracts/
│   ├── FC-Revolution.Storage/
│   ├── FC-Revolution.Emulation.Abstractions/
│   ├── FC-Revolution.Emulation.Host/
│   ├── FC-Revolution.Core/
│   │   ├── FC-Revolution.Core.FC/
│   │   ├── FC-Revolution.Core.FC.Managed/
│   │   └── FC-Revolution.Core.Sample.Managed/
│   ├── FC-Revolution.Rendering/
│   ├── FC-Revolution.Rendering.Metal/
│   └── FC-Revolution.ffmpeg/
└── tests/
    ├── FC-Revolution.Core.Tests/
    ├── FC-Revolution.Rendering.Tests/
    ├── FC-Revolution.Backend.Hosting.Tests/
    └── FC-Revolution.UI.Tests/
```

## 开发环境

当前仓库最适合的开发环境是：

- `.NET 10 SDK`
- `Avalonia 11`
- macOS Apple Silicon
- Xcode Command Line Tools

虽然架构上对多平台做了预留，但当前“现代渲染主路径”明显优先面向 macOS。

## 构建与运行

### 构建整个解决方案

```bash
dotnet build FC-Revolution.slnx
```

### 启动桌面应用

```bash
dotnet run --project src/FC-Revolution.UI/FC-Revolution.UI.csproj
```

### 启动独立后端

```bash
dotnet run --project src/FC-Revolution.Backend/FC-Revolution.Backend.csproj
```

### 运行测试

```bash
dotnet test FC-Revolution.slnx
```

如果你只想验证某一层，建议按项目分开跑，例如：

```bash
dotnet test tests/FC-Revolution.Core.Tests/FC-Revolution.Core.Tests.csproj
dotnet test tests/FC-Revolution.Rendering.Tests/FC-Revolution.Rendering.Tests.csproj
dotnet test tests/FC-Revolution.Backend.Hosting.Tests/FC-Revolution.Backend.Hosting.Tests.csproj
dotnet test tests/FC-Revolution.UI.Tests/FC-Revolution.UI.Tests.csproj
```

## 当前状态说明

这个仓库已经不是“玩具级 FC 模拟器”：

- 有完整的核心、UI、后端、存储、渲染、测试分层
- 有明确的多核心宿主重构方向
- 有比较重的产品化能力，例如预览、资源管理、局域网点播系统与 WebPad
- 有现代渲染路线和平台化规划

同时它也仍然是一个持续演进中的工程：

- `FC-Revolution.Core` 目录结构仍在继续整理
- 多核心宿主与核心包管理仍在推进
- 现代渲染方案当前以 macOS 为主
- 一部分功能已经完成，一部分还处在重构与收口阶段

如果你想快速理解这个项目，推荐阅读顺序是：

1. `README.md`
2. `docs/FC-REVOLUTION-PLUGGABLE-CORE-ARCHITECTURE-PLAN.md`
3. `docs/RENDERING_PLATFORM_GUIDE.md`
4. `docs/MACOS_DEVELOPMENT_GUIDE.md`
5. `src/FC-Revolution.UI`
6. `src/FC-Revolution.Core/FC-Revolution.Core.FC`

## 一句话总结

FC-Revolution 当前可以理解为：

> 一个以 `NES / FC` 为落地核心、以 `Avalonia + .NET 10` 为宿主、以 `局域网点播系统 / WebPad / 预览 / 时间线 / 现代渲染 / 可插拔核心` 为扩展方向的现代化模拟器工程。
