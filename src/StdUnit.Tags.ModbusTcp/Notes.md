# StdUnit.Tags.ModbusTcp 实现笔记（Notes）

> 面向**维护者与 AI 编码代理**：记录"为什么是这样"，以及每条结论的实证依据（测试名）。
> **使用者文档**见教程站 <https://tags.doc.stdunit.com/> 的 Chapter 14 ModbusTcp 驱动专题，
> 以及 [AI 参考手册](https://tags.doc.stdunit.com/assets/999.AI.markdown) §5.2。

## 1. 端序：数据链路上的三层，以及"主机端序为什么不参与"

```text
设备寄存器 ──线上大端（高字节在前）──▶ NModbus 解析 ──数值──▶ 本库 ushort[] ──endian + interpret 解读──▶ 测点物理值
```

先约定三个"字节坐标系"，后文与代码注释都用这套叫法（代码里对应 `deviceBytes` / `valueBytes`）：

| 叫法 | 是什么 | 谁看得见 |
|---|---|---|
| **线上字节** / 链路 | 真实 socket 上的字节：协议规定每个寄存器内部大端（高字节先） | 只有通道层（NModbus）与真机测试 |
| **设备端字节**（`deviceBytes`） | 从寄存器**数值**重建出的"设备里这 4/8 个字节怎么摆"。重建恰好是 NModbus 网络序 → 数值转换的**逆运算**，因此它与真实链路上同几个寄存器的线上字节**逐字节相同**（对非标设备也成立） | 测点层：`ModbusValueInterpreter<T>` |
| **值里的字节**（`valueBytes`） | 值的规范大端字节：`valueBytes[0]` = 最高字节（`BinaryPrimitives` 的约定） | `ModbusValueInterpreter<T>` |

`interpret` 描述的正是"**值的第 i 个字节出现在设备端字节的第几个位置**"，所以设备端字节就是它的坐标；
提到"线上"时，指的始终是真实链路那层。

**第一层：线上 / 设备侧。** Modbus 规范规定**单个寄存器内部**是两个字节、大端（MSB 先）。这是协议约定，配置改不动。

**第二层：NModbus。** 它把响应里那串字节还原成数值：

```csharp
// NModbus/Data/RegisterCollection.cs → NModbus/Utility/ModbusUtility.cs
public static ushort[] NetworkBytesToHostUInt16(byte[] networkBytes)
{
    ...
    result[i] = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(networkBytes, i * 2));
    ...
}
```

`NetworkToHostOrder` 是"网络序 → 主机序"：线上 `12 34` ⇒ 数值 `0x1234`。在 x86 上这一步会执行一次字节交换、
在大端主机上不做，**两种主机得到的数值完全相同**。写方向对称：`RegisterCollection.NetworkBytes` 用
`HostToNetworkOrder` 把它变回线上字节。

**第三层：本库。** 缓存是**数值数组**——`ModbusRegisterTagCbnt : TagCbnt<ushort>`（位空间是 `TagCbnt<bool>`），
不是字节容器；全库没有任何 `MemoryMarshal.AsBytes` / `BitConverter.GetBytes(ushort)` 参与测点值路径
（`Compat/FloatBitsCompat` 里的 `Unsafe.As` 是**数值级**位重解释，不重排字节）。**因此主机端序在本库语义里不出现。**

两个容易搞错、但已被测试钉住的推论：

- `ushort` 没有端序，端序是"**字节布局**"的属性。设备字节 `[12, 34]` 与上位机内存里同一个 `ushort` 的字节
  `[34, 12]` **顺序相反、数值相同**；拿 `byte[]` 去和设备端原样拷贝的字节逐字节比较会"不一致"，但这不影响任何
  算术与协议编解码。
- "NModbus 已经替我们转成了小端序"这种说法**只在 16 位尺度上碰巧成立**，不要采用（它会让人以为 32/64 位也该
  做整体字节翻转、也会让人以为语义依赖主机端序）。准确说法是：**NModbus 把"寄存器内部字节顺序"这个自由度用掉了。**

| 实证（测试） | 钉住什么 |
|---|---|
| `ModbusTcpWireRoundTripTests.HoldingRegisters_WireBigEndianBytes_ComeBackAsNumericValue` | 线上 `12 34` ⇒ 数值 `0x1234` |
| `ModbusTcpWireRoundTripTests.HoldingRegisters_Write_GoesOutAsBigEndianWireBytes` | 数值 `0x1234` ⇒ 线上 `12 34` |
| `ModbusTcpWireRoundTripTests.DeviceBytes_AndHostUshortMemoryLayout_AreReversedButSameValue` | 设备字节与主机内存布局顺序相反、数值相同（断言与主机端序无关） |

## 2. `endian` 的语义：只管"每个 16 位单元内部两个字节"

`endian` 与 S7 **同名同义**：描述**每个 16 位单元（寄存器）内部两个字节**的顺序。Modbus 规范规定标准设备是
大端（`BigEndian`）；非标设备（被字节序镜像过的网关）可能反着放，此时写 `LittleEndian` 让本库把这两个字节换回来。

| 类型 | 占几个寄存器 | `endian` 管什么 | `BigEndian` | `LittleEndian` |
|---|---|---|---|---|
| `INT16` `UINT16` | 1 | 寄存器内两个字节 | 直取寄存器数值 | 交换这两个字节 |
| `INT32` `UINT32` `FLOAT` | 2 | 每个单元内部两个字节（**单元之间**的顺序见 §3 `interpret`） | 单元内高位在前 | 单元内低位在前 |
| `INT64` `UINT64` | 4 | 同上 | 同上 | 同上 |
| `BYTE` | 半个 | 取寄存器的高字节还是低字节 | 高字节 | 低字节 |
| `BIT` `DI` `DO` | 一个位 | 无（没有一对字节可排） | — | — |

几条必须保持的约定：

- **默认值是 `LittleEndian`**（`TagDescriptor.EndianKind` 的初始化，全库统一，S7 也靠它），所以**标准设备必须显式写
  `endian="BigEndian"`**。不写不会报错，只会按"低位在前"解读：16 位换字节、32/64 位是**完全小端**（见 §3）。
  代码注释里说 `BigEndian` 是"Modbus 惯例"时，指的是**标准设备的做法**，不是本库的默认值。
- **16 位这里补偿的是"非标设备把两个字节反着放"**，不是在做主机↔设备的端序转换（主机端序按 §1 不参与）。
  标准设备走 `BigEndian` 分支即"什么都不做"。
- **两条承载路径语义必须一致**：同一份 XML 放在 `<TagGrp>` 下（DirectTag）与放在 `<TagCbnt>` 下（Cbntor）
  必须得到同一个物理值。这是一条硬约束，曾被破坏过（16 位组合子曾完全忽略 `endian`）。

| 实证（测试） | 钉住什么 |
|---|---|
| `ModbusEndianWireTests` | 真 socket（假服务端）上的端序矩阵：每种类型由直接测点与组合成员**各自**断言同一个物理值，覆盖 10 种类型 × 两种 `endian`；配错时得到可预测的错值且不报错 |
| `ModbusEndianWireTests.CrossPath_*` | 把"两条承载路径必须一致"本身写成断言（读 + 写各一条，组合成员写的是整个缓存故只比较子测点所在段） |
| `ModbusEndianWireTests.Int32_Cdab_SwapsRegistersWhileDcba_ReversesEverything` | `CDAB` 与 `DCBA` 是两个不同的结果，不能混为一谈 |
| `ModbusRegisterCbntorTests` | 组合子各类型的端序解读（每种排布独立成例） |

## 3. `interpret`：寄存器之间的字节排布（只对 32/64 位有意义）

一个 `endian` 布尔表达不了"字节序 × 字序"两个自由度，所以 32/64 位多一个可选属性 `interpret`：

```xml
<Tag name='a' address='40001' type='FLOAT' endian='BigEndian' interpret='CDAB' />
```

**记法**：一串大写字母，`A` = 数值的最高字节、`B` = 次高……从左到右就是这 4/8 个字节**在设备端字节里出现的先后**。
字符数必须等于该数值的字节数：32 位写 4 个字符、64 位写 8 个字符；16 位、`BYTE`、`BIT`、位空间（`DI`/`DO`）
以及 `TagCbnt` 本身都没有"多字节可排"，写 `interpret` 一律在**加载期报错**。

**与 `endian` 的分工**：`endian` 已经确定了每个 16 位单元内部两个字节的先后，因此 `interpret` 里
**每两个连续字符必须是同一个单元的两个字节，且先后与 `endian` 一致**——`BigEndian` 只接受 `AB`/`CD`/…，
`LittleEndian` 只接受 `BA`/`DC`/…。这样"同一个物理排布"只有唯一写法，不会被两种配置表达成同一件事。

32 位物理值 `0x12345678`（`A`=0x12、`B`=0x34、`C`=0x56、`D`=0x78）的四种排布：

| 写法 | 设备端字节 | 数值 | 说明 |
|---|---|---|---|
| `endian="BigEndian"`（或 `interpret="ABCD"`） | `12 34 56 78` | `0x12345678` | 完全大端，标准设备 |
| `endian="BigEndian" interpret="CDAB"` | `56 78 12 34` | `0x12345678` | 字交换（16 位单元顺序相反） |
| `endian="LittleEndian" interpret="BADC"` | `34 12 78 56` | `0x12345678` | 字节交换（单元内部两字节相反） |
| `endian="LittleEndian"`（或 `interpret="DCBA"`） | `78 56 34 12` | `0x12345678` | 完全小端 |

不写 `interpret` 时就是上表第一行与第四行：`BigEndian` = `ABCD`、`LittleEndian` = `DCBA`。
**显式写 `ABCD…` / `DCBA` 与不写完全等价**——解析后归一成同一个排布，连复用的解读器实例都是同一个
（恒等排布走直通分支，连落位表都不带）。
64 位把字母表延长到 `H`（`A` = 最高字节），于是 32 位的 `CDAB`/`BADC`/`DCBA` 分别对应
`GHEFCDAB`（按 16 位单元整体倒着排）、`BADCFEHG`（每个单元内部换字节）、`HGFEDCBA`（完全小端）；
`interpret="ABCDEFGH"` 与 `endian="BigEndian"` 等价。
**记法是"单元顺序"的完整表达**，不是只有上面四种：32 位只有 2 个单元（所以恰好四种），64 位有 4 个单元，
单元之间任意排列都写得出来（例如 `BigEndian` + `interpret="CDABGHEF"`，设备端字节为 `56 78 12 34 DE F0 9A BC`——
"8 个字节"不再是表达能力上的缺口）。

实现与校验都在驱动侧，**Core 不感知**：`interpret` 不是 Core 的 builtin 属性，会原样落进 `Extras`。两侧分工：

- `ModbusInterpret` —— 属性读取、记法解析与校验（长度 / 字符集 / 重复 / 与 `endian` 冲突），以及"合法排布"的枚举；
- `ModbusValueInterpreter<T>`（`int` / `uint` / `float` / `long` / `ulong` 各一个具体实现）—— 把寄存器数组与目标类型
  直接互转（`Read` / `Write`），调用点不再经过 `ulong` 中转与强制转换。

**解读器实例是复用的**：它不可变，状态只有"字节数 + 一张落位表"（值里的第 i 个字节 → 设备端第几个位置），
而合法排布有限——32 位 **4 种**、64 位 **48 种**（2 种 `endian` × 16 位单元全排列；见 `ModbusInterpret` 的枚举）。
所以每个具体类型在首次使用时一次性造好静态实例池，按排布编号取（`ModbusUInt32Interpreter.For(descriptor)`
这类入口），**同一型号 + 同记法的测点共用同一个对象**；不带 `interpret` 的恒等排布走直通分支，连落位表都没有
（记法里显式写 `ABCD…` 也归一到这里）。没有锁、没有延迟初始化技巧，整个过程发生在加载期。

落位表用 `ReadOnlyMemory<byte>` 表达（C# **没有**"只读数组"这种东西——`readonly byte[]` 只锁引用、元素照样能改：
`ReadOnlyMemory<T>` 是类型级只读，`ReadOnlySpan<T>` 是 `ref struct` 做不了字段、`ImmutableArray<T>` 要新引依赖），
每个字节一项、由所有实例共享：48 张 8 字节表合计约 0.5 KB，取项就是 `map.Span[i]`——没有位移/掩码，
也不受"byteCount ≤ 8"限制（将来要支持更宽的格式不用改编码）。实测三种表达在这个路径上没有可测差异。

副作用是**其它驱动不认识 `interpret`**（写上去等于写了个被忽略的属性），需要时由各驱动自己实现。

| 实证（测试） | 钉住什么 |
|---|---|
| `ModbusValueInterpreterTests.EveryType_RoundTripsEveryLayout` | 每一组排布 × 每种类型（`uint`/`int`/`float`/`ulong`/`long`）写进去再读回来都是原值：0、边界值、负数、NaN、±∞ |
| `ModbusValueInterpreterTests.Float_LeavesEachLayout` / `Int32_NegativeValue_LeavesEachLayout` | 逐字节落点（含符号位所在的那一个字节），把"字交换"与"字节交换"区分开 |
| `ModbusValueInterpreterTests.Interpreters_ExposeTheirWidth` / `NotationLength_ComesFromType_NotTagSize` | 宽度由类型给出（4/2、8/4），不受描述符上写错的 `tagSize` 影响 |
| `ModbusInterpretTests.DescribeNotation_MatchesKnownLayouts` | 记法文案（32 位四种、64 位四种）与排布一一对应 |
| `ModbusInterpretTests.Parse_IdentityMapping_IsNormalizedToEmpty` / `Parse_ExplicitDefault_EqualsImplicit` / `Parse_WhitespaceOnly_TreatedAsAbsent` / `Parse_TrimsSurroundingSpaces` | 恒等归空表、显式默认＝不写、空白＝没写、两端空白被裁掉 |
| `ModbusInterpretTests.RejectForSingleUnit_*` / `RejectOnCbnt_WithAndWithoutInterpret` | "不该写 `interpret`" 的三类拒绝策略（单 16 位种类 / 多寄存器放过 / 组合） |
| `ModbusValueInterpreterTests.Packings_AreFiniteAndStartWithIdentity` / `Packings_HaveNoDuplicates` / `VariantIndex_MatchesNotation` | 合法排布的枚举（4/48 种、下标 0 = 恒等、无重复）与"记法 → 编号"的对应 |
| `ModbusValueInterpreterTests.EnumeratedPackings_RoundTripThroughNotation` | 枚举与校验同源：每组排布写成记法后都能被 `Parse` 接受、还原成同一组排布，且只在自己那一种 `endian` 下合法 |
| `ModbusValueInterpreterTests.For_ReturnsSharedInstances` / `For_IsSharedAcrossThreads` | 同排布共用实例、不同排布不同实例；多线程首次取用不会各造一份 |
| `ModbusInterpretTests.Int64_Read_DirectTag_RecoversDeviceValue` / `Int64_Write_DirectTag_UsesConfiguredLayout` | 64 位四种排布在真 socket 上的读数与写回 |
| `ModbusInterpretTests.Int64_Read_CbntMember_RecoversDeviceValue` | 组合成员与直接测点解读一致 |
| `ModbusInterpretTests.Int32_ExplicitDefaultNotation_EqualsImplicit` | 显式 `ABCD`/`DCBA` 与不写 `interpret` 等价 |
| `ModbusInterpretTests.Interpret_ConflictingWithEndian_Throws` | 每对字符与 `endian` 的相容性校验 |
| `ModbusInterpretTests.Int32_Interpret_WithWrongLength_Throws` / `Int64_Interpret_WithFourLetters_Throws` | 字符数 = 字节数 |
| `ModbusInterpretTests.Int32_Interpret_WithIllegalLetter_Throws` / `WithRepeatedLetter_Throws` | 字符集与"各出现一次"校验 |
| `ModbusInterpretTests.DirectTag_SingleUnit_WithInterpret_Throws` 等 | 16 位 / `BYTE` / 寄存器位 / 位空间 / `TagCbnt` 上写 `interpret` 报错 |
| `ModbusRegisterCbntorTests.Int32_AllLayouts_RecoverSameValue` / `Int64_AllLayouts_RecoverSameValue` | 组合子层四种排布的读与写 |

## 4. 与 S7 的差别（为什么实现方式必然不同）

| | S7 | Modbus |
|---|---|---|
| 缓存 | `S7TagCbnt : TagCbnt<byte>` —— PLC 原始内存字节 | `ModbusRegisterTagCbnt : TagCbnt<ushort>` —— NModbus 解析后的寄存器数值 |
| 字节序自由度 | 还在我们手里 | 已被协议（寄存器内部大端）用掉 |
| `endian` 的实现 | 选择按哪种字节序读那串字节（`BinaryPrimitives.ReadInt16BigEndian` / `LittleEndian`） | 数值级补偿：每个 16 位单元内部换字节 |
| 寄存器/单元之间的顺序 | 由 `endian` 一并表达（缓存是连续字节，一个开关到底） | 由 `interpret` 表达（32/64 位），`endian` 只管单元内部 |
| 校验依据 | `S7Int16TagCbntor`、`S7 Int16DirectTag` 两侧都按 `endian` 处理 16 位 | 本库 16 位两条路径都按 `endian` 处理（与 S7 对齐） |

语义层已对齐（同一物理值 + 同一 `endian` ⇒ 同一结果；Modbus 的 `interpret` 是 S7 没有的额外自由度）；
实现层必然不同，因为一边"还没解读"、一边"已经解读过了"。

## 5. 通道层的边界

`ModbusTcpChannel` 只做"地址解析 → 分批 → 调 NModbus"，**不做任何端序调整**；端序解读全部在测点层
（`MultipleBytesDirectTag<T>.GetValueFromRegisters` / `FillRegisters`、`ModbusMultipleBytesCbntor<T>`、
`ModbusValueInterpreter<T>`）。
新增类型或改解读逻辑时，请守住这个边界：通道层不引入字节序概念，否则"同一份 XML 两条路径一致"这条契约会被破坏。

### 术语：参考号（reference number）与 StartPoint

XML 里 `address` 写的是**参考号**——1 起算的 5 位写法（`40001` = 保持寄存器的第 1 个点，`00001` = 1 号线圈）。
它是 Modicon 沿用下来的行业惯例（协议规范本身只说 `starting address`/`address`），
**不是**帧里传的那个值：解析时统一 `参考号 - 1` 得到 0 起算的协议地址，即
`ModbusTcpAddress.StartPoint`（= NModbus 的 `startAddress` 参数）。
仓库内只用这两个词，避免"点号 / 首地址"这类无法分辨 0 起算还是 1 起算的说法；
`ModBusTcpAddressParser` 里的正则分组名 `start` 指的是**参考号**（分组名不动）。
