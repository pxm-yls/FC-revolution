# FC-Revolution macOS Spatial / Temporal 后续执行清单

> **版本**: 1.2 | **更新**: 2026-04-07
> **适用范围**: macOS Apple Silicon (M1+)；承接 Phase 2.2 已完成后的后续渲染工作
> **用途**: 面向工程执行，拆解 MetalFX Spatial、MetalFX Temporal 及其配套验证、运行 hygiene、收口标准
> **当前执行进度**: Milestone A 已完成 18 / 26 项（A1: 5 / 5，A2: 5 / 5，A3: 4 / 4，A4: 2 / 5，A6: 2 / 4）；Milestone B 已完成 18 / 29 项（B1: 4 / 4，B2: 4 / 4，B3: 4 / 5，B4: 4 / 4，B7: 2 / 4）
> **2026-03-31 / 2026-04-07 本轮修复记录**: 已将 Spatial 的“渲染输出分辨率”与“窗口显示尺寸”解耦，并新增 `1080p / 1440p / 4K` 输出档位；F1 诊断与运行日志现区分 `内部 / 渲染 / drawable / 显示 / host / layer` 多层尺寸，同时补充 `MacMetalViewHost` 几何日志。新增独立几何日志 `/tmp/fc-revolution-geometry.log`（可用 `FC_REVOLUTION_GEOMETRY_LOG` 覆盖路径），当前会记录 `program / game-session / game-window / mac-metal-host` 四层尺寸链路，便于继续定位截断来源。2026-04-07 已复核 Spatial 相关自动化与残留进程收口，定向测试 `MotionVectorGeneratorTests` / `RenderDataExtractorTests` / `MotionTextureBuilderTests` / `MacMetalOffscreenRendererTests` / `MacMetalStabilityTests` / `GameWindowViewportDiagnosticsControllerTests` 通过；代表 ROM 的 3 条离屏 diff 用例仍因 ROM 基线缺失处于 skip。`scripts/fc-clean-residual-processes.sh` 已修复自匹配 helper 进程误报。Temporal 已推进到 B1 + B2：motion vector 现已同时表达背景 scroll 位移与精灵位移，并明确 `previous -> current`、screen-space、`scaleX / scaleY` 语义；此外已生成 CPU 侧 `RG16Float` 语义的 full-frame motion texture，默认背景区域填充背景 vector，精灵区域按当前渲染语义覆盖 sprite vector，并新增 offscreen/native 最小验证钩子以证明 motion texture payload 能到达 native 层。2026-04-07 已补齐 Temporal B1 的内部渲染分辨率缩放规则：motion vector 现会随 render resolution scale 正确换算，避免模式切换或输出档位变化后出现方向正确但尺度错误的问题。相关自动化已覆盖 `MotionVectorGenerator` 与 `RenderDataExtractor` 的缩放语义，B1 现已完成 4 / 4。2026-04-07 继续补齐 B3 / B4 的 reset 生命周期与回退语义：ROM 重载、快速读档、超分模式切换以及 presenter 重建 / 窗口重开现已统一走 `MacMetalTemporalResetReason`，对应 reset 原因会回传到 UI 诊断与启动 / 运行日志；相关自动化已覆盖读档、ROM 重载、窗口重开后的稳定性。当前宿主层已固定 Temporal history reset 规则：ROM / 快速读档 / 时间线回溯或 seek / 分支载入 / 模式切换会重置历史，暂停 / 继续不会重置；游戏内过场或场景切换若缺乏可靠宿主信号，暂不额外触发 reset。请求 `Temporal` 时，presenter 现会保留 `requested / effective / fallback reason` 诊断语义；由于 on-screen Temporal runtime 仍未接通，当前会先回退到 `Spatial`，若回退链也不可用或运行失败，再回退到 `无超分`。新增 `RequestedPathUnavailable` 回退原因以及对应的 UI 诊断 / 稳定性测试。Temporal 仍未接入 on-screen / MetalFX runtime，新的手工画质 / FPS / ROM 烟测尚未完成。

---

## 1. 背景与使用方式

