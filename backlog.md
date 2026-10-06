
# Backlog / 开发计划


## v1.0 之前的 todo

v1.0 之前只专注于正确性和可靠性，我不推荐外部人员使用——每多在一个现场用，就可能为我将来引入的不兼容改动增加一些负担。

- [x] 轮询清理路径修复：`TagGrpRunner` 清理路径不再用已取消的 ct 调 `DisconnectAsync`（原实现 S7 会因 `_rw.WaitAsync(ct)` 立即抛 OCE 导致连接不断开、资源泄漏）。
- [x] 通道断开策略化：新增 `ITagGrpRunnerDisconnectStrategy`（Core 公共接口）+ `DefaultTagGrpRunnerDisconnectStrategy`（有限超时等待，默认 5s），可按设备注入不同断开等待策略；`TagGrpRunnerFactory` 从 DI 解析。
- [x] OpcUaClientChannel 的具体问题清理：
  - [x] `WriteAsync` 不再丢弃调用方的 `ct`（原来是 `CancellationToken.None`，取消信号传不到底层，写入会无视取消一直阻塞）。
  - [x] 检查 `ReadValuesAsync` 返回的错误 —— **最终语义：坏点 = 读取失败（抛异常），既不静默跳过也不替下游解释质量**。
    - 判据：`OpcUaValueQuality.IsFailed(err, value)`——`err` 或 `DataValue.StatusCode` 任一为 **Bad** 即失败；`Uncertain` 不算（那只是"服务器给了值、它自己不确定"，是否可信是业务问题），为 `null` 视为失败。
    - 行为：`OpcUaClientTagChannel.ReadAsync`（任意节点 Bad → 整体失败）与 `ReadValueAsync`（该节点 Bad → 失败）抛 `InvalidOperationException`，消息带通道名 + 操作 + 每个坏节点的节点与状态码（`节点=ns=1;s=Bad 状态码=0x80340000(BadNodeIdUnknown)`）。三个消费点（cbnt / cbntor / 直连测点）不再判断质量，只按"返回即非坏值"使用。
    - **为什么不是"跳过坏点 + 记日志"（我最初的做法，已被推翻）**：(1) 坏点大多是通信/配置问题（`BadNotConnected`、`BadNodeIdUnknown`…），**只有抛出去才能走 `TagGrpRunner` 的"崩溃 → 断开全部通道 → 重连"恢复路径**，静默跳过恰好把重连路径掐断了；(2) 下游能用的接口只有"异常（重试/崩溃策略、`RunnerCrashed`）"和"值本身"，日志对下游没作用——静默跳过会让值被无限期冻结而入口看起来还活着；(3) 与 S7/Modbus"读到错误码就抛"的行为不一致。
    - 代价（已知并接受）：永久性坏点（如 NodeId 写错）会让该入口持续走"失败 → 断开 → 重连"，延迟按 `DefaultTagGrpRunnerRetryStrategy`（Logistic）从 500ms 涨到 30s 封顶，期间该入口不再产出新值——但这是**显式失败**而不是静默冻结，且应用可通过 `RunnerCrashed` 观测到。
    - 用户定调（原话）："数据采集就是采集……如果真想输出(点的测量值, 可信度)，再加一个 Tag 即可" → 驱动层不引入质量概念，可信度由业务用额外的测点表达。
    - **"什么算失败"做成了可注入的策略**（不改变默认口径）：`AddOpcUaClientSupport(checkIsFailed: (err, value) => ...)` / `AddOpcUaClientChannel(checkIsFailed: ...)` → 注册时闭包构造 `OpcUaClientTagChannelFactory` → `OpcUaClientTagChannel` ctor 的可选参数，默认 `OpcUaValueQuality.IsFailed`。于是"更严（Uncertain 也算失败）"与"更松（任何状态都照原样采集，代价是 Bad 时 Value 可能为 null，由使用方承担）"都成了使用方的选择，而库的默认行为仍是"不解释质量"。未做成 XML 配置项：它是个委托，XML 表达不了，也避免让配置承担代码职责。
    - 实测（Opc.Ua.Core 1.5.374）：`ServiceResult.IsBad(Bad)=true`、`IsBad(Uncertain)=false`、`IsBad(Good)=false`；但 `ServiceResult.IsGood(Uncertain)=false`——判据必须直接问 `IsBad` 而不是取反 `IsGood`，否则不确定质量会被误判成失败。该版本没有 `BadClarification` 之类"Bad 填充位"常量，无歧义。
  - [x] 去掉 `Bag` 的双重写入：`Bag[nodeId] = value;` 之后紧跟的 `Bag.AddOrUpdate(nodeId, value, ...)` 是冗余的（索引器本身已是插入或更新）。
  - [x] 证书默认值改为可配置并输出警告：新增 `OpcUaSecurityOpt`（挂在 `OpcUaClientTagChannelOpt.SecurityOpt`）——`AutoAcceptUntrustedCertificates`(true) / `RejectSHA1SignedCertificates`(false) / `MinimumCertificateKeySize`(1024，类型与底层 `SecurityConfiguration` 一致用 `ushort`)；默认值与旧硬编码行为一致，但**任一宽松项生效时构造通道会输出一次 `LogWarning`**（列出具体项，提示在 `<SecurityOpt>` 中收紧）。XML 解析/回写（`<SecurityOpt>` 子元素）、`opcua-client.xsd`、fixture 与 sample 均同步。
  - 附带修掉：`ReadAsync` 结果数与请求数不一致时抛带通道名与 TagCbnt 名的 `InvalidOperationException`（原来会 `IndexOutOfRangeException`）；`ParseSreverOpt` 的 `UsePassword` 与新增的 `SecurityOpt` 各字段**不再静默降级**（`bool.Parse` 失败现在抛 `TagsProjectXmlException` 并带 `Channel(名)` 定位，与 Hjzk 等驱动一致）。
  - 语义确认（写进代码注释）：读取失败时缓存/时间戳/通知都不改动，异常原样传出；成功时"先写完整个缓存再逐点通知"——处理器里读同组其它测点时不会看到半更新的缓存。
  - [x] 轮询热路径去掉每轮重复构建（`ReadAsync`/`WriteAsync` 每轮都要"子测点 ↔ NodeId"映射）：
    - 删掉冗余的 `_nodeIdCache` / `GetNodeIdByTagName`：`NodeId` 本来就是 `OpcUaClientTagCbntor` 的只读属性（构造时从 `RawAddress` 定下），用"测点名 → NodeId"的字典去"缓存"它等于把字段读取换成字符串哈希。
    - 新增懒构建的 `GetNodeMap()`：两个下标对齐的数组（`OpcUaClientTagCbntor[]` + `NodeId[]`），读/写路径共用；子节点集合在加载完成后不再变化（与 S7/Modbus 一致——它们在构建期就把偏移量烘进了字节缓存），仍按数量做一次校验，万一加载后增删了子节点会重建而不是静默错位。
    - 效果（每轮每组合）：省掉 1 次 LINQ 委托 + N 次 `TagName()` + N 次字符串哈希查字典 + 2 个 `List` 分配（`.ToList()` / `Select`）。**量级说明**：这些开销相对一次 OPC UA 往返（ms 级）可以忽略，收益主要是"不再每轮做显然重复的事"与减少 GC 压力，不是吞吐瓶颈。
    - 两趟遍历**有意保留**（先写完整个缓存再通知）：与 `TagCbnt.NotifyChildrenRead()` 的语义一致（S7/Modbus 走的就是它）——处理器里读同组其它测点时不该看到半更新的缓存。两趟现在都是对缓存数组/字典的紧循环，合并省下的开销可忽略。
    - 附带行为变化（有意）：同一 NodeId 被两个子测点引用（别名）时，写入不再抛"已添加相同键"，而是只写一次（最后设置的脏值生效）——与读路径（本来就容忍别名）一致。
  - 新增 21 个测试（通道 9：`ct` 透传、宽松默认值+警告、收紧后不警告、任一节点 Bad → 抛错并带通道/节点/状态码、Uncertain 不抛错、单节点 Bad → 抛错、`checkIsFailed` 三例（恒 false 不抛 / 恒 true 也抛 / 可把 Uncertain 当失败）；cbnt 5：通道抛错时缓存与通知不动、结果数不匹配报错、子节点增删后映射缓存重建、别名 NodeId 写入只写一次、脏但无值抛错；cbntor 2：通道抛错时保留上次值/时间戳且不发通知、脏但无值抛错且不调通道；直连测点 1：读取失败时值/通知/时间戳不动；描述符 6：`SecurityOpt` 解析/默认值/两个非法值/`UsePassword` 非法值/`ToXElement` 往返；注册 2：带 `checkIsFailed` 能正常解析并建通道、与手动组合等价）。

  - [x] 脏但没有缓存值 → **抛带上下文的错**（用户定调："这一点我认为应该抛出异常"）：`OpcUaClientTagCbnt.WriteAsync` 与 `OpcUaClientTagCbntor.WriteAsync` 都改为先 `Bag.TryGetValue`，取不到就抛 `InvalidOperationException`，消息带通道名 + 测点名 + NodeId（原来 `Bag[nodeId]` 直接取下标会抛**无消息**的 `KeyNotFoundException`）。理由：跳过会变成"标记了要写、实际什么都没写"的静默失效，而正常路径不会出现这种情况（`Value` setter 先写 Bag 再 `MarkDirty()`），只有绕开 setter 直接置 `IsDirty = true` 才会——那属于用户错误，就该显式报出来。新增 2 个测试（cbnt 路径；cbntor 路径同时断言**不调用通道**，避免"什么都没写却报告写成功"）。


