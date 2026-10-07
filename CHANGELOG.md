# Changelog

本文件记录**用户可见**的变化。版本号遵循[语义化版本](https://semver.org/lang/zh-CN/) `<major>.<minor>.<patch>`：
`v1.0` 之前，每个 `minor` 跳变都可能引入新特性与破坏性更新；`v1.0` 之后，只在 `major` 跳变时才引入破坏性更新。

## 未发布

### 行为变更（破坏性）

- **ModbusTcp：`endian` 回归"每个 16 位单元内部两个字节的顺序"，32/64 位的寄存器顺序改由新属性 `interpret` 表达**。
  此前 `endian` 在 16 位与 32/64 位是两套含义（16 位 = 寄存器内两字节顺序，32/64 位 = 寄存器顺序），而且 16 位的
  **组合成员**完全忽略 `endian`、**直接测点**却会交换两个字节——同一份 XML 放在 `<TagGrp>` 下与放在 `<TagCbnt>` 下
  缺省解读恰好相反且不报错。现在：

  | 关注点 | 表达方式 |
  |---|---|
  | 每个 16 位单元内部两个字节的顺序 | `endian`（`BigEndian` 直取、`LittleEndian` 交换；与 S7 同名同义） |
  | 32/64 位里各单元之间的顺序 | 新属性 `interpret="ABCD"` / `"CDAB"` / `"BADC"` / `"DCBA"`（64 位用 8 个字母，如 `"GHEFCDAB"`） |

  由此，同一份 XML 的两条承载路径（组合成员与直接测点）语义完全一致：32 位 `0x12345678` 的四种排布分别是
  `endian="BigEndian"`（线上 `12 34 56 78`）、`endian="BigEndian" interpret="CDAB"`（`56 78 12 34`）、
  `endian="LittleEndian" interpret="BADC"`（`34 12 78 56`）、`endian="LittleEndian"`（`78 56 34 12`，**完全小端**）。

  **升级提示**：这次变更有两处会影响既有配置。
  1. 16 位**组合成员**此前忽略 `endian`。若依赖过这一点，升级后请显式写 `endian="BigEndian"` 保持原解读
     （不写即按默认 `LittleEndian`，会交换字节）。
  2. 32/64 位原先的 `LittleEndian` 等于"只交换寄存器顺序"，现在是"完全小端"，结果不同（`0x56781234` → `0x78563412`）。
     想保持原来的解读，请改写为 `endian="BigEndian" interpret="CDAB"`（64 位为 `interpret="GHEFCDAB"`）。

  `interpret` 是驱动私有属性（走 `Extras`，Core 不感知），只说 32/64 位：字符数必须等于字节数，每两个连续字符
  必须是同一个 16 位单元的两个字节且先后与 `endian` 一致，写错（长度/字符/与 `endian` 冲突/重复）在**加载期报错**；
  16 位、`BYTE`、`BIT`、位空间以及 `TagCbnt` 本身写 `interpret` 也会在加载期报错。位空间（`DI`/`DO`）、`BIT`、
  `BYTE` 及 16 位的行为不变。
  新增回归测试 `ModbusEndianConsistencyTests`（两条路径必须得到同一个物理值）与 `ModbusInterpretTests`
  （64 位四种排布、与 `endian` 的相容性、各类加载期报错）。

### 修复和改进

- **ModbusTcp：`TagCbnt` 的 `slave` 属性真正生效**。此前**位**空间构建器会解析并校验它、**寄存器**空间构建器连解析都没有，但两者都没有把从站号并入寻址地址，于是 `slave="2"` 静默等同于 `slave="1"`（点位一直落在 1 号站）。现在两个空间都会在加载期把它合成到组合的起始地址：`address="10001" slave="2"` 等同于 `address="2~10001"`；地址里已经写了 `~` 前缀时以 `slave` 为准，`~` 之后的原文（如 `00020` 的前导零）保持不变。组合内的子测点地址仍是相对偏移，从站号一律由组合决定。
  **升级提示**：如果你此前在 `TagCbnt` 上写过 `slave="2"` 而点位其实一直落在 1 号站，升级后它会按字面生效——请核对这类配置是笔误还是有意为之。
- **ModbusTcp：`ModbusTcpAddress.ToString()` 修复线圈地址的回程错误**。线圈（`Area=OutputCoils`）的参考号是 `0xxxx`（`00001` 即 1 号线圈），此前的实现只做基址相加、不补前导零，`StartPoint=19` 会被格式化成 `1~20`——这个串再解析回来是 **1 号区域**（离散输入）的 20 号点，而不是 0 号区域的线圈。现在按 5 位补零输出 `00020`，四个区域的 `ToString()` 与 `ModBusTcpAddressParser.Parse` 可正常互转。

## 1.0.0

首个正式版本。此前的所有版本都只发布在测试源（`https://baget.stdunit.com/v3/index.json`），从v1.0.0起，正式版本一律发布到 nuget.org，测试版本可能会先于正式版之前发布在测试源上。

相对上一版本 `0.16.0` 的变化如下。

### 破坏性变更

- **`IsScaned` 重命名为 `IsScanned`**（`ITag` / `ITagCbnt` / `ITagGrp`）。
- **加载期异常族重构**：新增抽象基类 `TagsProjectLoadException`（带 `Location` 位置上下文），加载期错误统一派生自它——
  `TagsProjectXmlException`（XML 值不合法）、`TagsProjectAddressException`（地址无法解析）、
  `TagsProjectConfigurationException`（配置语义不自洽）、`TagsProjectValidationException`（校验器聚合错误）。
  `TagsProjectSchemaException` 不再是直接派生自 `Exception` 的独立类型，改为 `TagsProjectValidationException` 的派生（仅用于 XSD 校验）；
  校验错误始终是聚合形态（`Errors` 逐条列出，`Message` 里也拼进全部条目）。
  **此前按 `TagsProjectSchemaException` 或裸 `Exception` 捕获加载错误的代码需要调整。**
- **运行期不再抛裸 `Exception`**：库内改为语义贴近的 BCL 类型（`InvalidOperationException` / `ArgumentException` /
  `ArgumentNullException` / `ArgumentOutOfRangeException` / `InvalidCastException` / `KeyNotFoundException`），
  并在消息里补上通道名、操作、地址、底层错误等上下文。只写 `catch (Exception)` 的调用方不受影响。
- **S7：组合（`TagCbnt`）自身必须写绝对地址**。写成相对地址（如 `$$100`）会在**加载期**被拒绝 （`TagsProjectAddressException`）——这种配置以前从未真正生效过：`BlockSpecified=false` 只由 `$$` 解析产生，
  而没有任何代码为组合自身的相对地址去找上层解析。
- **SimpleFiles：移除对 废弃XML 属性 `AutoCreateFile`的支持**，现在只认 camelCase 风格的 `autoCreateFile`。仍写旧名会在加载期抛`TagsProjectConfigurationException` 并提示迁移。
- **OpcUa：读取失败 = 抛异常**。设备报的读取错误（Bad 质量）不再"打日志跳过"，改为抛异常，交给 runner 的失败重试与重连； `Uncertain` 仍按原值采集，如果需要自定义如何解释什么是“坏点”，自行在注册时设定“”。

虽然都叫破坏性更新，但是真正影响较大的只有第一条“IsScanned”的 typo fix。

### 修复和改进

- **OpcUa：证书库路径不再依赖 `%CommonApplicationData%` 占位符**。该占位符由 OPC UA 栈以"纯字符串拼接"方式替换，在 Linux 上会得到 `/usr/share\OPC Foundation\...`（名字里带反斜杠的目录），底层随即报 `File does not exist`；现在四个证书库（`MachineDefault` / `UA Certificate Authorities` / `UA Applications` / `RejectedCertificates`）都用 `Path.Combine` 拼出正确层级，**Windows 上的取值与之前完全一致**。同时新增 `OpcUaSecurityOpt.CertificateStoreRoot`（XML：`<SecurityOpt><CertificateStoreRoot>`）供 Linux 等场景指定可写目录——默认位置 `/usr/share/...` 通常需要 root。
- `TagsProjectCtrl.StopAsync()` 现在会**等待轮询真正退出**（有界超时）之后再释放项目、断开通道并触发"已停止"事件——该事件从此是确定性信号，可以放心紧跟"复用通道/换 XML/删目录"；驱动不响应取消时超时（默认 30s）记一条 Warning 并继续清理。
- `ITag.Timestamp` 统一为 **UTC**：原实现本地时间（`DateTime.Now`）与 UTC 混用，现统一为 `DateTime.UtcNow`；展示或与本地时间比较时请自行转换。
- OpcUa：`WriteAsync` 不再丢弃调用方的 `CancellationToken`；
- SimpleFiles：解析文件内容失败时，异常消息补上测点名与文件路径（`ParseValue` 子类只拿得到文本，路径由基类补）。
- 日志: 不再把异常拍平成 `ex.Message`，记录更多上下文。

