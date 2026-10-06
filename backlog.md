
# Backlog / 开发计划


## v1.0 之前的 todo

v1.0 之前只专注于正确性和可靠性，我不推荐外部人员使用——每多在一个现场用，就可能为我将来引入的不兼容改动增加一些负担。

- [x] 轮询清理路径修复：`TagGrpRunner` 清理路径不再用已取消的 ct 调 `DisconnectAsync`（原实现 S7 会因 `_rw.WaitAsync(ct)` 立即抛 OCE 导致连接不断开、资源泄漏）。
- [x] 通道断开策略化：新增 `ITagGrpRunnerDisconnectStrategy`（Core 公共接口）+ `DefaultTagGrpRunnerDisconnectStrategy`（有限超时等待，默认 5s），可按设备注入不同断开等待策略；`TagGrpRunnerFactory` 从 DI 解析。
- [ ] OpcUaClientChannel 的具体问题清理：`WriteAsync` 不再丢弃调用方的 `ct`；检查 `ReadValuesAsync` 返回的错误（坏值不进缓存）；去掉 `Bag` 的双重写入；证书默认值（`AutoAcceptUntrustedCertificates` 等）改为可配置并输出警告。
- [ ] TagsProjectCtrl 的清理路径的空 `catch` 被有意设计成了静默吞掉异常（如 `StartPollAsync`/`StopAsync` 中的 `Dispose`/`DisconnectAsync`），但应该补上日志，增加可观测性、不改变吞掉异常的语义。
- [x] 异常细化（**加载期**已完成）：加载期错误统一收敛到异常族 `TagsProjectException`(**abstract**) → `TagsProjectLoadException`(**abstract**，带 `Location`) → `TagsProjectXmlException` / `TagsProjectAddressException` / `TagsProjectConfigurationException` / `TagsProjectValidationException`（`Errors` 明细；原 `TagsProjectSchemaException` 改为其子类，保留 XSD 语义与旧消息格式）。Core 与全部驱动（S7 / ModbusTcp / Hjzk / ZLan / OpcUaClient / ComScanner / SimpleFiles）的 XML 解析、地址解析、描述符校验、构建期配置检查已全部改用具体类型；校验器（交叉引用/驱动工厂/入口独占/嵌套入口）改抛 `TagsProjectValidationException`。重名测点报错带完整路径上下文（`TagGrp(a)/TagCbnt(b)/Tag(c)`，段间用 `/`，与 `ITagGrp.Descendant` 的路径语法一致），由 `CompositeTagsLoader` 与 `TagCbntBuilderBase.AddTag` 提前拦截（不再暴露字典的 "An item with the same key..."）；面向驱动作者的选型决策树见 [docs/异常处理.md](docs/异常处理.md)。
  - 仍待办（**运行期**异常细化）：读写/连接/清理路径上仍有裸 `Exception`/`InvalidOperationException`/`NotImplementedException`（如通道与测点组合的类型不匹配、字节序分支兜底、S7 地址格式化兜底）。这些大多是"不应发生"的不变量，价值低于加载期，留待后续；届时可引入运行期异常族并把两者挂到同一个根类型下。
