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

- SDK 固定为 **8.0.102**（`global.json`，`rollForward: minor`）。
- 依赖用 **Paket** 管理。**不要 `dotnet add package`**：请改 `paket.dependencies` 后运行 `dotnet paket install`（会更新 `paket.lock`）。
- 包源有两个：nuget.org 与私有测试源 `https://baget.stdunit.com/v3/index.json`。离线编译依赖 `nuget-package-caches/`。
- `paket.dependencies` 普遍使用 `lowest_matching: true` ⇒ 默认取**最低匹配版本**；升级必须显式改版本号。
- 改了 `paket.dependencies`/`paket.lock` 后若 `obj/<proj>.<tfm>.paket.resolved` 没刷新，删掉 `paket-files/paket.restore.cached` 与 `obj/*.paket.resolved`、`*.paket.references.cached` 再 restore。

## 目标框架（多目标）

**Core / StdUnit.Tags / Rx / R3 / 全部驱动包（S7、ModbusTcp、Hjzk、ZLan、OpcUaClient、ComScanner、SimpleFiles）/ McpServer 都是 `net8.0;net472`；仅 BlazorLib(.Core) 仍是 `net8.0`。**

> McpServer 走 `ModelContextProtocol`（核心包）的 `netstandard2.0` 资产。
> `ModelContextProtocol.AspNetCore` 是 net8-only，与核心包同在 `Mcp` 组，但只被 net8-only 的项目引用，因此不影响 net472。

新增代码时的约定：

- **有标准库就用标准库。** 跨框架差异用调用点的 `#if NETFRAMEWORK` 或项目内 `Compat/` 目录的单点垫片（如 `StdUnit.Tags.Core/Compat/ReferenceEqualityComparer.cs`、各驱动的 `Compat/*.cs`）。
- **不要把 `#if` 铺到调用点满屏幕，也不要用 `#if` 去条件修饰公共 API**（那会给库使用者制造两套签名）。
- 每新增一个项目级 TFM，记得同步 `<LangVersion>latest</LangVersion>`：net472 默认 C# 7.3，`Nullable=enable` 会直接报 CS8630。
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

- 所有项目 `ImplicitUsings` + `Nullable` 开启，`LangVersion latest`，生成 XML 文档文件。
- 驱动包内的 DI 扩展类约定**同名** `TagsProject_Extensions`，各自位于自己的命名空间（如 `StdUnit.Tags.S7`）。
- 驱动名常量集中在 `XxxNames.DriverName`（如 `S7Names.DriverName = "S7"`、`ComDriverNames.DriverName = "COM"`）；XML 中的 `driver="..."` 必须与之完全一致。
- 每个驱动的支持注册收口为 `.AddXxxSupport()`，实现为细粒度注册（`AddXxxChannel` / `AddXxxTagCbntBuilder` / `AddXxxDirectTagBuilder`）的组合。新增驱动请沿用这个形状。
- 公开 API 变更时，同步检查 `src/StdUnit.Tags.Core/Schemas/tagsproject.xsd`（及驱动 XSD）与 `README.md` 的文件夹/API 说明。

## 文档

- **AI 参考手册**（事实清单 / 铁律 / XML 与 API 速查 / 任务配方）：<https://tags.doc.stdunit.com/assets/999.AI.markdown>
- 教程站点：<https://tags.doc.stdunit.com/>
- 整站单页版：<https://tags.doc.stdunit.com/print.html>