- [x] TagsProjectCtrl 的清理路径日志化：5 处空 `catch`（`StartPollAsync` finally 里的 `project.Dispose()`；`StopAsync` 里的 `project.Dispose()`、取消 `_cts` 失败、逐通道 `DisconnectAsync`、`StartedOrStopped` 事件处理器）改为捕获异常后 `LogWarning(ex, ...)`，**语义完全不变**（仍然吞掉、继续清理、不影响返回/原始异常），日志带上 `ProjectRoot` 或通道名。新增 3 个测试锁定四条不变量：不向外抛、留痕、一个通道失败不阻断其它通道、释放失败不覆盖轮询阶段的原始异常（`TagsProjectCtrlTests`，配 `CapturingLoggerProvider` + `MockTagsProject.DisposeThrows`）。
- [x] `TagGrpRunner` 的清理路径日志化（与 TagsProjectCtrl 同类问题）：3 处静默吞掉改为捕获后 `LogWarning(ex, ...)`，**语义完全不变**（仍然吞掉、不阻断其它通道的清理、不影响 `StartAsync` 的异常传播）——`DisconnectAsync` 抛错、`DisconnectAllAsync` 里"发起断开"失败、等待断开策略失败；消息带通道名/入口名。新增 2 个测试（`StartAsync_WhenDisconnectThrows_SwallowsButLogs` / `StartAsync_WhenDisconnectStrategyThrows_SwallowsButLogs`），并把 `CapturingLoggerProvider` 提到 `Tests/Fakes/` 供两个测试类共用。
- [x] 消除时间敏感测试的偶发失败（能确定化的部分已完成，套件耗时 11s → 8s）：
  - `TagGrpRunnerTests.StartAsync_TurnCrashedHandlerThrows_ExceptionPropagates`：原来用 `CancellationTokenSource(2000)` 当截止——**到期取消会被外层 `finally` 的 `Task.Delay(delay, ct)` 变成 `TaskCanceledException`，掩盖真实断言**；改为 `CancellationToken.None` + `WaitAsync(15s)` 兜底。
  - `TagGrpRunnerTests.StartAsync_Exception_FiresTurnCrashed` / `StartAsync_ConsecutiveFailures_GrowDelay`：去掉"到点停循环"的时限猜测，改为在碰撞回调里主动取消（攒够次数即停）。
  - `TagGrpRunnerTests.StartAsync_Cancellation_StopsLoop`：原来靠"100ms 内必须跑完一轮"，改为在第一次 `ReadAsync` 里取消。
  - `TagsProjectCtrlTests`：9 处 `await Task.Delay(200)` 猜后台启动进度，改为等待 `StartedOrStopped` 的"已启动"事件（测试侧 `ProjectStartedWatcher`，与生产侧 R3/Rx 的 `ObserveStartedOrStopped` 同一个信号）。**注意不能只等 `ctrl.Project != null`**：`Project` 在启动 hook 执行**之前**就已赋值，而 hook 里常要向项目补通道/逻辑组件，只等它就去 `StopAsync()` 会与 hook 竞态（清理不到 hook 刚加的东西）；该事件是在 hook 执行**完之后**才触发，才是"项目确实已就绪"的确定性信号。
  - `TagGrpRunnerTests.StartAsync_FailResetThenAccumulateAgain`：加了 `WaitAsync(15s)` 兜底，但"禁用后 1 秒再启用"仍是时间猜测（产品代码里禁用轮询固定 `Task.Delay(500, ct)`，测试观察不到它何时完成），根治见下一条。
  - 后续复跑（做 OpcUa 驱动那条时）观察到 **1 次未复现的失败**（约 1/36 轮，之后 36+ 轮全绿），当时未捕获失败用例名，无法定位。据此把仍是"秒级预算"的等待一次性放宽（超时只影响失败暴露的快慢，断言语义不变）：`ComTagTests` 3 处 `2s→10s`、`LineBasedComChannelTests` 2 处 `3s→10s`、`TagsProjectCtrlTests` 里 `await startTask.WaitAsync(1s)`→`10s`（停表后等启动任务收尾，最可疑的一处）、新加的轮询失败日志测试等待预算 `3s→10s`。
