
[![codecov](https://codecov.io/gh/StdUnit/StdUnit.Tags/graph/badge.svg?token=X872NXTCJI)](https://codecov.io/gh/StdUnit/StdUnit.Tags)

| 子项目| 说明 | 覆盖率 |
|------|------|------|
| StdUnit.Tags.Core | 硬件无关的核心抽象，无外部依赖 | [![codecov](https://codecov.io/gh/StdUnit/StdUnit.Tags/graph/badge.svg?component=stdunit_tags_core )](https://app.codecov.io/gh/StdUnit/StdUnit.Tags/components?components%5B0%5D=stdunit_tags_core ) | 
| StdUnit.Tags | 依赖于 StdUnit.Tags.Core，补充项目、日志、插件等功能 | [![codecov](https://codecov.io/gh/StdUnit/StdUnit.Tags/graph/badge.svg?component=stdunit_tags )](https://app.codecov.io/gh/StdUnit/StdUnit.Tags/components?components%5B0%5D=stdunit_tags ) | 
| StdUnit.Tags.SimpleFiles | 简单文件支持，把测点树映射为文件树 | [![codecov](https://codecov.io/gh/StdUnit/StdUnit.Tags/graph/badge.svg?component=stdunit_tags_simplefiles )](https://app.codecov.io/gh/StdUnit/StdUnit.Tags/components?components%5B0%5D=stdunit_tags_simplefiles ) | 
| StdUnit.Tags.S7 | 西门子S7通信支持 | [![codecov](https://codecov.io/gh/StdUnit/StdUnit.Tags/graph/badge.svg?component=stdunit_tags_s7 )](https://app.codecov.io/gh/StdUnit/StdUnit.Tags/components?components%5B0%5D=stdunit_tags_s7 ) | 
| StdUnit.Tags.ModbusTcp | ModbusTcp通信支持 | [![codecov](https://codecov.io/gh/StdUnit/StdUnit.Tags/graph/badge.svg?component=stdunit_tags_modbstcp )](https://app.codecov.io/gh/StdUnit/StdUnit.Tags/components?components%5B0%5D=stdunit_tags_modbstcp ) | 
| StdUnit.Tags.OpcUaClient  | OpcUa通信支持 | [![codecov](https://codecov.io/gh/StdUnit/StdUnit.Tags/graph/badge.svg?component=stdunit_tags_opcuaclient )](https://app.codecov.io/gh/StdUnit/StdUnit.Tags/components?components%5B0%5D=stdunit_tags_opcuaclient ) | 
| StdUnit.Tags.Hjzk  | Hjzk 远程IO 通信支持 | [![codecov](https://codecov.io/gh/StdUnit/StdUnit.Tags/graph/badge.svg?component=stdunit_tags_hjzk )](https://app.codecov.io/gh/StdUnit/StdUnit.Tags/components?components%5B0%5D=stdunit_tags_hjzk ) | 
| StdUnit.Tags.ZLan | ZLan 远程IO 通信支持 | [![codecov](https://codecov.io/gh/StdUnit/StdUnit.Tags/graph/badge.svg?component=stdunit_tags_zLan )](https://app.codecov.io/gh/StdUnit/StdUnit.Tags/components?components%5B0%5D=stdunit_tags_zLan ) | 
| StdUnit.Tags.ComScanner | 串口通信支持 | [![codecov](https://codecov.io/gh/StdUnit/StdUnit.Tags/graph/badge.svg?component=stdunit_tags_comscanner )](https://app.codecov.io/gh/StdUnit/StdUnit.Tags/components?components%5B0%5D=stdunit_tags_comscanner ) | 
| StdUnit.Tags.RxExtensions  | Rx.NET 扩展 | [![codecov](https://codecov.io/gh/StdUnit/StdUnit.Tags/graph/badge.svg?component=stdunit_tags_rx )](https://app.codecov.io/gh/StdUnit/StdUnit.Tags/components?components%5B0%5D=stdunit_tags_rx ) | 
| StdUnit.Tags.R3Extensions  | R3 扩展 | [![codecov](https://codecov.io/gh/StdUnit/StdUnit.Tags/graph/badge.svg?component=stdunit_tags_r3 )](https://app.codecov.io/gh/StdUnit/StdUnit.Tags/components?components%5B0%5D=stdunit_tags_r3 ) | 
| StdUnit.Tags.McpServer | McpServer 扩展 | [![codecov](https://codecov.io/gh/StdUnit/StdUnit.Tags/graph/badge.svg?component=stdunit_tags_mcpserver )](https://app.codecov.io/gh/StdUnit/StdUnit.Tags/components?components%5B0%5D=stdunit_tags_mcpserver ) | 



这是一套面向**串行轮询读写**场景的 .NET 类库：把一组只能周期性读写的资源表达成一棵**测点树**，再为每个入口提供一套严格串行的交互机制（入口之间可以并行）。

目前的主打用例都是工业现场通信——我们内置了`S7`/`OpcUa`/`ModbusTcp` 等驱动。但有意思的是，这里的抽象本身，其实与硬件无关。它面向的是没有**事件 API**、只能粗暴**轮询**的场景。例如，需要周期性监控服务器的资源消耗情况(Linux 甚至为此提供了 `procfs`/`sysfs`)、或者想要周期性监控某些网站上公开信息的变化。对于这些需求，我们只要为它们单独定制一套驱动，就可以共享同一套抽象机制。

这既不是一个开箱即用的产品，也不是一个`framework`，而是一套`library`家族。它的核心是一组统一的、可扩展的通信抽象，以及在此基础之上提供的开箱即用的交互方式。目前，我们只提供一种交互方式：多个入口并行、每一个入口内部严格的串行轮询(有意地模仿了 PLC 的扫描机制)。

0. 执行外部意图
1. 读入数据 
2. 处理逻辑
3. 刷写底层

从某种意义上说，它是一套测点通信抽象，外加一套以**确定性**为核心目标的 `mini-runtime`。我们把它定位成`library`的初衷之一是，希望它能被灵活地组合到各种场景中，而不是仅仅被当做一个项目模板拷来拷去。**确定性**体现在这些地方：

- 扫描串行化消除了许多潜在的**race-condition**，“外部意图”就是一个典型的例子；
- “测点即文件”这种驱动让你不必连接任何硬件，就能对业务逻辑做等效的模拟测试；
- 通道独占（一个通道实例只能属于一个入口）；
- 加载器快速失败；
- ......

`1.0.0` 及以后的相关包都已经发布到 [nuget.org](https://www.nuget.org/packages/StdUnit.Tags)。主要变化参见 [CHANGELOG.md](./CHANGELOG.md)。

> 当然，如果你需要自动创建模板，我们也提供了[StdUnit.Tags.Templates](https://www.nuget.org/packages/StdUnit.Tags.Templates)，参见下面的 [Quick Start](#quick-start)。


## Quick Start

你可以仅使用这个类库中的通信功能；不过我们更推荐你采用它默认的交互方式，**你只管提供描述(`xml`)，我们负责让它跑起来**。

我们提供了模板来快速创建脚手架
```bash
dotnet new install StdUnit.Tags.Templates
```

然后即可以创建相关模板项目：
```bash
dotnet new tags.wpf -n your-project.name # 这会创建一个 WPF 模板项目
dotnet new tags.web -n your-project.name # 这会创建一个 ASP.NET Core 项目
```

想要创建你新的硬件驱动库？
```bash
dotnet new tags.driver -n your-project.name -D YourDriver 
```
备注：这里`-D`是给你的驱动起的名字

优势：
- 硬件无关抽象：理论上，你可以在家里用[S7模拟器](https://github.com/newbienewbie/S7SvrSim)编写自动化测试，验证你的逻辑，最后到现场前再切换到`OpcUa`设备上(或者反过来)。或者你不想用任何模拟器的话，可以直接使用“测点即文件”的功能，用文件系统来测试你的`S7`、`OpcUa`逻辑。
- “测点即文件”: 添加`StdUnit.Tags.SimpleFiles`支持，可以把测点树映射为文件树，让你轻松读写和变更配置。配合`R1W`+`IsScanned`，可以尽可能减少文件系统的访问次数。
- 业务逻辑插件化：`netcore`上支持逻辑组件插件(dll)，各插件的依赖相互隔离、支持卸载和热更(`netfx`上降级支持，见“netfx 的插件化限制”)。
- 支持通过MCP方式暴露给AI：把测点项目描述作为上下文，让AI可以轻松操作你的设备

## 文档与示例

### 文件夹结构

- `src/`: 源码，主要包括核心包、驱动包和扩展包
- `tests/`: 自动化测试项目（`StdUnit.Tags.Tests` / `StdUnit.Tags.Tests.NetCoreOnly`）与 AOT 冒烟程序（`AotSmokeTest.*`）
- `samples/`：放示例与喂狗程序
- `.config/`
    - `dotnet-tools.json`: 本项目用到的 dotnet tools 配置
- `paket.dependencies`: 用 [`paket`](https://github.com/fsprojects/Paket)管理的依赖声明
- `paket.lock`: 依赖锁定文件
- `global.json`: 本项目SDK配置，目前锁定版本 `8.0.102`
- `codecov.yml`: 代码覆盖率上报配置（按子项目划分组件）

其中 `src/` 下的核心包包括：
- `StdUnit.Tags.Core`: 核心抽象；其 `Schemas/` 目录持有项目描述 XML 的 XSD（`tagsproject.xsd`）
- `StdUnit.Tags`: 基本功能，但和具体的硬件设备无关，只依赖于`StdUnit.Tags.Core`。
- `StdUnit.Tags.SchemaGenerator`: 源生成器，把各项目的 XSD 编译为 DLL 内常量（AOT/trim 友好）

驱动包：
- `StdUnit.Tags.S7`: 西门子S7协议扩展，只依赖于`StdUnit.Tags` + **Sharp7**
- `StdUnit.Tags.OpcUaClient`: OpcUa客户端扩展，依赖于`StdUnit.Tags` + **OpcUa**
- `StdUnit.Tags.ModbusTcp`: ModbusTcp扩展，依赖于`StdUnit.Tags` + **NModbus**
- `StdUnit.Tags.Hjzk`: Hjzk IO盒子扩展，依赖于`StdUnit.Tags.ModbusTcp` 
- ... 其它硬件驱动

扩展包：
- `StdUnit.Tags.RxExtensions`: `dotnet/reactive`扩展，只依赖于`StdUnit.Tags.Core`
- `StdUnit.Tags.R3Extensions`: `Cysharp/R3`扩展，只依赖于`StdUnit.Tags.Core`
- `StdUnit.Tags.BlazorLib.Core`: Blazor 类库，包含核心功能抽象，以及一个极简的监控页面。
- `StdUnit.Tags.McpServer`: 这是一个把`StdUnit.Tags`暴露成 [Model Context Protocol Server](https://modelcontextprotocol.io/) 的类库。

> 备注：源码目录中还有一个 `StdUnit.Tags.BlazorLib`(不是 `StdUnit.Tags.BlazorLib.Core`)，放的是常用硬件设备的编辑器和设计器实现。这些只是我把脑子里的片段随手写下来的实验性代码——因为将来可能做成类库，里面不少东西被标成了`public`——目前它只是实验场，不对外发布。

相关测试统一放在 `tests/` 下的两个测试项目中：
- `StdUnit.Tags.Tests`: 上述子项目的自动化测试（多目标 `net8.0` + `net472`）。
- `StdUnit.Tags.Tests.NetCoreOnly`: **仅 `net8.0`** 的测试项目，专门存放无法面向 net472 的测试（Blazor / ASP.NET Core 等）；目前所有测试都放在 `StdUnit.Tags.Tests` 里，所以这个项目还是空项，占位预留。

这里的自动化测试会在 `main`/`dev` 的推送、PR、发布新版时自动执行，并把结果传到 [codecov.io](https://app.codecov.io/gh/StdUnit/StdUnit.Tags)。

由于开发步调的不同，我们对上面涉及到的驱动和扩展包提供了不同程度的支持：

| 驱动及扩展 | 包名 | 状态 |
|---|---|---|
| 简单文件 | `StdUnit.Tags.SimpleFiles` | ✅ 支持 |
| 西门子S7 | `StdUnit.Tags.S7` | ✅ 支持 |
| OpcUA | `StdUnit.Tags.OpcUaClient` | ✅ 支持 |
| 华杰智控IO盒子 | `StdUnit.Tags.Hjzk` | ✅ 支持 |
| ModbusTcp | `StdUnit.Tags.ModbusTcp` | 🧪 实验 |
| 串口 | `StdUnit.Tags.ComScanner` | 🧪 实验 |
| 卓兰IO盒子 | `StdUnit.Tags.ZLan` | 🧪 实验 |
| Rx扩展 | `StdUnit.Tags.RxExtensions` | ✅ 支持 |
| R3扩展 | `StdUnit.Tags.R3Extensions` | ✅ 支持 |
| McpServer扩展 | `StdUnit.Tags.McpServer` | ✅ 支持 |
| BlazorLibCore扩展 | `StdUnit.Tags.BlazorLib.Core` | ✅ 支持 |

上表列出的包中，标记为“实验”的，`<Major>.<Minor>.<Patch>` 里的 `<Minor>` 跳变可能包含破坏性更新；标记为“支持”的，只有 `<Major>` 跳变才可能包含破坏性更新。核心包（`StdUnit.Tags`、`StdUnit.Tags.Core`）不属于驱动/扩展、因此不在此表内，按语义化版本处理——同样只有 `<Major>` 跳变才可能包含破坏性更新。

> 警告：有一些未在本仓库中列出的驱动及扩展，却由我发布到公开的`nuget.org`上以及私有的测试源上(比如[`StdUnit.Tags.LinuxFs.ProcInfo`](https://www.nuget.org/packages/StdUnit.Tags.LinuxFs.ProcInfo)、[`StdUnit.Tags.WinUsbKeyboardCodeScanners`](https://www.nuget.org/packages/StdUnit.Tags.WinUsbKeyboardCodeScanners/))，只供内部使用——那部分包不对外提供任何质量保证，请谨慎使用。


### 给人类的

0. 我们为本类库编写了专门的文档和教程，部署在[https://tags.doc.stdunit.com](https://tags.doc.stdunit.com)。其中由浅入深地讲解了基本概念、常用写法、设计思路和驱动专题，也穿插了不少常见故障与排错手法。
1. 仓库内 [`docs/`](docs/) 放**框架性**内容：[核心模型](docs/核心模型.md)、[外部意图与死锁](docs/外部意图.md)、[扩展点](docs/扩展点.md)、[错误处理](docs/异常处理.md)、[设计决策与边界](docs/设计决策与边界/)（范围与运行期契约、[CI 取舍](docs/设计决策与边界/1.%20CI.md)、[兼容与命名](docs/设计决策与边界/2.%20兼容与命名.md)、[打包与发布](docs/设计决策与边界/3.%20打包与发布.md)）；详细使用说明见上一点。
2. 本仓库自带的 [Samples](https://github.com/StdUnit/StdUnit.Tags/tree/dev/samples)：主要用于开发验证+喂狗。AOT 冒烟程序（`AotSmokeTest.*`）已挪到 [`tests/`](https://github.com/StdUnit/StdUnit.Tags/tree/dev/tests)。（其中 `samples/WpfDemo` 是 WPF 示例，依赖 Windows SDK，所以有意**不纳入到解决方案内**；有需要时，在本地`windows`上用 `dotnet build samples/WpfDemo/WpfDemo.csproj` 单独构建。）
3. 供新手熟悉功能的 [WPFDemo](https://github.com/StdUnit/StdUnit.Tags.WPFDemo)：按分支演示功能。
4. 对于已经开放给公众使用的包，每一个公开的接口、类、方法、属性都写了`XML`注释，所以通过`*.nupkg`安装后即有完整的智能提示，也就没必要另外再提供在线版的`API`文档了,有需要的可以自己用 [`docfx`](https://github.com/dotnet/docfx) 生成(唯一的例外是 `StdUnit.Tags.BlazorLib`：它是只是实验性的代码片段，只对团队内部开放，暂时不发布到公开源上)。

### 给AI的

AI 可以直接参考 [StdUnit.Tags —— 给 AI 的参考手册](https://tags.doc.stdunit.com/assets/999.AI.markdown)（机器可读的原始 Markdown），也可以先访问 <https://tags.doc.stdunit.com/llms.txt>，那里是文档站给 AI 准备的索引。


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

两个框架的能力差异在 `Logicet` 插件上（共两处）：见 [netfx 的插件化限制](#netfx-的插件化限制) 与 [AOT 的插件化限制](#aot-的插件化限制)。


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

### AOT 的插件化限制

由于 **.NET NativeAOT 的运行时完全不提供「动态加载程序集」的能力**，所以在`XML`中，不支持通过配置`<Logicet/>`元素的方式为 AOT 宿主引入逻辑插件——**在 AOT 下只能使用代码注册的方式添加逻辑组件**：

| 宿主形态 | XML动态插件(`<Logicet/>`)  | 编程注册(`TryAddLogicet<TLogicet>`) |
|---|---|---|
| `net8.0` + CoreCLR（JIT / 仅裁剪 / 单文件） | ✅ 隔离 + 卸载 | ✅ |
| `net8.0` + **NativeAOT** | ❌ 不可用 | ✅ |
| `net472` | ✅ 降级可用（见上一节） | ✅ |

> 配了 `<Logicet/>` 的 AOT 宿主会在**加载期**抛 `TagsProjectNotSupportedException`。
