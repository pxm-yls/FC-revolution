# Mapper Family Roadmap

## 目标

后续新增 mapper 不再直接照搬 FCEUX 的“一个 board 文件 = 一个 mapper 文件”组织方式，而是按当前项目自己的新架构落地：

1. `RomDescriptor`
2. `MapperProfile`
3. `MapperCartridge`
4. `MapperCore`
5. 可选能力接口

核心原则：

- FCEUX 的 `Init` 函数映射成这里的 `MapperProfile`
- FCEUX 的共享 board 文件映射成这里的 `MapperCore family`
- FCEUX 的全局状态映射成 core 的实例字段
- FCEUX 的回调钩子映射成可选能力接口，不直接复制其全局回调模型
- CRC 修正、IPS 自动补丁、NES2 参数读取属于装载层，不混进 mapper 逻辑

## 新架构分层

### 1. RomDescriptor

职责：

- 解析 iNES/NES2 头
- 计算 ROM CRC32
- 提供装载后的真实参数
- 应用兼容性修正

建议字段：

- `MapperNumber`
- `Submapper`
- `PrgRomSize`
- `ChrRomSize`
- `PrgRamSize`
- `ChrRamSize`
- `HasBattery`
- `HasTrainer`
- `Mirroring`
- `Crc32`
- `Overrides`

### 2. MapperProfile

职责：

- 描述某个 mapper 编号应进入哪个 family
- 提供 family 初始化参数

建议字段：

- `MapperNumber`
- `Name`
- `Family`
- `PrgRamSize`
- `ChrRamSize`
- `Flags`
- `Settings`

### 3. MapperCartridge

职责：

- 统一实现 `ICartridge`
- 内部持有一个 `MapperCore`
- 把 profile、descriptor、公共内存和可选能力粘合到一起

### 4. MapperCore

职责：

- 只处理硬件行为
- 不处理 ROM 头修正
- 不处理文件补丁
- 不暴露系统外的全局状态

### 5. 可选能力接口

当前已预留：

- [IMapperCapabilities.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Core/Mappers/IMapperCapabilities.cs)
  - `ICpuCycleDrivenMapper`
  - `IPpuAddressObserver`
  - `IPpuNametableProvider`

用途：

- `ICpuCycleDrivenMapper`
  解决 VRC 系列、JY90 这类需要按 CPU 周期推进的核心
- `IPpuAddressObserver`
  解决 A12 边沿、PPU fetch 观测类核心
- `IPpuNametableProvider`
  解决 nametable 指向 CHR-ROM/CHR-RAM 的板子

## 当前缺失 Mapper 的家族归类

基于当前缺失队列：

- `15`
  - 家族：`Multicart15Core`
  - 是否可合并：可以独立成小 family，不建议硬并
  - 前置依赖：无

- `25`
  - 家族：`Vrc24Core`
  - 可合并对象：`21/22/23/25`
  - 前置依赖：`ICpuCycleDrivenMapper`

- `68`
  - 家族：`Sunsoft4Core`
  - 可合并对象：`UNIF NTBROM`
  - 前置依赖：`IPpuNametableProvider`

- `73`
  - 家族：`Vrc3Core`
  - 可合并对象：基本独立
  - 前置依赖：`ICpuCycleDrivenMapper`

- `74`
  - 家族：`Mmc3VariantCore`
  - 可合并对象：大量 MMC3 变体
  - 前置依赖：无

- `87`
  - 家族：`DiscreteLatchValueCore`
  - 可合并对象：`8/11/29/38/70/78/86/87/89/94/101/107/113/140/152/180/184/203/218/240/241`
  - 前置依赖：无

- `90`
  - 家族：`Jy90Core`
  - 可合并对象：`90/209/211`
  - 前置依赖：`ICpuCycleDrivenMapper`、`IPpuAddressObserver`、`IPpuNametableProvider`

- `163`
  - 家族：`Nanjing163FamilyCore`
  - 可合并对象：`163/164/162`
  - 前置依赖：建议接入 `ICpuCycleDrivenMapper` 或更细的扫描线/PPU 观察

- `164`
  - 家族：`Nanjing163FamilyCore`
  - 可合并对象：`163/164/162`
  - 前置依赖：无