- [x] 根治 `TagGrpRunnerTests.StartAsync_FailResetThenAccumulateAgain` 的时序假设（**纯测试改法，产品代码不动**）：该用例要验证"失败计数在禁用一轮后复位"，而复位发生在轮询循环内部（`!IsEnabled → continue → finally`），外部观察不到它的结束时刻。改为给 mock 的 `IsEnabled` 加读取钩子（`MockTagGrp.OnIsEnabledRead`，回调先于取值生效，所以回调里改写 `IsEnabled` 后本次读取就返回新值），**第 2 次读到 false 时才重新启用**——`IsEnabled` 每轮外循环只读一次、复位就在上一轮末尾，同一线程串行，因此此刻复位必然已完成，不再需要定时器猜时机（1 秒定时器已删）。已做反证：把条件改成"第 1 次读到 false 就启用"，用例立刻以 `Expected: 1, Actual: 4` 失败，说明它确实在断言复位时刻而非静默通过。未采纳"把禁用轮询间隔做成可注入策略"的备选方案：那会为测试给生产 API 开口子，而禁用间隔只是时长问题、不是正确性问题。
- [x] 异常细化：
  - [x] 加载期：统一收敛到异常族 `TagsProjectLoadException`(**abstract**，带 `Location`，直接派生自 `Exception`) → `TagsProjectXmlException` / `TagsProjectAddressException` / `TagsProjectConfigurationException` / `TagsProjectValidationException`（`Errors` 明细；原 `TagsProjectSchemaException` 改为其子类，保留 XSD 语义与旧消息格式）。
  - [x] 运行期：结论是**不抽运行期异常族**——[TagGrpRunner](src/StdUnit.Tags/TagGrpRunners/TagGrpRunner.cs) 一律 `catch (Exception)` 交给重试策略与 `RunnerCrashed`，分型换不来任何控制流收益。因此只做「归类 + 补上下文」：库内 43 处裸 `Exception` 已全部改为语义贴近的 BCL 类型（`ArgumentNullException` / `ArgumentOutOfRangeException` / `ArgumentException` / `InvalidOperationException` / `InvalidCastException` / `KeyNotFoundException`），库代码里**已无裸 `Exception`**。
  - [x] 运行期剩余项（原「仍待办」3 条）逐条收口：
    1. `NotImplementedException` → **讨论后结论：不算误用，保留**。分两类看：(a) `TagContainer.Map` / `TagUnion.Map` 的 `_ =>` 兜底、以及各驱动"默认实现依赖通道 X，请提供自己的实现"（S7/Modbus 共 6 处）——这是**扩展点契约**："这个默认实现没有覆盖你的组合，自己实现/重写"；`NotImplementedException` 恰好是这个意思，改成 `NotSupportedException`/`InvalidOperationException` 只是口味，且会破坏"按类型 catch 再回退到自己实现"的既有用法。(b) `S7Address.ToString()/Format()` 的两个 `_` 分支**不是死分支**（`AreaKinds` 有 `None/MB/DB` 三个取值，二者只匹配 `MB`/`DB`，`None` 落到 `_`），net472 也没有 `UnreachableException` 可用（net7+ 才有），为它加垫片不值得。理由已写进 `docs/异常处理.md`。
       - [x] 连带发现并修复：`S7TagChannel` 里两个 `不支持的地址区域类型=None` 是**可达的**——组合自身的起始地址写成相对地址（`$$100`）时，`NormalizeTagAddress` 把 `Area=None` 回填给所有子测点，于是每轮轮询都抛这条指向内部概念的消息。已在 `S7TagCbntBuilder.NormalizeTagAddress` 加载期拒绝（`TagsProjectAddressException`，`Location=TagCbnt(...)`，消息说明"组合自身必须写绝对地址"），新增 1 个测试。依据：`BlockSpecified=false` 只由 `$$` 解析产生（全仓仅 `ParseRelativeAddress` 一处设置），且**没有任何代码**为组合自身的相对地址去找上层解析——即这种配置此前从未生效过，收紧不会破坏能工作的配置。运行期那两个分支保留作兜底。
    2. [x] **通信故障的消息上下文**（已做）：见下一条。
    3. ~~`TagsProjectCtrl` 清理路径的静默 `catch` 补日志~~（已合并进上面「TagsProjectCtrl 的清理路径日志化」那条，删除）。