- [x] Phase 2.2 已完成：`GameWindow` 主视口已优先走 `LayeredFrameData` 的背景/精灵 GPU 分层渲染。
- [x] GPU vs `ReferenceFrameRenderer` 自动化 diff 已建立，`FC-Revolution.Rendering.Tests` 当前基线为 32 / 32 通过。
- [ ] 本清单只覆盖 macOS 后续路线，不扩展到 Windows / Linux / iOS / Android。
- [ ] 执行顺序固定：先完成 Spatial，再进入 Temporal；Temporal 不得先行落地。
- [ ] 每个里程碑结束时都必须执行“测试后收口”，确认没有残留 `FC-Revolution.UI` / `dotnet` / `testhost` 进程。

---

## 2. 当前基线

- [x] 现有 `MacMetalPresenter` / `MacMetalBridge` / `FCRMetalBridge.m` 渲染链路已稳定可用。
- [x] 现有 native bridge 已支持分层渲染与离屏读回，能够做 GPU/Reference 像素 diff 回归。
- [x] 现有软件 `Image` 回退路径仍保留，允许在 Metal 或 capability 不满足时回退。
- [x] `libFCRMetalBridge.dylib` 已通过共享 targets 自动复制到 UI 与测试输出目录。
- [x] 启动日志与运行期日志已具备可观测性，可追踪 `program` / `app` / `main-vm` / `game-session` / `game-window` 阶段。
- [x] Spatial 已接入到实际运行时渲染链路，并复用现有 `MacMetalPresenter` / `FCRMetalBridge.m` presenter 架构。
- [ ] Temporal 尚未接入到实际运行时渲染链路。

### 接口层约束

- Spatial / Temporal 必须扩展现有 `MacMetalPresenter` 与 `FCRMetalBridge.m`，不得新建第二套 macOS presenter 架构。
- CPU -> native bridge 输入继续以 `LayeredFrameData` 为主，不为 Spatial / Temporal 新建并行 UI 显示链。
- 本阶段默认不新增运行时公共 API；若必须新增类型或配置，优先放在现有 capability / policy / settings 路径中，并尽量保持 `internal` 或最小公开面。
- 统一回退顺序固定为：`Temporal -> Spatial -> 无超分`。
- 运行时切换与异常恢复必须以“可回退、可重开、可重复运行”为优先级，不追求一次性接通所有高级配置项。

---

## 3. Milestone A：Spatial 执行清单

### A1. Native bridge 与渲染链路

- [x] 在现有 `FCRMetalBridge.m` presenter 路径中接入 `MTLFXSpatialScaler`。
- [x] 明确并固化输出链路：`内部 render target -> Spatial scaler -> CAMetalLayer drawable.present`。
- [x] 复用现有背景 / 精灵 / 合成 render target，不新建第二套窗口宿主或 presenter。
- [x] 在窗口尺寸变化、显示器 scale 变化、输出分辨率变化时，正确重建 Spatial 相关纹理与 scaler 资源。
- [x] 保证无超分路径仍可独立运行，Spatial 初始化失败时不得拖垮整个窗口。

### A2. Capability / Policy / 回退策略

- [x] 增加 Spatial capability gating：仅在 Apple Silicon 且满足目标 macOS 版本时启用。
- [x] 在运行时配置中明确区分 `无超分` 与 `Spatial` 模式。
- [x] Spatial 不可用、初始化失败、运行中异常时，自动回退到无超分路径。
- [x] 启动日志与运行日志中记录当前超分路径和回退原因，便于定位 capability 或资源初始化问题。
- [x] 固化输出分辨率策略：内部渲染分辨率、输出分辨率、显示目标分辨率三者关系必须在日志或诊断信息中可见。

### A3. UI / 配置 / 切换稳定性

- [x] 在现有配置或诊断入口中暴露当前渲染模式：`无超分` / `Spatial`。
- [x] 保证运行中或重开窗口时切换 `无超分 <-> Spatial` 不崩溃。
- [x] 验证 ROM 重载、窗口关闭重开、读档后再切换渲染模式时路径稳定。
- [x] 验证 Spatial 模式下软件回退仍可用，且 UI 不出现黑屏或假死。

### A4. 自动化与手工验证

- [x] 增加至少一条围绕 Spatial 输出尺寸 / 路径稳定性的自动化检查。
- [ ] 手工验证启用 Spatial 后输出分辨率与目标显示分辨率一致。
- [ ] 手工对比 `Spatial` 与 `无超分`，确认无明显画质劣化。
- [ ] 在 M1 基础款上记录 frame time / FPS，目标为稳定 60fps，单帧预算不超过 16.7ms。
- [x] 验证 `无超分 <-> Spatial` 来回切换、ROM 重载、窗口重开均无崩溃。

