
[![codecov](https://codecov.io/github/newbienewbie/StdUnit.Tags/branch/dev/graph/badge.svg?token=Q0UW94C5SS)](https://codecov.io/github/newbienewbie/StdUnit.Tags)

| 子项目| 说明 | 覆盖率 |
|------|------|------|
| StdUnit.Tags.Core | 硬件无关的核心抽象，无外部依赖 | [![codecov](https://codecov.io/github/newbienewbie/StdUnit.Tags/graph/badge.svg?component=stdunit_tags_core )](https://codecov.io/github/newbienewbie/StdUnit.Tags/components?components%5B0%5D=stdunit_tags_core ) | 
| StdUnit.Tags | 依赖于 StdUnit.Tags.Core，补充项目、日志、插件等功能 | [![codecov](https://codecov.io/github/newbienewbie/StdUnit.Tags/graph/badge.svg?component=stdunit_tags )](https://codecov.io/github/newbienewbie/StdUnit.Tags/components?components%5B0%5D=stdunit_tags ) | 
| StdUnit.Tags.SimpleFiles | 简单文件支持，把测点树映射为文件树 | [![codecov](https://codecov.io/github/newbienewbie/StdUnit.Tags/graph/badge.svg?component=stdunit_tags_simplefiles )](https://codecov.io/github/newbienewbie/StdUnit.Tags/components?components%5B0%5D=stdunit_tags_simplefiles ) | 
| StdUnit.Tags.S7 | 西门子S7通信支持 | [![codecov](https://codecov.io/github/newbienewbie/StdUnit.Tags/graph/badge.svg?component=stdunit_tags_s7 )](https://codecov.io/github/newbienewbie/stdunit.Tags/components?components%5B0%5D=stdunit_tags_s7 ) | 
| StdUnit.Tags.ModbusTcp | ModbusTcp通信支持 | [![codecov](https://codecov.io/github/newbienewbie/StdUnit.Tags/graph/badge.svg?component=stdunit_tags_modbstcp )](https://codecov.io/github/newbienewbie/StdUnit.Tags/components?components%5B0%5D=stdunit_tags_modbstcp ) | 
| StdUnit.Tags.OpcUaClient  | OpcUa通信支持 | [![codecov](https://codecov.io/github/newbienewbie/StdUnit.Tags/graph/badge.svg?component=stdunit_tags_opcuaclient )](https://codecov.io/github/newbienewbie/StdUnit.Tags/components?components%5B0%5D=stdunit_tags_opcuaclient ) | 
| StdUnit.Tags.Hjzk  | Hjzk 远程IO 通信支持 | [![codecov](https://codecov.io/github/newbienewbie/StdUnit.Tags/graph/badge.svg?component=stdunit_tags_hjzk )](https://codecov.io/github/newbienewbie/StdUnit.Tags/components?components%5B0%5D=stdunit_tags_hjzk ) | 
| StdUnit.Tags.ZLan | ZLan 远程IO 通信支持 | [![codecov](https://codecov.io/github/newbienewbie/stdunit.Tags/graph/badge.svg?component=stdunit_tags_zLan )](https://codecov.io/github/newbienewbie/StdUnit.Tags/components?components%5B0%5D=stdunit_tags_zLan ) | 
| StdUnit.Tags.ComScanner | 串口通信支持 | [![codecov](https://codecov.io/github/newbienewbie/stdunit.Tags/graph/badge.svg?component=stdunit_tags_comscanner )](https://codecov.io/github/newbienewbie/StdUnit.Tags/components?components%5B0%5D=stdunit_tags_comscanner ) | 
| StdUnit.Tags.RxExtensions  | Rx.NET 扩展 | [![codecov](https://codecov.io/github/newbienewbie/StdUnit.Tags/graph/badge.svg?component=stdunit_tags_rx )](https://codecov.io/github/newbienewbie/StdUnit.Tags/components?components%5B0%5D=stdunit_tags_rx ) | 
| StdUnit.Tags.R3Extensions  | R3 扩展 | [![codecov](https://codecov.io/github/newbienewbie/StdUnit.Tags/graph/badge.svg?component=stdunit_tags_r3 )](https://codecov.io/github/newbienewbie/StdUnit.Tags/components?components%5B0%5D=stdunit_tags_r3 ) | 
| StdUnit.Tags.McpServer | McpServer 扩展 | [![codecov](https://codecov.io/github/newbienewbie/StdUnit.Tags/graph/badge.svg?component=stdunit_tags_mcpserver )](https://codecov.io/github/newbienewbie/StdUnit.Tags/components?components%5B0%5D=stdunit_tags_mcpserver ) | 



这是一个面向工业交互场景的类库家族：

* 免费开源: 整个类库家族都是MIT授权，而且相关依赖链也都是(或近乎是)MIT授权。
* 高度模块化: 每种硬件实现，以`nuget`包为单元，各自独立。
* 易于扩展：照抄这里内置的设备实现，实现你自己的通讯封装，然后编写一个`.AddYourOwnSupport()`扩展方法挂接上去。比如，在我的树莓派上，我基于它造了一个监控GPIO、和 Linux ProcInfo、MemInfo等系统信息的网页程序。
* 跨平台：依托于`dotnet`跨平台的能力，让你的代码跑到各种设备上。

这不是`framework`，而是一个`library`家族。我们希望它能被灵活地组合到各种场景，而不是仅仅被当做一个项目模板。它的核心是一组统一的、可扩展的通信类库的抽象，以及在此基础之上提供的开箱即用的交互方式。目前，我们只提供一种交互方式：严格的串行轮询(有意地模仿了 PLC 的扫描机制)。

0. 执行外部意图
1. 读入数据 
2. 处理逻辑
3. 刷写底层

> **在正式发布1.0版本之前，这个包只会发布在我的测试源上**。
> 如果你使用`nuget`管理，请参照[示例](https://github.com/newbienewbie/StdUnit.Tags.WPFDemo/blob/867a5063bc65ec16f77692d4c56ce9da5a38dc3c/nuget.config#L3-L8)，指定包源为 https://baget.stdunit.com/v3/index.json ；
> 如果你使用`paket`管理，参照本项目[paket.dependencies](https://github.com/newbienewbie/StdUnit.Tags/blob/b4ef40f2952fa75d7154db03782c2b5f98be914c/paket.dependencies#L1-L2) 指定包源。
> 我个人建议你使用`paket`管理依赖，这样哪怕我和nuget.org都破产跑路了，你的本地代码也能完全断网的情况下离线编译。

警告：假设版本号是`<major>.<minor>.<patch>`:
- 在`v1.0`版本之前，每个`minor`版本的跳变，可能会引入新特性和破坏性更新。
- 在`v1.0`版本之后，每个`major`版本的跳变，可能会引入新特性和破坏性更新。


## Quick Start

你可以仅使用这个类库中的通信功能；不过我们更推荐你采用它默认的交互方式，**你只管提供描述(`xml`)，我们负责让它跑起来**。

我们提供了模板来快速创建脚手架
```bash
dotnet new install StdUnit.Tags.Templates
```

然后即可以创建相关模板项目：
```bash
dotnet new tags.wpf # 这会创建一个 WPF 模板项目
dotnet new tags.web # 这会创建一个 ASP.NET Core 项目
```

优势：
- 硬件无关抽象：理论上，你可以在家里用[S7模拟器](https://github.com/newbienewbie/S7SvrSim)编写自动化测试，验证你的逻辑，最后到现场前再切换到`OpcUa`设备上(或者反过来)。或者你不想用任何模拟器的话，可以直接使用“测点即文件”的功能，用文件系统来测试你的`S7`、`OpcUa`逻辑。
- “测点即文件”: 添加`StdUnit.Tags.SimpleFiles`支持，可以把测点树映射为文件树，让你轻松读写和变更配置。配合`R1W`+`IsScaned`，可以尽可能减少文件系统的访问次数。
- 业务逻辑插件化：`netcore`上支持逻辑组件插件(dll)，各插件的依赖相互隔离、支持卸载和热更(`netfx`上降级支持，见“netfx 的插件化限制”)。
- 支持通过MCP方式暴露给AI：把测点项目描述作为上下文，让AI可以轻松操作你的设备

## 文档

0. 我为本类库编写了教程，部署在[tags.doc](http://tags.doc.stdunit.com)。
1. 供新手熟悉功能[WPFDemo](https://github.com/newbienewbie/StdUnit.Tags.WPFDemo): 按分支演示功能。
2. 本仓库自带的[Samples](https://github.com/newbienewbie/StdUnit.Tags/tree/dev/samples): 主要用于开发验证+喂狗。

## 文件夹结构

- `.config`
    - `dotnet-tools.json`: 本项目用到的 dotnet tools 配置
- `global.json`: 本项目SDK配置，目前锁定版本 `8.0.102`
- `src/`: 项目代码及测试
	- `StdUnit.Tags.Core`: 核心抽象；其 `Schemas/` 目录持有项目描述 XML 的 XSD（`tagsproject.xsd`）
	- `StdUnit.Tags.SchemaGenerator`: 源生成器，把各项目的 XSD 编译为 DLL 内常量（AOT/trim 友好）
	- `StdUnit.Tags`: 基本功能，但和具体的硬件设备无关，只依赖于`StdUnit.Tags.Core`。
	- `StdUnit.Tags.RxExtensions`: `dotnet/reactive`扩展，只依赖于`StdUnit.Tags.Core`
	- `StdUnit.Tags.R3Extensions`: `Cysharp/R3`扩展，只依赖于`StdUnit.Tags.Core`
	- `StdUnit.Tags.S7`: 西门子S7协议扩展，只依赖于`StdUnit.Tags` + **Sharp7**
	- `StdUnit.Tags.OpcUaClient`: OpcUa客户端扩展，依赖于`StdUnit.Tags` + **OpcUa**
	- `StdUnit.Tags.ModbusTcp`: ModbusTcp扩展，依赖于`StdUnit.Tags` + **NModbus**
	- `StdUnit.Tags.Hjzk`: Hjzk IO盒子扩展，依赖于`StdUnit.Tags.ModbusTcp` 
	- ... 其它硬件扩展
	- `StdUnit.Tags.BlazorLib.Core`: Blazor 类库，包含核心功能抽象，以及一个极简的监控页面。
	- `StdUnit.Tags.BlazorLib`: 包含一些常用硬件设备的实现。
	- `StdUnit.Tags.McpServer`: 这是一个把`StdUnit.Tags`暴露成 [Model Context Protocol Server](https://modelcontextprotocol.io/) 的类库。
	- `StdUnit.Tags.Tests`: 上述子项目的测试（多目标 `net8.0` + `net472`）
	- `StdUnit.Tags.Tests.NetCoreOnly`: **仅 `net8.0`** 的测试项目，专门存放无法面向 net472 的测试（Blazor / ASP.NET Core 等）。目前为空项，占位预留。
- `samples/`: 示例代码
- `paket.dependencies`: 用 [`paket`](https://github.com/fsprojects/Paket)管理的依赖声明
- `paket.lock`: 依赖锁定文件

## 目标框架

本项目家族同时支持 `net8.0` 与 `net472`（.NET Framework 4.7.2），以便逐步迁移中的旧系统也能用上同一套抽象：

| 子项目 | 目标框架 |
|---|---|
| `StdUnit.Tags.Core` / `StdUnit.Tags` | `net8.0` + `net472` |
| `StdUnit.Tags.RxExtensions` / `StdUnit.Tags.R3Extensions` | `net8.0` + `net472` |
| `StdUnit.Tags.S7` / `ModbusTcp` / `Hjzk` / `ZLan` / `OpcUaClient` / `ComScanner` / `SimpleFiles` | `net8.0` + `net472` |
| `StdUnit.Tags.McpServer` | `net8.0` + `net472`（经 `ModelContextProtocol` 核心包的 `netstandard2.0` 资产） |
| `StdUnit.Tags.BlazorLib` / `BlazorLib.Core` | 仅 `net8.0` |
| `StdUnit.Tags.SchemaGenerator` | `netstandard2.0`（源生成器） |

两个框架的能力差异（目前只有一处，即插件化的隔离与卸载）：见 [netfx 的插件化限制](#netfx-的插件化限制)。

> 实现约定：跨框架差异一律收在**调用点**（`#if NETFRAMEWORK`）或项目内 `Compat/` 目录的单点垫片里，
> 且**有标准库时优先使用标准库**（例如 `ReferenceEqualityComparer` 只在 net472 下用仓库内的等价实现）。
> 不要把差异扩散到公共 API（如 `#if` 修饰公开成员），否则会给库使用者制造两套签名。

### net472 下的测试

`StdUnit.Tags.Tests` 同时面向 `net472`，因此 CI 在两个框架下都会跑测试。
MCP 测试已在 `McpServer` 支持 net472 后合并回主测试项目（走 `ModelContextProtocol` 核心包的 `netstandard2.0` 资产）。
仅 `net8.0` 的测试（如 Blazor / ASP.NET Core 方向）另放 `StdUnit.Tags.Tests.NetCoreOnly`。

两个只在 net472 出现、且需要知道的坑（已在代码注释中标注）：

- **测试宿主会做影子拷贝**：.NET Framework 的测试宿主默认对程序集做 shadow copy，
  使 `Assembly.Location` 指向 `%TEMP%` 下的临时目录，**而且每个程序集落在不同的子目录**，
  依赖「程序集所在目录」定位 XML 夹具的用例会失败。
  已统一改用 `AppContext.BaseDirectory`（夹具定位走 `TestPaths`，与库自身的默认项目根目录约定一致），
  **无需**关闭 AppDomain 或改任何宿主设置。
- **引用程序集没有可空标注**：net472 的 BCL 没有 `[NotNullWhen(false)]` 之类的标注，
  于是 `if (string.IsNullOrEmpty(x)) return ...; return x;` 在 net472 下会报 CS8603/CS8601/CS8604（net8.0 不报）。
  这是假阳性，改用语言级判空（`x is null || x.Length == 0`）即可，**不要用 `NoWarn` 压掉**。

## LICENSING

本仓库由许多子项目构成，根据上游依赖的不同，我们为每个子项目采用不同的授权协议。基本原则是**在尊重上游依赖包授权的前提下，选择最友好的开源授权协议** (几乎都是 **MIT**，详见各仓库下的 LICENSE)。

1. 我们自己编写的核心类库部分和部分硬件实现包，由于不涉及官方类库之外的第三方依赖，一律采用**MIT协议**。
2. 除了**OPC UA**之外，所有涉及第三方依赖的实现包，其上游依赖都是[MIT](https://github.com/NModbus/NModbus)授权，所以这里我们也放心采用**MIT协议**。
3. 目前唯一比较特殊的是**OPC UA**，我记得**早期**OPC基金会的仓库下基本都是GPL授权，不过最近我发现它们官方已经**改成了[OPC Foundation MIT License 1.00](https://github.com/OPCFoundation/UA-.NETStandard/blob/master/LICENSE.txt)**，所以我们也遵循这个开源协议——**OPC Foundation MIT License 1.00**

## 开发计划

开发计划与待办事项见 [backlog.md](backlog.md)。

### netfx 的插件化限制

`net472` 没有 `AssemblyLoadContext`，无法做到「按目录隔离 + 可卸载」的插件加载，
因此 `<Logicet>` 插件在该框架下退化为 `Assembly.LoadFrom`：

- **仍然可用**：插件可以独立编译、单独发布，再挂载到宿主——「宿主开发与插件开发分离」的能力保留；
- **无依赖隔离**：插件依赖与宿主同名程序集冲突时以先加载者为准，不会为插件目录单独建立加载上下文；
- **无法卸载**：`project` 停止不会释放插件程序集，插件 `dll` 在宿主进程退出前一直被锁定；
  同一路径的插件重新编译后，若不重启宿主，可能仍运行旧代码。

加载时会输出 `WARNING` 日志提示上述限制。若需要完整能力（隔离 + 卸载 + 热更新），请使用 `net8.0`。

> 上述限制不影响在宿主内注册逻辑组件（`TryAddLogicet<TLogicet>`），该方式在两个框架下都可用。