- [x] 运行期故障消息必须能定位现场（通道/操作/地址/底层错误）：多通道入口里 `RunnerCrashed(entry, channel, ex)` 只带**主通道**，所以**异常消息本身**必须能指认是哪个通道、在做什么。
  - `S7TagChannel`：4 处读/写失败由 `err.Text`（只有 `CLI : Unknown error (0xfffffffe)`，**连通道名都没有**）改为 `通道(X) 读取DB失败：DB1.100，长度=3；CLI : Unknown error (0xfffffffe)（OsSockerError=-2;IsoTcpError=Resvd4;S7Error=-1）`；4 处 `S7通道客户端为null` 补通道名与地址；连接失败由 `result.ErrorValue.ToString()` 改为带操作与端点（`连接PLC失败：{IpAddr} Rack=.. Slot=..`）。
  - `OpcUaClientTagChannel`：6 处 `会话未创建`/`会话未连接` 补通道名与操作（读取节点/读取节点值/写入节点）。
  - `SimpleFiles`：`TagBase.ReadAsync` 包住 `ParseValue`，把 `Tag({名称})` 与**文件路径**补进 `InvalidDataException`（类型不变、原异常进 `InnerException`）——解析子类只拿得到文本，路径由基类补。
  - 日志不得把异常拍平成 `ex.Message`（丢了类型与堆栈，插件场景下 `TargetInvocationException` 更是完全不可读）：`S7TagChannel`/`ModbusTcpChannel`/`OpcUaClientTagChannel` 的释放/断开告警、`ComChannelBase` 的轮询失败、`LogicetLoader` 的加载/构建失败、`ScriptBasedComChannel` 的脚本编译失败、`TagGrpRunner` 的错误处理器再抛错，统一改为 `LogXxx(ex, ...)` 重载。
  - **刻意不做**：不为加前缀去包装第三方异常（`SocketException`/`OpcException`/NModbus）。它们自带端点与协议信息，包装会改变调用方可 catch 的类型（1.0 前不该做这种收紧）。
  - 新增测试：S7 通道 5 处断言通道名/操作/地址/长度/错误码明细，OpcUa 通道 3 例（含"连接后掉线"），SimpleFiles 2 例断言测点名与文件路径，ComScanner 1 例断言轮询失败日志带通道名**且带异常对象**。
