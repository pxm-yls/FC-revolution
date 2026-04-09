# Mapper 架构

当前 mapper 层采用三段式结构：

1. `MapperCartridge`
2. `MapperProfileRegistry`
3. `family core`

目标不是把所有 mapper 都塞进一个巨型 `switch` 类里，而是把“对外卡带接口”统一，把“硬件差异实现”收敛到 family core。

完整路线图见：

- [docs/mapper-family-roadmap.md](/Users/pxm/Desktop/Cs/FC/FC-Revolution/docs/mapper-family-roadmap.md)

## 当前入口

- 基础接口：[ICartridge.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Core/Mappers/ICartridge.cs)
- 通用基类：[MapperBase.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Core/Mappers/MapperBase.cs)
- 统一外壳：[MapperCartridge.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Core/Mappers/MapperCartridge.cs)
- Profile 定义：[MapperProfile.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Core/Mappers/MapperProfile.cs)
- Profile 注册表：[MapperProfileRegistry.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Core/Mappers/MapperProfileRegistry.cs)
- Core 抽象：[MapperCore.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Core/Mappers/MapperCore.cs)
- Family core：
  - [BasicMapperCores.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Core/Mappers/Cores/BasicMapperCores.cs)
  - [Mmc1MapperCore.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Core/Mappers/Cores/Mmc1MapperCore.cs)
  - [Mmc3MapperCore.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Core/Mappers/Cores/Mmc3MapperCore.cs)
- 扩展能力接口：
  - [IExtraAudioChannel.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Core/Mappers/IExtraAudioChannel.cs)
  - [IMapperCapabilities.cs](/Users/pxm/Desktop/Cs/FC/FC-Revolution/src/FC-Revolution.Core/Mappers/IMapperCapabilities.cs)

## 当前如何创建 Mapper

`MapperFactory.Create()` 会：

1. 解析 iNES 头
2. 用 mapper number 查询 `MapperProfileRegistry`
3. 构造统一的 `MapperCartridge`
4. 由 `MapperCoreFactory` 选择对应 family core

也就是说：

- 外部系统只关心 `ICartridge`
- Mapper 编号只在加载 ROM 时决定一次
- 运行时的 `CpuRead/CpuWrite/PpuRead/PpuWrite` 不再走全局大 `switch`

## 新增 Mapper 的原则

优先判断“它是不是现有 family 的一个变体”。

### 只需要新增 Profile 的情况

如果新 mapper 只是现有硬件家族的编号变体，通常只要：

1. 在 `MapperProfileRegistry` 新增一个 `MapperProfile`
2. 给出 mapper 编号、名称、family
3. 按需补参数，例如 `PrgRamSize`、`ChrRamSize`、后续扩展设置
4. 补测试

这种情况下，不需要新增对外类。

### 需要新增 Core 的情况

如果新 mapper 的寄存器协议、IRQ、bank 组织、mirroring 控制方式明显不同：

1. 在 `Mappers/Cores/` 新增一个 family core
2. 继承 `MapperCore`
3. 实现：
   - `CpuRead`
   - `CpuWrite`
   - `PpuRead`
   - `PpuWrite`
   - `SerializeState`
   - `DeserializeState`
4. 如果需要，覆盖：
   - `Reset`
   - `Clock`
   - `SignalScanline`
5. 在 `MapperCoreFactory` 中接线
6. 在 `MapperProfileRegistry` 注册编号

## 共享能力

`MapperBase` 已经统一处理：

- PRG ROM 拆包
- CHR ROM / CHR RAM 初始化
- PRG RAM 分配
- 当前镜像模式

core 可以直接通过 `MapperCore` 访问：

- `PrgRom`
- `ChrMem`
- `PrgRam`
- `ChrIsRam`
- `PrgRomBanks16K`
- `PrgBanks8K`
- `ChrBanks1K`
- `CurrentMirroring`
- `IsPrgRamAddress(...)`
- `ReadPrgRam(...)`
- `WritePrgRam(...)`
- `ReadChr(...)`
- `ReadChrAt(...)`
- `WriteChr(...)`
- `WriteChrAt(...)`

## 未来预留能力

当前已经预留但尚未统一接线的扩展点：

- `ICpuCycleDrivenMapper`
- `IPpuAddressObserver`
- `IPpuNametableProvider`

这些能力用于后续接入：

- VRC/JY90 的按 CPU 周期推进
- A12/PPU 地址观察
- nametable 指向 CHR-ROM/CHR-RAM 的板子