- `240`
  - 家族：`DiscreteLatchValueCore`
  - 可合并对象：同上
  - 前置依赖：无

- `242`
  - 家族：`AddressLatchCore`
  - 可合并对象：`58/59/61/92/174/200/201/202/204/212/213/214/217/227/229/231/242/261`
  - 前置依赖：无

- `245`
  - 家族：`Mmc3VariantCore`
  - 可合并对象：大量 MMC3 变体
  - 前置依赖：无
  - 附加注意：要预留 CRC 兼容修正规则

- `246`
  - 家族：`RegisterWindow246Core`
  - 可合并对象：当前看更适合独立小 family
  - 前置依赖：无

## 全局可复用家族建议

结合 [FCEUX_mapper_list.md](/Users/pxm/Desktop/Cs/FC/例子/FCEUX_mapper_list.md)，未来值得长期维护的 family 如下。

### A. MMC1 Family

适合吸收：

- `1`
- `105`
- `155`
- `171`
- 各类 UNIF `SAROM/SBROM/SCROM/SEROM/SGROM/SKROM/SL1ROM/SLROM/SNROM/SOROM`

前置补丁：

- `RomDescriptor` 读取 NES2 WRAM/电池 WRAM

### B. MMC3 Variant Family

适合吸收：

- `4`
- `12/37/44/45/47/49/52/74/76/114/115/118/119/134/165/191/192/194/195/196/197/198/205/245/249/250/254/361/366/406`

前置补丁：

- CRC override
- profile 参数化
- 必要时 CHR-RAM 变体支持

### C. Discrete Latch Value Family

适合吸收：

- `8/11/29/38/70/78/86/87/89/94/101/107/113/140/152/180/184/203/218/240/241`

前置补丁：

- 无

### D. Address Latch Family

适合吸收：

- `58/59/61/92/174/200/201/202/204/212/213/214/217/227/229/231/242/261`

前置补丁：

- 无

### E. Konami VRC Family

建议拆成三个 family：

- `Vrc24Core`
  - `21/22/23/25`
- `Vrc3Core`
  - `73`
- `Vrc6Core`
  - `24/26`
- `Vrc7Core`
  - `85`

前置补丁：

- `ICpuCycleDrivenMapper`
- `IExtraAudioChannel` 用于 VRC6/VRC7

### F. Nanjing / Chinese Original Family

适合吸收：

- `162/163/164`
- `FS304`

前置补丁：

- 可能需要 mid-frame CHR 切换能力
- 建议预留 PPU 观察接口

### G. Nametable Override Family

适合吸收：

- `68`
- `90/209/211`
- 后续其他 ROM-backed nametable boards

前置补丁：

- `IPpuNametableProvider`

### H. 高复杂度系统型 Family

需要单独家族，不建议过早合并：

- `Bandai + EEPROM`
- `MMC5`
- `OneBus`
- `Coolboy/Coolgirl`
- `Sachen`
- `FK23C`

这些通常除了 mapper 行为，还会牵涉：

- EEPROM/Flash
- 扩展音频
- 特殊 CHR-RAM 容量
- ROM 不规则大小
- 外设输入

## 必做前置补丁

这些补丁建议先做，再开始批量补 mapper。

### Patch-00: 家族化基础骨架

内容：

- `MapperProfile`
- `MapperCartridge`
- `MapperCore`
- `MapperCoreFactory`

要求：

- 不改变现有 `ICartridge` 对外接口
- 先让 `0-4` 迁移到家族骨架

### Patch-01: CPU 周期桥

内容：

- 在 `NesConsole` 主循环中，每 CPU 周期调用可选的 `ICpuCycleDrivenMapper`

原因：