- [x] 为项目描述 XML 引入 schema 校验机制：XSD 校验（核心 `tagsproject.xsd` + 各驱动 `Schemas/*.xsd`，XSD 1.0 命名空间模块化；运行期可选校验 `EnableXmlSchemaValidation()`，编辑器经 buildTransitive 自动注入 XSD 获得智能提示；第三方 schema 经 `ITagsProjectSchemaProvider` 合并）。
- [x] 加载期交叉引用校验（XSD 表达不了）：`channel` 属性必须能在已声明的 `<Channel>` 中找到，拼错的通道名在加载期报错而不是运行时才暴露；错误带完整路径上下文。统一抽象为 `ITagsProjectValidator`（可注册多个实现）：`EnableXmlSchemaValidation()`（XSD）、`EnableCrossReferenceValidation()`（channel 引用 + `ChannelDriverFactoryValidator`：driver 必须对应已注册的通道工厂，未注册的驱动名在加载期报错；`NestedEntryValidator`：嵌套在入口内的 `isEntry="true"` 不生效，拒绝；`EntryChannelExclusivityValidator`：同一通道不得被多个入口共用，拒绝）；第三方可用 `AddValidation<TValidator>()` 追加。
- [x] 单入口多通道：入口子树中所有会被用到的通道统一建连与断开。`ITagGrpExtensions.CollectChannels()` 向下递归收集；`TagGrpRunner` 每轮逐一 `EnsureConnectedAsync(force:false)`，清理路径 `DisconnectAllAsync` 断开全部通道（先全部发起再按 `ITagGrpRunnerDisconnectStrategy` 逐一等待）。语义：事件委托的 `channel` = 入口**主通道**；**一个通道 = 一个轮询回路**，同一通道实例不得跨入口共用——由默认启用的 `EntryChannelExclusivityValidator` 强制。理由不止"断开互相踩"（连接动作可为空实现，如 SimpleFiles），更根本的是两套扫描周期会交错读写同一底层资源、且两个入口的生命周期被耦合；而要求分离的代价几近为零（再声明一个指向同一设备的 Channel 即可）。
  - 注：入口识别（`ScanEntries`）遇到入口即停止下探 ⇒ **只有最外层入口才是入口**，嵌套 `isEntry="true"` 不生效（该子树仍由外层入口轮询）；错误配置由默认启用的 `NestedEntryValidator` 拒绝。
