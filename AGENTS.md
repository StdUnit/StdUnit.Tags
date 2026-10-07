# AGENTS.md — StdUnit.Tags

面向在本仓库中工作的 AI 编码代理。**人类用户文档**见文末「文档」。

## 这是什么

面向工业硬件交互的 .NET 类库家族（`net8.0`，MIT，跨平台）。核心模型：

```text
测点项目 ITagsProject = 通道 ITagChannel + 测点树 ITagGrp + 逻辑 ILogicet
```

每个 `isEntry="true"` 的测点组分配一个 `ITagGrpRunner`，按**串行轮询**运行：
【执行意图 → 读取输入 → 处理逻辑 → 刷写输出】。**入口之间并行，入口内部严格串行**。

## 构建 / 测试（与 CI 一致）

```powershell
dotnet tool restore                    # 见 .config/dotnet-tools.json
dotnet paket restore                   # 见 paket.dependencies
dotnet build StdUnit.Tags.sln
dotnet test --no-build --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

CI（`.github/workflows/dotnet.yml`）有**两个 job**：
- **`build`（ubuntu-latest，权威）**：整解 `dotnet build StdUnit.Tags.sln -c Release`（含 net472 的**编译**校验——引用程序集由 SDK 隐式引入的 `Microsoft.NETFramework.ReferenceAssemblies` 提供）+ `dotnet test -f net8.0`（带覆盖率上报 codecov）。
- **`net472-on-mono`（ubuntu-22.04）**：用镜像**预装的 Mono** 跑 `dotnet test -f net472`，让 net472 的**运行**测试也自动化、又不占 Windows runner 的排队时间。**mono ≠ .NET Framework**，它绿只说明"大概率没退化"，（权威结果仍归发版工作流 `release-nuget.yml` / `release-baget.yml`，windows-latest 全 TFM）+ 本机 Windows。当前是**硬性把关**（未设 `continue-on-error`）；万一出现 mono 专有的伪失败，可在 job 上加 `continue-on-error: true` 降级为补充信号。mono job 红时先看是不是"mono 与真框架的差异"，别急着改产品代码。
- 为何钉 `ubuntu-22.04`：只有该镜像预装 Mono（`ubuntu-latest`/24.04 没有）；镜像退役后改成 `apt-get install -y mono-complete` 即可。
- 参考实测（2026-10，WSL/Ubuntu 22.04 + mono 6.8）：net472 **1028 全绿**，与 Windows 真 .NET Framework 的 1028 逐一致。
- 改了 `Compat/*` 或 `#if NETFRAMEWORK` 分支时，仍建议在本机 Windows 上跑一次 `-f net472`（或在 PR 里看 `net472-on-mono`）。
`samples/WpfDemo`（net8.0-windows + WPF）**已从解决方案移除**，目录保留、需要时在本机 Windows 上单独 `dotnet build samples/WpfDemo/WpfDemo.csproj`（只要它还留在解决方案里，Linux 上的整解构建就会失败）。

- SDK 固定为 **8.0.102**（`global.json`，`rollForward: minor`）。
- 依赖用 **Paket** 管理。**不要 `dotnet add package`**：请改 `paket.dependencies` 后运行 `dotnet paket install`（会更新 `paket.lock`）。
- 包源有两个：nuget.org 与私有测试源 `https://baget.stdunit.com/v3/index.json`（**后者只是本仓库自身的构建源**——`paket.dependencies` 里列着它；包已发布到 nuget.org，消费方不需要知道私有源）。离线编译依赖 `nuget-package-caches/`。
- `paket.dependencies` 普遍使用 `lowest_matching: true` ⇒ 默认取**最低匹配版本**；升级必须显式改版本号。
- 改了 `paket.dependencies`/`paket.lock` 后若 `obj/<proj>.<tfm>.paket.resolved` 没刷新，删掉 `paket-files/paket.restore.cached` 与 `obj/*.paket.resolved`、`*.paket.references.cached` 再 restore。

## 目标框架（多目标）

**Core / StdUnit.Tags / Rx / R3 / 全部驱动包（S7、ModbusTcp、Hjzk、ZLan、OpcUaClient、ComScanner、SimpleFiles）/ McpServer 都是 `net8.0;net472`；仅 BlazorLib(.Core) 仍是 `net8.0`。**

> McpServer 走 `ModelContextProtocol`（核心包）的 `netstandard2.0` 资产。
> `ModelContextProtocol.AspNetCore` 是 net8-only，与核心包同在 `Mcp` 组，但只被 net8-only 的项目引用，因此不影响 net472。

新增代码时的约定：

- **有标准库就用标准库。** 跨框架差异用调用点的 `#if NETFRAMEWORK` 或项目内 `Compat/` 目录的单点垫片（如 `StdUnit.Tags.Core/Compat/ReferenceEqualityComparer.cs`、各驱动的 `Compat/*.cs`）。
- **不要把 `#if` 铺到调用点满屏幕，也不要用 `#if` 去条件修饰公共 API**（那会给库使用者制造两套签名）。
- `ImplicitUsings`/`Nullable`/`LangVersion`/`GenerateDocumentationFile` 由 `src/Directory.Build.props` 统一提供，新建项目不用再写这几项（net472 默认 C# 7.3，少写一次就少一次 CS8630）；`TargetFrameworks` 仍写在各 csproj（存在 net8-only 项目）。不生成 XML 文档的项目在本项目显式写 `<GenerateDocumentationFile>false</GenerateDocumentationFile>`（现有：BlazorLib、两个测试项目；BlazorLib 是"尚未补注释"的历史包袱）。
- net472 缺 init-only setter 时，在 `paket.references` 里加 `IsExternalInit`（已有先例：Core / StdUnit.Tags / ComScanner / OpcUaClient / SimpleFiles）。
- **net472 的引用程序集没有可空标注**，`string.IsNullOrEmpty` 之类的 BCL 后置条件看不到，会出现 net472 专属的 CS8601/CS8603/CS8604 假阳性。改用语言级判空（`x is null || x.Length == 0`），**不要用 `NoWarn` 压掉**。
- 不要用 `| Select-Object -First n` 过滤 `dotnet paket`/`dotnet build` 的长输出：会在第 n 行处终止上游进程。先 `Out-File` 再 grep。

## 项目和依赖分层

```text
StdUnit.Tags.Core                硬件无关的核心抽象 + Schemas/tagsproject.xsd
├── StdUnit.Tags                 项目 / 日志 / 插件 / DI，仍与硬件无关
│   ├── StdUnit.Tags.S7
│   ├── StdUnit.Tags.ModbusTcp
│   │   ├── StdUnit.Tags.Hjzk    建立在 ModbusTcp 之上
│   │   └── StdUnit.Tags.ZLan    建立在 ModbusTcp 之上
│   ├── StdUnit.Tags.OpcUaClient
│   ├── StdUnit.Tags.ComScanner
│   ├── StdUnit.Tags.SimpleFiles
│   ├── StdUnit.Tags.McpServer
│   └── StdUnit.Tags.BlazorLib(.Core)
└── StdUnit.Tags.RxExtensions / R3Extensions     只依赖 Core
```

- `StdUnit.Tags.SchemaGenerator` 是源生成器，把各项目的 XSD 编译成 DLL 内字符串常量（AOT / trim 友好）。
- 新增驱动包时，需一并提供 `Schemas/{driver}.xsd`，并以 `buildTransitive` 方式注入到消费方项目树（照抄 `StdUnit.Tags.Core.csproj` + `Schemas/StdUnit.Tags.Core.targets` 的模式），并实现 `ITagsProjectSchemaProvider` 注册为 Singleton。
- 测试分两个项目（xUnit），且**主测试项目多目标 `net8.0;net472`**：
  - `src/StdUnit.Tags.Tests`：主体测试，跑在两个 TFM 上；
  - `src/StdUnit.Tags.Tests.NetCoreOnly`：**仅 net8.0**，专门存放无法面向 net472 的测试（如依赖 `AssemblyLoadContext`，或将来涉及 Blazor / ASP.NET Core 的测试）。目前为空项，作占位预留。
- **新增测试默认放 `StdUnit.Tags.Tests`**（多目标，两个 TFM 都跑）；只有当测试依赖 `net8.0` 专属的包/项目（如 `ModelContextProtocol.AspNetCore`、`BlazorLib`）或 `net8.0` 专属运行期 API（如 `AssemblyLoadContext`）时，才放 `Tests.NetCoreOnly`。
  不要在 `StdUnit.Tags.Tests` 里用 `#if NETFRAMEWORK` 排除整个文件——那是编译期开关，解决不了“引用 net8-only 项目”的问题（目前仅 `Core/Logicets/LogicetPluginLifecycleTests.cs` 因这一原因用 `Compile Remove` 在 net472 下排除）。

## 不可破坏的设计约束

1. **公开面 = 接口 + DI 扩展 + 工厂/构建器。** `TagsProject`、`TagsProjectCtrl`、`TagGrpRunner`、`TagGrpRunnerFactory`、`LogicetLoader`、`SimpleFilesDirectTagFactory` 等具体实现一律 `internal`。不要把新的具体实现暴露成 public——用户代码只应通过接口 + DI 使用。
2. **入口内严格串行。** `ILogicet.ProcessAsync` 与 `TagGrpWriteIntent` 都跑在轮询线程上，必须尽快返回。不要在这些路径上同步阻塞（`.Result` / `.Wait()` / `Thread.Sleep`），也不要把这些回调改成"等待外部事件"的形态——那会与 `WriteIntent` 构成环路等待死锁。
3. **一个 `ITagChannel` 实例只能属于一个入口。** 运行器崩溃/取消时会断开入口子树中的**全部**通道（`DisconnectAllAsync`），`EntryChannelExclusivityValidator` 依赖此约束。改动清理逻辑时务必保持该语义。
4. **库不做任何隐式数值转换。** 测点值类型必须精确匹配（`short` 不能写给 `int`）。`GetTagValue<T>()` 是硬转换，不做兼容处理。
5. **`ITag.Channel` 是只读属性**（`ITagGrp.Channel` / `ITagCbnt.Channel` 可读写）；**`ITagGrp.IsDirty()` 是方法**，而 `ITag.IsDirty` / `ITagCbnt.IsDirty` 是属性。这些不统一之处是刻意为之(比如`ITagGrp`是否已经是脏数据只依赖于其任意子节点是否已脏)。
6. **校验器默认值**：4 个代码校验器（跨引用、驱动工厂、入口通道独占、嵌套入口）在 `UseDefaults = true` 下**默认开启**；XSD 校验（`EnableXmlSchemaValidation()`）**默认关闭**（opt-in）。
7. **解析大小写敏感度**：`EndianKinds` / `TagAccessMode`（`LittleEndian`、`R1W` …）**大小写敏感**；`isEntry` / `isEnabled` **不敏感**。测试常覆盖这些边界。
8. **`IntentCapacity` 必须 > 0**，且只能在 `RunAsync` 之前设置。

## 约定

- **时间戳一律 UTC**：`ITag.Timestamp` 用 `DateTime.UtcNow` 写入，**不要用 `DateTime.Now`**（本地时间跨时区/夏令时不可比）；展示或与本地时间比较时由调用方自行转换。
- **启停语义**：`ITagsProjectCtrl.StopAsync()` 会先**等轮询循环退出**（有界超时，默认 30s）再释放项目、断开通道、触发"已停止"事件；`StartPollAsync` 的 `finally` 是项目的**唯一常规释放点**（只有等待超时那一刻才由 `StopAsync` 兜底释放，因此"只释放一次"是硬约束）。改启停或清理路径时必须保持这个顺序，详见 `docs/设计决策与边界/`。
- 公共编译设置（`ImplicitUsings` + `Nullable` + `LangVersion latest` + XML 文档文件）与 NuGet 包元数据（作者/授权/仓库/项目主页/标签/README）统一在 `src/Directory.Build.props`；**新增会被发布的包时，务必在自己 csproj 里补一行 `<Description>`**（否则 nuget.org 上只会显示 SDK 占位文本 `Package Description`），并把项目名加进 `src/publish-packages.ps1` 的列表。
- **测试代码必须跨平台**（CI 只跑 Linux），两类路径都要当心：
  - **分隔符**：相对路径一律用 `/` 或 `Path.Combine` 分段，**不要写 `@"Samples\Web\index.xml"`**——反斜杠在 Linux 是合法文件名字符，`Path.Combine` 不会转换，结果是"Windows 上绿、Linux 上 `File.Exists` 失败"。夹具/示例用 `TestPaths.Fixture("Samples", "Web", "index.xml")` 定位。
  - **Windows 专有绝对路径**：`C:\base` 在 Linux 上**不是** rooted 路径，`Path.Combine` 的行为会不同（Windows 丢弃前一段、Linux 拼接），于是同一个用例在两个平台上"测的不是同一件事"。需要"绝对路径"语义的数据请用 `TestPaths.TempPath("base")`（运行时由 `Path.GetTempPath()` 拼出，两个平台都真 rooted）。
  - 两者都收在 `src/StdUnit.Tags.Tests/TestPaths.cs`。新增夹具/路径类用例后请在 WSL 上跑一遍 `-f net8.0`（或直接看 CI）。
- **大小写也算"跨平台"**：Linux 文件系统区分大小写，Windows 不区分。写错大小写的路径（例如测试里的 `Schemas/drivers` 而 csproj 的 `Link` 是 `Schemas\Drivers`）在 Windows 上一直绿，一到 Linux CI 就 `DirectoryNotFoundException`。
  - **警告：这个坑在 WSL 里测不出来**——`/mnt/d`（DrvFs）同样不区分大小写。要真验证，得把仓库复制到 WSL 的 ext4（`tar` 排除 `bin`/`obj`，删掉 `paket-files/paket.restore.cached` 后 `paket restore`），或直接依赖 CI。
  - 需要按名字定位文件/目录时，**大小写必须与产出方（csproj 的 `Link`、`TestPaths.Fixture(...)`）逐字符一致**——不要为了"容错"写成不区分大小写，那恰好把这个坑掩盖掉（我们就是先写成 `SchemaPath("drivers")` 才红的）。写错了就让它在 CI 上红，并顺手核对 csproj。
- 驱动包内的 DI 扩展类约定**同名** `TagsProject_Extensions`，各自位于自己的命名空间（如 `StdUnit.Tags.S7`）。
- 驱动名常量集中在 `XxxNames.DriverName`（如 `S7Names.DriverName = "S7"`、`ComDriverNames.DriverName = "COM"`）；XML 中的 `driver="..."` 必须与之完全一致。
- 每个驱动的支持注册收口为 `.AddXxxSupport()`，实现为细粒度注册（`AddXxxChannel` / `AddXxxTagCbntBuilder` / `AddXxxDirectTagBuilder`）的组合。新增驱动请沿用这个形状。可选的"策略/判定"类扩展点按这个形状走：**注册扩展方法的可选参数 → 注册时闭包构造工厂 → 通道 ctor 的可选参数（默认用内置实现）**，例：OpcUa 的 `checkIsFailed`（`AddOpcUaClientSupport(checkIsFailed:)` → `OpcUaClientTagChannelFactory` → `OpcUaClientTagChannel` ctor，默认 `OpcUaValueQuality.IsFailed`）；不要把它做成需要用户自己组装的服务类型。
- **异常分型**（`src/StdUnit.Tags.Core/Exceptions/`）：加载期（XML 解析 / 地址解析 / 描述符解析 / 构建期配置检查 / 校验器）一律抛 `TagsProjectLoadException` 的具体子类——`TagsProjectXmlException`（属性值非法）、`TagsProjectAddressException`（地址无法解析）、`TagsProjectConfigurationException`（配置语义不自洽，含重名测点）、`TagsProjectValidationException`（校验器聚合，`Errors` 明细）。**不要**再在这些路径上抛裸 `Exception`/`ArgumentException`/`InvalidOperationException`/`NotImplementedException`。所有加载期错误消息都要带定位上下文（`XElement.GetLocationPath()` / `ITagGrp.GetLocationPath()`，段间用 `/` 连接，形如 `TagGrp(a)/TagCbnt(b)/Tag(c)`、`Channel(S7-3)`，与 `ITagGrp.Descendant("a/b/c")` 语法一致），并尽量把 `Location` 属性一起填上；具体选型见 `docs/异常处理.md` 的决策树。运行期（读写/连接/清理）**不建新类型**，但**不许抛裸 `Exception`**：一律用语义贴近的 BCL 类型（入参问题 `ArgumentNullException`/`ArgumentOutOfRangeException`/`ArgumentException`，时序/状态问题 `InvalidOperationException`，类型不符 `InvalidCastException`，按名查不到 `KeyNotFoundException`，能力不支持 `NotSupportedException`）。运行期故障消息必须能定位现场（`RunnerCrashed` 只带入口的**主通道**）：**通道名 + 操作 + 地址/端点 + 底层错误文本与错误码明细**；不要为加前缀而包装第三方异常（`SocketException`/`OpcException`/NModbus 自带端点信息，包装会改变调用方可 catch 的类型）；日志用 `LogError(ex, "...")` 而非 `LogError("...{ex}", ex.Message)`。**设备/协议报的读取失败必须抛出去**（如 OPC UA 的节点 `Bad`、PLC 错误码）——只有抛才能走"崩溃 → 断开 → 重连"恢复路径，"跳过 + 记日志 + 保留上次值"会掐断重连路径并对下游完全不可见；**不要引入质量/可信度概念**，要表达可信度就由业务另加一个测点（详见 `docs/异常处理.md`）。`NotImplementedException` 的两处合法用途（扩展点"默认实现没覆盖你的组合"、`switch` 兜底分支）保留不动，详见 `docs/异常处理.md`。
- 公开 API 变更时，同步检查 `src/StdUnit.Tags.Core/Schemas/tagsproject.xsd`（及驱动 XSD）与 `README.md` 的文件夹/API 说明。

## 文档

- **AI 参考手册**（事实清单 / 铁律 / XML 与 API 速查 / 任务配方）：<https://tags.doc.stdunit.com/assets/999.AI.markdown>
- 教程站点：<https://tags.doc.stdunit.com/>
- 整站单页版：<https://tags.doc.stdunit.com/print.html>