- 当前 [NesConsole.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Core/NesConsole.cs#L117) 到 [NesConsole.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Core/NesConsole.cs#L122) 只推进 APU 和扩展音频
- 未来 `25/73/90` 会依赖这一层

### Patch-02: PPU Nametable 桥

内容：

- 在 PPU `$2000-$3EFF` 访问路径中先询问 `IPpuNametableProvider`
- 若 mapper 不接管，再退回内部 `Vram + MirrorNametable()`

原因：

- 当前 [Ppu2C02.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Core/PPU/Ppu2C02.cs#L154) 到 [Ppu2C02.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Core/PPU/Ppu2C02.cs#L181) 只支持标准镜像
- `68/90` 不补这层就不准

### Patch-03: PPU 地址观察桥

内容：

- 在 PPU pattern/nametable fetch 或统一 `ReadVram` 路径上通知 `IPpuAddressObserver`

原因：

- `90` 这类核心不能只靠 `SignalScanline()`

### Patch-04: ROM 兼容修正规则

内容：

- 引入 `RomCompatibilityOverride`
- 允许按 CRC 覆盖：
  - `MapperNumber`
  - `Mirroring`
  - 特殊 flags

原因：

- 当前缺失队列里 `245` 已存在 `ines-correct.h` 命中项
- 这属于装载层问题，不应塞进 mapper core

### Patch-05: NES2 参数提升

内容：

- 把当前简单 `InesHeader` 提升为 `RomDescriptor`
- 补 `submapper`
- 补 `wram/vram/battery` 容量

原因：

- FCEUX 清单里大量 board 依赖这些参数

## 当前缺失集的推荐实施顺序

### Phase-1: 先拿下高收益、低依赖

目标：

- `245`
- `164`
- `74`
- `15`
- `87`
- `240`
- `242`
- `246`

特点：

- 不依赖 nametable override
- 不强依赖 PPU 地址观察
- 覆盖当前失败列表的大部分标题

### Phase-2: 加 CPU 周期桥后补 VRC/Nanjing

目标：

- `73`
- `25`
- `163`

### Phase-3: 加 nametable override

目标：

- `68`

### Phase-4: 加 PPU 地址观察

目标：

- `90`

## 未来补丁更新参考方案

以后新增 mapper，建议严格按下面的补丁粒度推进。

### 模板 A: 纯 profile 扩容

适用：

- 只是现有 family 的新编号或轻微参数差异

步骤：

1. 在 `MapperProfileRegistry` 加 profile
2. 补 family 参数
3. 加最小 ROM 测试
4. 加状态序列化测试

### 模板 B: 现有 family 的变体扩展

适用：

- 行为主体相同，但 bank 公式、镜像、副寄存器略有差异

步骤：

1. 给 family 增加 `Settings`
2. 在 core 内新增分支点，但只允许围绕一个家族模型扩展
3. 把差异封装成局部方法，不把编号判断散落到 `CpuRead/PpuRead`
4. 用 2 个以上 ROM 做回归

### 模板 C: 新 family 引入

适用：

- 新硬件协议，不适合塞进现有 family

步骤：

1. 先判断它需要哪些系统桥
2. 如果桥不存在，先独立做前置补丁
3. 再新增 `MapperCore`
4. 再新增 profile
5. 最后补 ROM 和状态测试

### 模板 D: 装载器修正补丁

适用：

- ROM 头错误
- CRC 专用 mapper/mirroring 覆盖

步骤：

1. 不改 mapper core
2. 在 compatibility override 表中加规则
3. 加针对该 CRC 的加载测试

## 新增 Mapper 的通用检查单

每次新增 mapper 前，都先问这几件事：

1. 它是现有 family 吗？
2. 差异是 bank 公式，还是整个寄存器协议？
3. 它需要 CPU 周期推进吗？
4. 它需要 A12/PPU 地址观察吗？
5. 它会接管 nametable 吗？
6. 它依赖 NES2 参数吗？
7. 它依赖 CRC 修正吗？
8. 它需要扩展音频吗？
9. 它需要 EEPROM/Flash/外设吗？

## 测试模板

至少覆盖：

- `MapperFactory` / `MapperProfile` 选型
- `CpuWrite -> bank 切换`
- `PpuRead/PpuWrite`
- `Mirroring`
- `IRQ`
- `SerializeState/DeserializeState`
- 至少一份真实 ROM 冒烟

如果 mapper 依赖特殊系统桥，还要补：

- CPU 周期推进测试
- PPU 地址观察测试
- nametable provider 测试
- CRC override 测试

## 结论

后续实现 mapper 时，优先不是“再加一个 `MapperXXX.cs`”，而是：

1. 先看它属于哪个 family
2. 再看缺少哪个系统桥
3. 先补桥，再补 core
4. 只把编号差异留在 profile

这样才能让你后面持续扩 mapper，而不是把项目重新做回“一个 mapper 一个文件”的老结构。
