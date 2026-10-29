# Changelog

本文件记录**用户可见**的变化。版本号遵循[语义化版本](https://semver.org/lang/zh-CN/) `<major>.<minor>.<patch>`。

## 2.0.0-beta5(尚未发布)

> 本批仍是 `2.0.0` 的预发布——`2.0.0` 正式版保持未发布。

主题：**取消令牌贯穿全部运行期回调**（并把"取消"从"崩溃"里摘出来），以及**新包 `StdUnit.Tags.WebUi`**（SSE 值流 + 结构树 + 设计器）。

### 破坏性变更

- **`TurnProcess` 增加取消令牌**：签名由 `Task TurnProcess(ITagGrp grp, ITagChannel? channel)`
  变为 `Task TurnProcess(ITagGrp grp, ITagChannel? channel, CancellationToken ct)`（与 beta4 的 `ILogicet.ProcessAsync` 一致）。
  - 订阅 `ITagGrpRunner.TurnProcess` 的 lambda / 方法组要补参数；二进制层面同样是破坏性的，**旧版本编译出的程序集需要重编**。
  - 这里新增的`ct` 代表**项目生命周期令牌**（`ITagsProjectCtrl.StopAsync()` 时取消）；回调里发起的异步操作应把它透传下去。
- **`ITagsProject` 新增成员 `SetTurnProcessHandler(string entry, TurnProcess? handler)`**：
  这是**公开接口新增成员**——自定义实现 `ITagsProject` 的代码（多为测试替身）必须补上实现；只调库、不实现该接口的代码不受影响。
- **`RunnerStarted` / `RunnerCrashed` 增加取消令牌**：
  - `RunnerStarted`：`(ITagGrp grp, ITagChannel? channel)` → `(ITagGrp grp, ITagChannel? channel, CancellationToken ct)`；
  - `RunnerCrashed`：`(ITagGrp grp, ITagChannel? channel, Exception ex)` → `(ITagGrp grp, ITagChannel? channel, Exception ex, CancellationToken ct)`。
  - 订阅方的 lambda / 方法组需要补参数；用旧版本编译出的订阅代码需要重编。**`ct` 同样是项目生命周期令牌**。
  - `RunnerCrashed` 的 `ct` 进入时通常是**未取消**的（取消不再是崩溃，见下）；但回调执行期间项目可能被停止，
    令牌随时可能取消——"必须完成"的清理/上报请改用 `CancellationToken.None`。
- 示例（`samples/NixMonitor`、`samples/WebMonitor`、`samples/WpfDemo`）已按新签名更新。

### 行为变更

- **取消不再被当作崩溃**：项目停止引起的取消不再触发 `RunnerCrashed`、不计入连续失败、也不按重试策略退避
  （该入口子树里的全部通道仍会被断开，`StartAsync` 仍以 `OperationCanceledException` 结束）。
  此前每次正常停机都会上报一条 `TaskCanceledException` 崩溃，在监控里表现为"停机即故障"。
  - 判据是**令牌确实已取消**而不是只看异常类型：驱动因自身原因（如设备超时）抛的 `OperationCanceledException` 仍是崩溃。
  - 同理，回调（`ILogicet.ProcessAsync` / `TurnProcess`）在**令牌未取消**时抛 `OperationCanceledException` 依旧是失败；
    停机取消时才不再计入失败。
- 取消时不再空等退避延迟（`ITagGrpRunnerRetryStrategy` 仍按既有契约每轮被询问一次，只是不再真的等待）。
- `SetTurnProcessHandler` **运行前、运行中都可以设置/替换/清除**，运行期改动从下一轮开始生效（回调表写时复制，
  轮询线程零锁）。每个入口只有一个回调，重复设置是替换——需要多个观察者请自行组合成一个 `TurnProcess`。

### 新增

- **新包 `StdUnit.Tags.WebUi`**（仅 `net8.0`，ASP.NET Core；不依赖 Blazor / SignalR，可在 NativeAOT 宿主下运行）：
  - 监控：结构树走一次性 `GET /stdunit.tags/tree`，值按"入口 + 轮次"以 SSE（`GET /stdunit.tags/stream`）推送，内置极简监控页 `GET /stdunit.tags/ui`。
  - 设计器：把核心 + 各驱动 schema 内省成元数据（`GET /stdunit.tags/catalog`），设计器页 `GET /stdunit.tags/designer` 据此渲染表单、
    导入既有项目 XML 并导出（未知驱动/元素/属性原样保留）。
  - 写命令：`POST /stdunit.tags/value`（改测点值）、`POST /stdunit.tags/rescan`（标记未扫描）。
  - 注册：`services.AddTagsStream()` / `app.MapTagsStream()`；多项目场景用 `AddKeyedTagsStream(key)` 注册具名流（路径形如 `/stdunit.tags/{key}/stream`）。
    只允许改**路由前缀**（`app.MapTagsStream("/internal/tags")`），相对路径是线协议的一部分。
  - **不内置任何鉴权**，暴露到非受信网络前请自行加策略（如 `app.MapTagsStream().RequireAuthorization()`）。
- **`AddKeyedTagsProjectCtrl(key)`**（`StdUnit.Tags`，`IServiceCollection` 与 `TagsProjectServiceBuilder` 上各一个）：
  以指定 key 注册 `ITagsProjectCtrl`；配合 `AddKeyedTagsStream(key)` 可以在一个宿主里同时监控多个项目。
- **驱动 XSD 增加设计器元数据**：核心与各驱动 schema 用 `xs:annotation/xs:appinfo` 声明 `<driver name=… displayName=… prefix=…>`，
  以及驱动自有属性（`<tagAttribute>` / `<cbntAttribute>`）。**只供设计器/内省使用，不参与校验**——现有 XML 的校验行为不变。
- 新示例 `samples/WebMonitor`（Web 监控 + 设计器演示）与 AOT 冒烟测试 `tests/AotSmokeTest.WebUi`（CI 的 `aot-smoke` job 覆盖）。

## 2.0.0-beta4

> 本批仍是 `2.0.0` 的预发布——`2.0.0` 正式版保持未发布。

主题：**逻辑组件能拿到项目声明周期的取消令牌**。

### 破坏性变更

- **`ILogicet.ProcessAsync` 增加取消令牌**：签名由 `Task ProcessAsync(ITagGrp entry, ITagChannel? thisChannel)`
  变为 `Task ProcessAsync(ITagGrp entry, ITagChannel? thisChannel, CancellationToken ct = default)`（`LogicetBase` 的抽象方法同步修改）。
  - **调用方不用改代码**：`ct` 是可选参数，`logicet.ProcessAsync(entry, ch)` 照旧编译，取值为 `default`。
  - **实现方要改签名**：实现 `ILogicet`、或继承 `LogicetBase` 并 override 该方法的代码必须补上参数；二进制层面同样是破坏性的，
    **旧插件 dll 需要重编**（与 beta3 的迁移动作相同）。
  - **`ct` 的语义是项目的生命周期**：`TagsProjectCtrl.StopAsync()` 时取消，**不是**"单轮超时"——框架不会为某一轮单独取消，
    同一入口的每一轮拿到的都是同一个令牌。停机时想尽快收手请用 `if (ct.IsCancellationRequested) return;`；
    **不要**在 `ProcessAsync` 里抛 `OperationCanceledException`，它会被运行器当成一次失败（触发 `RunnerCrashed`、按策略断开重连）。
    "回调必须尽快返回"的约束不因新增令牌而放松。
  - **影响面**：只调库、不实现/继承逻辑组件的终端 App **编译与行为都不变**。
- 示例与 AOT 冒烟测试（`samples/Samples.Plugin1`、`samples/StdUnit.Tags.Web`、`samples/WpfDemo`、`tests/AotSmokeTest.LogicetPlugins`）已按新签名更新。

## 2.0.0-beta3

> 本批仍是 `2.0.0` 的预发布——`2.0.0` 正式版保持未发布。

主题：**逻辑组件（`ILogicet`）改为持有整个测点项目**，构造函数不再逐个注入通道集与测点树。

### 破坏性变更

- **`ILogicet` 新增 `Project`、移除 `Channels` / `Tags`**：逻辑组件改为通过 `Project.Channels` / `Project.Tags` 访问通道与测点。
  `LogicetBase` 仍保留同名的 `Channels` / `Tags` 便捷属性（等价于 `Project.Channels` / `Project.Tags`），所以继承 `LogicetBase` 的子类里
  `this.Tags.SelectCbnt(...)` 这类写法**不用改**；要从 `ILogicet` 变量上读的代码需要改成 `logicet.Project.Tags` / `logicet.Project.Channels`。
- **`LogicetBase` 的构造函数改为 `LogicetBase(ITagsProject project)`**：旧的 `(IReadOnlyList<ITagChannel> channels, ITagGrp tags)` 重载已删除。
  继承 `LogicetBase` 的子类需要把构造函数改成接收 `ITagsProject` 并 `: base(project)`；原来用参数 `tags` 的地方改用 `this.Tags`
  （构造期间通道与测点已加载完成，可直接使用）。
- **`ILogicetCreator.CreateLogicet` / `ILogicetsLoader.LoadLogicets` 的参数由 `channels + tags` 改为 `ITagsProject`**：自己实现这两个扩展点的代码需要跟着改。
- **旧插件 dll 必须重编**：上面两处签名变化都是二进制层面的——用旧版本编译出的插件在加载期会创建失败（日志中是"构建Logicet错误"或"加载Logicet失败"），
  不支持"只替换 dll"的升级方式。
- **影响面**：只调库、不继承 `LogicetBase`、不实现上述接口、也不从 `ILogicet` 上读 `Channels` / `Tags` 的终端 App，**编译与行为都不变**。
- 示例与 AOT 冒烟测试（`samples/Samples.Plugin1`、`samples/StdUnit.Tags.Web`、`samples/WpfDemo`、`tests/AotSmokeTest.LogicetPlugins`）已按新签名更新。

## 2.0.0-beta2

> 本批仍是 `2.0.0` 的预发布——`2.0.0` 正式版保持未发布。

主题：**S7 的读写改成"调用方提供缓冲区"的同步方法**——缓冲区一路透到硬件层，读、写两条路径都不再产生 `byte[]`。

### 破坏性变更

- **`S7TagChannel` 的字节读写改成同步方法**（方法名也从 `ReadAsync`/`WriteAsync` 变为 `Read`/`Write`）：

  | `2.0.0-beta` 及以前 | `2.0.0-beta2` |
  |---|---|
  | `Task<byte[]> ReadAsync(string address, int length, CancellationToken ct)` | `void Read(string address, Span<byte> buffer, CancellationToken ct)` |
  | `Task WriteAsync(string address, byte[] bytes, CancellationToken ct)` | `void Write(string address, Span<byte> buffer, CancellationToken ct)` |

  读取长度由 `buffer.Length` 决定。**只能是同步的**：`Span<T>` 是 ref struct，既不能跨 `await` 存活、也不能作为 `Task` 的结果返回；
  而底层 S7 调用本来就是同步阻塞的，原来那层 `Task` 只是伪装。读写失败依旧抛 `InvalidOperationException`（未连接、或 PLC 返回错误码）。
- **删除了 `IContinuousBytesBasedTagChannel`（`StdUnit.Tags.Core` 的公开接口）**。它表达的是"能以字节方式连续读写的通道"，
  但删除前**唯一的实现者就是 `S7TagChannel`**（Modbus 早就有自己的 `IModbusBitsChannel` / `IModbusRegisterChannel`），
  没有第二个使用者的抽象不再保留。
- **S7 组合测点要求通道必须是 `S7TagChannel`**：`S7TagCbnt` / `S7TagCbntorBase` 由"按接口判定"改成"按具体类型判定"，
  于是"自己实现一个字节通道来驱动 S7 组合子"的用法不再可行——改为继承 `S7TagChannel`（`Read` / `Write` 仍是 `virtual`；
  `EnsureConnectedAsync` / `DisconnectAsync` / `Dispose` 的签名都没变）。
- **影响面**：只调库、不继承 `S7TagChannel` 的终端 App **编译与行为都不变**（`ITag` / `ITagCbnt` / `ITagGrp` 本次全部未动）；
  需要动手的是自行继承/实现 S7 通道的代码，以及把 S7 通道当"连续字节通道"传递/判定的代码。

### 修复和改进

- **S7 读、写两条路径都不再分配 `byte[]`**：组合式测点直接读写 `TagCbnt<byte>` 的缓存，直接测点用 `stackalloc` 临时缓冲；
  前者顺带省掉了"先读进临时数组、成功后再拷回缓存"的那次拷贝（缓冲区就是缓存本身）。
- **`Read` / `Write` 的 `ct` 只作用于等待 I/O 互斥体**（连接/断开可能长时间占用它），底层 S7 调用本身仍不可取消——
  可取消性与 `2.0.0-beta` 及以前**一致**（`StopAsync` 的有界等待因此仍然有效）。新增确定性回归测试覆盖"互斥体被占用时 `ct` 能中断等待"。
- **S7 读写失败的消息更准**：地址一律用地址自身的文本（`MB` 区域此前会被拼成 `DB0.x`），措辞从"读取DB失败 / 读取MB失败"
  统一为"读取失败 / 写入失败"（区域信息已经含在地址里）；写入失败的消息同样带上了长度。
- 依赖：**`StdUnit.Sharp7` 升到 0.8.1**（多目标 `net6.0` / `netstandard2.0` / `netstandard2.1`）。因此 `net8.0` 侧取 `net6.0` 资产。

## 2.0.0-alpha

> `2026-10-10`发布`2.0.0-alpha`。使能语义相关改动自 `2.0.0-alpha` 起可用；AOT 支持相关改动自 `2.0.0-beta` 起可用；S7 读写路径的破坏性改造自 `2.0.0-beta2` 起可用。

本次更新有两个主题：

1. **使能**（`isEnabled`）的语义定死为"**只作用于自动轮询，且自顶向下短路整棵子树**"，并为此引入遍历模式 `TraversalMode`（新增公开枚举，取值 `IgnoreEnabled` / `RespectEnabled`）。下面的条目基本都是它的连带影响。
2. **AOT（NativeAOT / 裁剪）支持**。

### 破坏性变更

- **`ITagGrp` 的读写方法带上了遍历模式**：`ReadAsync(CancellationToken)` / `WriteAsync(CancellationToken)` 变为
  `ReadAsync(TraversalMode, CancellationToken)` / `WriteAsync(TraversalMode, CancellationToken)`。
  - **调用方不用改代码**：少传参数的重载仍然可用，只是从接口方法降级为扩展方法（`ITagGrpExtensions.ReadAsync(ct)`；`TagUnion` 上同理，见 `TagUnionExtensions`），语义等价于传 `TraversalMode.IgnoreEnabled`，与历史行为一致。
  - **实现方要改签名，而且必须透传 `mode`**：通常开发者不必实现自己的`TagGrp`，但是如果自己实现了`ITagGrp`的，需要补上参数，并且要把 `mode` 透传给子节点——不透传的话门控会在那一层断掉（父节点未使能，子节点却照样读写）。驱动层实现的是 `ITag` / `ITagCbnt`，这两个接口本次**没有变**（`ITagCbnt` 只改了注释），所以各驱动包的作者基本不受影响；只调库、不实现这些接口的终端 App 则完全不受影响。
- **`IsEnabled` 的生效范围从"只有入口"扩到"整棵树"**（这条才会真的改变现场行为）：2.0 之前，`IsEnabled` 在库内**只有一处**被读取——`TagGrpRunner` 见到**入口**未使能就整轮跳过；中间层的分组/组合写 `isEnabled="false"` 是**空操作**，读写照旧发生（手动读写则从来不检查它）。现在`IsEnabled`下沉到遍历过程本身、**自顶向下短路**：某一层未使能，它整棵子树既不读也不写（脏标记保留，重新使能后照常刷写）。 

### 修复和改进

#### `IsEnabled`语义：

- **"`isEnabled` 不影响手动读写"这件事被写进了契约**：此前它只是"没人去检查"，现在由 `TraversalMode.IgnoreEnabled` 明确表达，
  并成为单参扩展方法（`ITagGrpExtensions.ReadAsync(ct)`、`TagUnionExtensions`）的固定语义——行为不变，含义变清楚。
  于是"平时禁用、需要时手动读一次/点动一下"是受支持的用法（就像 PLC 里下了使能，人工仍可操作）。
- **自动轮询统一按 `TraversalMode.RespectEnabled` 遍历**（`TagGrpRunner`）：入口级跳过与 2.0 之前一致，新增的是中间层也会短路；
  被跳过的子树**保留脏标记**，重新使能后该刷的照常刷。
- **`ITagCbnt` / `ITag` 的读写方法**不带** `mode`。原因是`ITagCbnt`是整体读写，不需要递归；而`ITag`既可能是`DirectTag`，也可能是`ITagCbntor`，语义上不自洽, 上干脆没有 `IsEnabled`。
  想单独停采某个直连测点，套一层 `isEnabled="false"` 的 `TagGrp` 即可。
- 文档：`ITagGrp.IsEnabled` / `ITagCbnt.IsEnabled` 的注释写明"自动轮询 + 短路语义"；`ReadAsync` / `WriteAsync` 的注释写明
  `mode` 的两种取值与"实现方必须透传"。

#### `AOT` 支持

- 核心包与各驱动包现在都做了AOT适配，并标注了 `IsAotCompatible`，可以被 `PublishAot=true` 的宿主直接引用；
- 扩展包`StdUnit.Tags.RxExtensions`和`StdUnit.Tags.BlazorLib.Core`由于底层 对 AOT 支持不好，暂时不提供AOT支持;

新增公开成员：

- `TagsProjectNotSupportedException`：表示"配置本身合法，但当前宿主运行时没有该配置所需的能力"。与 `TagsProjectConfigurationException` 的分界是**换一个宿主是否能跑**。
- `AddJsonSerializerContext(JsonSerializerContext)`：SimpleFiles 的 JSON 测点（`type="JSON"`）在 AOT / 裁剪宿主下必须用它登记源生成元数据。

两项由于`AOT`引入，带来了两条**行为变更**（只影响 AOT / 裁剪宿主，普通 JIT 宿主完全不变）：

- 由于 NativeAOT 的运行时没有动态加载程序集的能力（官方限制），所以现在一旦检测到“AOT+动态插件”这种组合，直接在加载期抛异常。异常消息里给出建议措施。
- 由于 JSON 的AOT需要提前注册类型信息，所以现在 SimpleFiles 一旦检测到“AOT+未登记SimpleFiles JSON测点元数据”这种组合时，直接在加载期抛异常。

细节与排错见文档站 [AOT 支持](https://tags.doc.stdunit.com/部署专题/AOT支持.html)。

## 1.1.0

> 首发日期：`2026-10-08`。
> [docs/设计决策与边界/1. CI.md](docs/设计决策与边界/1.%20CI.md)。

本次更新集中在`StdUnit.Tags.ModbusTcp`这个类库。

### 行为变更（破坏性）

> **本批变更只涉及 `StdUnit.Tags.ModbusTcp`**——它在 README「文件夹结构与驱动支持」里标记为**实验**，
> 而实验包的 `minor` 跳变可能包含破坏性更新（"支持"包只在 `major` 跳变时才会）。
> 影响面核对（对照 `1.0.0`）：改动全部落在 `StdUnit.Tags.ModbusTcp`；**编译层面无破坏**——没有被改名/删除的公开类型
> （被重构的 `Modbus*DirectTag`、`Modbus*Cbntor`、`interpret` 相关类型都是 `internal`，`IModbusRegisterChannel` 只改了注释），
> 公开面只有新增成员（如 `ModbusBitTagCbntBuilder.Slave`）。
> **行为层面的破坏全部落在点表（XML）语义上**：`endian` 的含义、多出来的 `interpret`、`TagCbnt.slave` 生效、参考号 0 报错——
> 修的都是"同一份 XML 换一种写法（`<TagGrp>` / `<TagCbnt>`）就得到另一个值且不报错"这类静默错误。
> Modbus 驱动目前除 DI/DO 外没有现场在用，因此按 minor 发布。
> 直接引用`StdUnit.Tags.ModbusTcp`中类型的驱动开发者才需要注意（像 ZLan / Hjzk 那样继承 `ModbusBitTagCbntBuilder` / `ModbusRegisterTagCbntBuilder` 的驱动）：
> `WithCbntDescriptor` 现在会消费 `slave` 并改写 `TagCbnt.StartAddress`，override 时请调用 `base`。

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
  新增回归测试 `ModbusEndianWireTests`（两条承载路径各自断言同一个物理值，覆盖全部寄存器/位类型）与
  `ModbusInterpretTests`（64 位四种排布、与 `endian` 的相容性、各类加载期报错）。

### 修复和改进

- **ModbusTcp：`TagCbnt` 的 `slave` 属性真正生效**。此前**位**空间构建器会解析并校验它、**寄存器**空间构建器连解析都没有，但两者都没有把从站号并入寻址地址，于是 `slave="2"` 静默等同于 `slave="1"`（点位一直落在 1 号站）。现在两个空间都会在加载期把它合成到组合的起始地址：`address="10001" slave="2"` 等同于 `address="2~10001"`；地址里已经写了 `~` 前缀时以 `slave` 为准，`~` 之后的原文（如 `00020` 的前导零）保持不变。组合内的子测点地址仍是相对偏移，从站号一律由组合决定。
  **升级提示**：如果你此前在 `TagCbnt` 上写过 `slave="2"` 而点位其实一直落在 1 号站，升级后它会按字面生效——请核对这类配置是笔误还是有意为之。
- **ModbusTcp：`ModbusTcpAddress.ToString()` 修复线圈地址的回程错误**。线圈（`Area=OutputCoils`）的参考号是 `0xxxx`（`00001` 即 1 号线圈），此前的实现只做基址相加、不补前导零，`StartPoint=19` 会被格式化成 `1~20`——这个串再解析回来是 **1 号区域**（离散输入）的 20 号点，而不是 0 号区域的线圈。现在按 5 位补零输出 `00020`，四个区域的 `ToString()` 与 `ModBusTcpAddressParser.Parse` 可正常互转。
- **ModbusTcp：参考号 0（`00000` / `10000` / `30000` / `40000`）在加载期报错**（此前静默指向协议地址 65535）。Modbus 参考号从 1 开始，`x0000` 没有对应的点；旧实现里 `start -= 1` 会下溢成 `65535`，于是 `<Tag address="40000" ...>` 会被解释成一个既不报错、也访问不到的点。
  **升级提示**：如果配置里用的是 0 起算的写法（想表达"区域里的第 1 个点"），请改成参考号 `x0001`（如 `40001`）。
- **ModbusTcp：地址解析的报错更准确**。形状对、内容非法时（位号越界、从站号越界、参考号越界）报真正的原因，而不是另一套写法的"未能匹配模式"；位号/从站号的提示改为写明取值范围（`0~15` / `0~255`）。

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