- [x] 为项目描述 XML 引入 schema 校验机制：XSD 校验（核心 `tagsproject.xsd` + 各驱动 `Schemas/*.xsd`，XSD 1.0 命名空间模块化；运行期可选校验 `EnableXmlSchemaValidation()`，编辑器经 buildTransitive 自动注入 XSD 获得智能提示；第三方 schema 经 `ITagsProjectSchemaProvider` 合并）。
- [x] 加载期交叉引用校验（XSD 表达不了）：`channel` 属性必须能在已声明的 `<Channel>` 中找到，拼错的通道名在加载期报错而不是运行时才暴露；错误带完整路径上下文。统一抽象为 `ITagsProjectValidator`（可注册多个实现）：`EnableXmlSchemaValidation()`（XSD）、`EnableCrossReferenceValidation()`（channel 引用 + `ChannelDriverFactoryValidator`：driver 必须对应已注册的通道工厂，未注册的驱动名在加载期报错；`NestedEntryValidator`：嵌套在入口内的 `isEntry="true"` 不生效，拒绝；`EntryChannelExclusivityValidator`：同一通道不得被多个入口共用，拒绝）；第三方可用 `AddValidation<TValidator>()` 追加。
- [x] 单入口多通道：入口子树中所有会被用到的通道统一建连与断开。`ITagGrpExtensions.CollectChannels()` 向下递归收集；`TagGrpRunner` 每轮逐一 `EnsureConnectedAsync(force:false)`，清理路径 `DisconnectAllAsync` 断开全部通道（先全部发起再按 `ITagGrpRunnerDisconnectStrategy` 逐一等待）。语义：事件委托的 `channel` = 入口**主通道**；**一个通道 = 一个轮询回路**，同一通道实例不得跨入口共用——由默认启用的 `EntryChannelExclusivityValidator` 强制。理由不止"断开互相踩"（连接动作可为空实现，如 SimpleFiles），更根本的是两套扫描周期会交错读写同一底层资源、且两个入口的生命周期被耦合；而要求分离的代价几近为零（再声明一个指向同一设备的 Channel 即可）。
  - 注：入口识别（`ScanEntries`）遇到入口即停止下探 ⇒ **只有最外层入口才是入口**，嵌套 `isEntry="true"` 不生效（该子树仍由外层入口轮询）；错误配置由默认启用的 `NestedEntryValidator` 拒绝。
- [ ] 文档完善和更新：`docs/` 放框架性内容，详细使用说明放独立文档库。
- [ ] OpcUa和ModbusTcp单通道多入口并发支持。说明：当前S7已经做了单通道多入口的串行化，不过OpcUa和ModbusTcp目前只支持"单通道单入口"模型。这可能是一个值得改进的方向，但优先级不是很高：第一，我认为在工业交互的场景下，“单通道单入口”串行轮询机制更合理；其次，对于单设备多入口场景，可以手动建立指向单一设备的多个通道来解决——哪怕是`C#`官方类库，也没有强迫`TcpClient`是线程安全的；第三，多入口并行在很多场景下，对于没有思索过具体细节的新手用户，会带来非常多的困扰。简单的说，我更推崇**单入口多通道**模式。如果将来真要做单通道支持入口并行化，可以参考 ComScanner/S7 设计横展。

## v1.0 之后的 todo

嗯，v1.0 

- [ ] 日志、报错提示、注释文档的多语言支持。
- [ ] 工程化：
  - [x] CI 强制测试（`.github/workflows/dotnet.yml` 构建+测试、`release.yml`）
  - [x] `coverlet.collector` 已引入但未做覆盖率门槛；
  - [ ] 启用 NetAnalyzers / `TreatWarningsAsErrors`；
- [ ] Cache性能优化：Modbus 读路径 cache 复用——`TagCbnt<T>` 各驱动的 `ReadAsync` 每轮轮询换新 `Cache` 引用（如 `ModbusRegisterTagCbnt` 的 `this.Cache = regs`、位空间 `this.Cache = bits`），`CacheSize` 不变时可复用同一 `T[]` 消除每轮分配；需接口改动（`IModbusRegisterChannel.ReadRegistersAsync`/`IModbusBitsChannel.ReadBitsAsync` 改为写入预分配 buffer 返回元素数）
- [ ] S7 优化：底层基于 Sharp7 一个古老的实现，有两个优化的点：
    - 底层通道基于 Sharp7 一个古老的同步实现，用了 `Thread` 伪装成异步接口。将来可以改成真异步（上层异步接口不需要变更）。说明：PLC的并发吞吐能力远小于上位机，而且目前已经在单通道做了串行轮询机制，所以这个改进的收益不大。
    - 底层有很多无谓的字节拷贝和内存分配操作，借助 C# 的 `Span<T>` 和 `Memory<T>` 可以大幅优化实现。