### A5. 推荐手工验证 ROM

- [ ] `Super Mario Bros`：基础场景、HUD 清晰度、静态边缘锐利度。
- [ ] `冒险岛3`：横向滚动、背景连续性、滚动时画质稳定性。
- [ ] `忍者神龟2`：复杂场景、精灵叠加、战斗时整体稳定性。

### A6. 测试后收口

- [x] 执行 `scripts/fc-clean-residual-processes.sh --check`（或等效 `pgrep` / `ps` 检查）确认残留进程状态。
- [ ] 若发现残留 `FC-Revolution.UI`，优先正常退出；必要时执行 `pkill -f "FC-Revolution.UI"`。
- [ ] 若发现残留 `testhost`，必要时执行 `pkill -f "testhost"`。
- [x] 确认无残留进程后，再进入下一轮实际运行或手工烟测。

---

## 4. Milestone B：Temporal 执行清单

### B1. Motion vector 数据升级

- [x] 将 motion vector 从“仅精灵差分”升级为“背景 scroll 位移 + 精灵位移”的完整方案。
- [x] 明确并统一 motion vector 方向、单位、缩放基准，确保符合 MetalFX Temporal 输入要求。
- [x] 背景位移基于 scroll 变化量生成，精灵位移基于帧间 sprite 位置变化生成。
- [x] 明确内部渲染分辨率变化时的 vector scale 规则，避免模式切换后方向或尺度错误。

### B2. 全帧 motion texture 生成

- [x] 生成覆盖整个帧的 `RG16Float` motion vector 纹理，而不是仅保留 CPU 元数据。
- [x] 默认背景区域填充背景 scroll vector，精灵覆盖区域写入对应 sprite vector。
- [x] 明确精灵重叠区域、越界区域、左 8 像素 mask 区域的 motion texture 行为。
- [x] 为 motion texture 增加最小可验证路径，确保可做离屏检查或关键日志验证。

### B3. History / reset 生命周期

- [ ] 在 native bridge 中补齐 Temporal 所需的 history texture / reset flag 管理。
- [x] ROM 加载时强制 reset Temporal history。
- [x] 读档时强制 reset Temporal history。
- [x] 场景跳切、过场结束、暂停菜单进出等可能造成历史失真的节点，明确是否 reset，并写成固定规则。
- [x] 模式切换 `Temporal <-> Spatial <-> 无超分` 时重置历史，避免跨模式复用旧 history。

### B4. Capability / 回退策略

- [x] 增加 Temporal capability gating：仅在支持的 Apple Silicon 与目标 macOS 版本启用。
- [x] Temporal 初始化失败或运行中异常时，自动回退到 Spatial；Spatial 也不可用时再回退到无超分。
- [x] 启动日志与运行日志记录当前使用的 upscale 路径与 reset 原因。
- [x] 读档、重载 ROM、窗口重开后，确保回退策略仍然稳定可重复。

### B5. 画质 / 性能验证

- [ ] `Super Mario Bros` / `冒险岛3`：横向滚动场景无明显拖影。
- [ ] `忍者神龟2`：快速精灵与高动态场景无明显鬼影。
- [ ] 读档、ROM 重载、场景跳切后第 1-3 帧无明显残影。
- [ ] 对比 `Temporal` 与 `Spatial`，确认静态清晰度或时序稳定性存在明确提升。
- [ ] 在至少一档高于 M1 基础款的芯片上记录 frame time / FPS，并确认性能预算可接受。

### B6. 推荐手工验证 ROM / 场景

- [ ] `Super Mario Bros`：横向滚动、管道进出、读档后首帧。
- [ ] `冒险岛3`：持续横向滚动、背景连续细节。
- [ ] `忍者神龟2`：战斗高动态精灵场景、快速移动与遮挡。

### B7. 测试后收口

- [x] 执行 `scripts/fc-clean-residual-processes.sh --check`（或等效 `pgrep` / `ps` 检查）确认残留进程状态。
- [ ] 若发现残留 `FC-Revolution.UI`，优先正常退出；必要时执行 `pkill -f "FC-Revolution.UI"`。
- [ ] 若发现残留 `testhost`，必要时执行 `pkill -f "testhost"`。
- [x] 确认无残留进程后，再进行下一轮实际运行、回归测试或性能采样。

---

## 5. Cross-cutting 验证与运行 hygiene