- [ ] 文档完善和更新：`docs/` 放框架性内容，详细使用说明放独立文档库。
- ~~OpcUa和ModbusTcp单通道多入口并发支持~~ **已废弃（不做）**。原说明：当前S7已经做了单通道多入口的串行化，不过OpcUa和ModbusTcp目前只支持“单通道单入口”模型。这可能是一个值得改进的方向，但优先级不是很高：第一，我认为在工业交互的场景下，“单通道单入口”串行轮询机制更合理；其次，对于单设备多入口场景，可以手动建立指向单一设备的多个通道来解决——哪怕是`C#`官方类库，也没有强迫`TcpClient`是线程安全的；第三，多入口并行在很多场景下，对于没有思索过具体细节的新手用户，会带来非常多的困扰。简单的说，我更推崇**单入口多通道**模式。如果将来真要做单通道支持入口并行化，可以参考 ComScanner/S7 设计横展。
  - 废弃补充理由：**这个用法现在会被默认校验器直接拒绝**——`EntryChannelExclusivityValidator`（`UseDefaults = true` 时默认开启）不允许同一通道被多个入口共用，所以“OpcUa/ModbusTcp 不支持单通道多入口”不是潜在缺陷，而是有明确报错的既定约束。真要做单通道多入口并行，必须先让该约束变成可选（那等于放弃“一个通道实例只属于一个入口”的设计，见 `AGENTS.md` 约束 3），代价远大于收益。

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