### 推荐验证顺序

1. 先运行渲染相关自动化测试，确认基础回归未破坏。
2. 执行一次残留进程检查，避免旧实例占用输出产物。
3. 使用启动日志运行 UI，定位 `program` / `app` / `main-vm` / `game-session` / `game-window` 阶段。
4. 进行手工烟测与指定 ROM 验证。
5. 完成后再次执行残留进程检查与清理，再进行下一轮开发或实际运行。

### 启动日志推荐命令

```bash
FC_REVOLUTION_STARTUP_DEBUG=1 \
FC_REVOLUTION_STARTUP_LOG=/tmp/fc-revolution-startup.log \
dotnet run --project /Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.UI/FC-Revolution.UI.csproj
```

### 几何日志推荐命令

```bash
FC_REVOLUTION_GEOMETRY_LOG=/tmp/fc-revolution-geometry.log \
dotnet run --project /Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.UI/FC-Revolution.UI.csproj

rg -n "game-session|game-window|mac-metal-host|render path|viewport layout" /tmp/fc-revolution-geometry.log
```

### 重点观察日志阶段

- `program`：进程是否真正进入 UI 启动。
- `app`：Avalonia 生命周期是否完整进入。
- `main-vm`：主窗口初始化是否完成。
- `game-session`：游戏会话创建、窗口显示、异常回退是否发生。
- `game-window`：macOS presenter 启用、回退或失败原因。

### 残留进程与锁占用故障征候

- 游戏窗口无法正常拉起。
- 点击运行后看似无响应，但无明确崩溃提示。
- 新进程复用旧实例，导致观察到的行为与当前代码不一致。
- 输出目录中的程序集 / PDB / dylib 被旧进程占用，影响重新运行或调试。

### 常用检查与清理命令

```bash
# 优先使用仓库脚本检查残留 UI / testhost / dotnet 进程
scripts/fc-clean-residual-processes.sh --check

# 某些 macOS 环境里 pgrep 可能不可用，可回退到 ps + rg
ps -ax -o pid=,ppid=,etime=,command= | rg "FC-Revolution.UI|testhost|dotnet"

# 使用仓库脚本结束残留进程
scripts/fc-clean-residual-processes.sh --kill

# 结束残留 testhost 进程
pkill -f "testhost"
```

### 手工验证 ROM 基线

- `Super Mario Bros`
- `冒险岛3`
- `忍者神龟2`

---

## 6. 完成定义

### Milestone A：Spatial Exit Criteria

| 项目 | 完成标准 |
|------|---------|
| 运行时链路 | 已稳定接入 `内部 render target -> Spatial -> drawable present` |
| Capability / 回退 | Apple Silicon + macOS gating 生效；异常时可回退到无超分 |
| 切换稳定性 | `无超分 <-> Spatial`、窗口重开、ROM 重载、读档均不崩溃 |
| 画质验证 | Spatial 相比无超分无明显劣化 |
| 性能验证 | M1 基础款稳定 60fps，单帧预算不超过 16.7ms |
| 收口要求 | 测试结束后确认无残留 `FC-Revolution.UI` / `testhost` 进程 |

### Milestone B：Temporal Exit Criteria

| 项目 | 完成标准 |
|------|---------|
| Motion vector | 已包含背景 scroll + 精灵位移，方向与尺度统一 |
| Motion texture | 已生成覆盖整帧的 `RG16Float` motion vector 纹理 |
| History / reset | ROM 加载、读档、场景跳切、模式切换时 reset 规则固定且可验证 |
| 回退策略 | `Temporal -> Spatial -> 无超分` 回退链稳定 |
| 画质验证 | 横向滚动无明显拖影，快速精灵无明显鬼影，Temporal 明显优于 Spatial |
| 性能验证 | 在至少一档高于 M1 基础款芯片上性能可接受 |
| 收口要求 | 测试结束后确认无残留 `FC-Revolution.UI` / `testhost` 进程 |

### 全部完成前不得省略的最终检查

- [ ] 自动化测试通过。
- [ ] 启动日志中无未解释的 `game-session` / `game-window` 错误。
- [ ] Spatial 与 Temporal 手工烟测完成。
- [ ] 至少完成一轮读档 / 重载 / 模式切换 / 窗口重开稳定性验证。
- [ ] 最终确认无残留 `FC-Revolution.UI` / `testhost` / 异常 `dotnet` 进程。

---

*文档结束*
