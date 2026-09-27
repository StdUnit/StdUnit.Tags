
[![codecov](https://codecov.io/github/newbienewbie/Itminus.Tags/branch/dev/graph/badge.svg?token=Q0UW94C5SS)](https://codecov.io/github/newbienewbie/Itminus.Tags)

| 子项目| 说明 | 覆盖率 |
|------|------|------|
| Itminus.Tags.Core | 硬件无关的核心抽象，无外部依赖 | [![codecov](https://codecov.io/github/newbienewbie/Itminus.Tags/graph/badge.svg?component=itminus_tags_core )](https://codecov.io/github/newbienewbie/Itminus.Tags/components?components%5B0%5D=itminus_tags_core ) | 
| Itminus.Tags | 依赖于 Itminus.Tags.Core，补充项目、日志、插件等功能 | [![codecov](https://codecov.io/github/newbienewbie/Itminus.Tags/graph/badge.svg?component=itminus_tags )](https://codecov.io/github/newbienewbie/Itminus.Tags/components?components%5B0%5D=itminus_tags ) | 
| Itminus.Tags.SimpleFiles | 简单文件支持，把测点树映射为文件树 | [![codecov](https://codecov.io/github/newbienewbie/Itminus.Tags/graph/badge.svg?component=itminus_tags_simplefiles )](https://codecov.io/github/newbienewbie/Itminus.Tags/components?components%5B0%5D=itminus_tags_simplefiles ) | 
| Itminus.Tags.S7 | 西门子S7通信支持 | [![codecov](https://codecov.io/github/newbienewbie/Itminus.Tags/graph/badge.svg?component=itminus_tags_s7 )](https://codecov.io/github/newbienewbie/Itminus.Tags/components?components%5B0%5D=itminus_tags_s7 ) | 
| Itminus.Tags.ModbusTcp | ModbusTcp通信支持 | [![codecov](https://codecov.io/github/newbienewbie/Itminus.Tags/graph/badge.svg?component=itminus_tags_modbstcp )](https://codecov.io/github/newbienewbie/Itminus.Tags/components?components%5B0%5D=itminus_tags_modbstcp ) | 
| Itminus.Tags.OpcUaClient  | OpcUa通信支持 | [![codecov](https://codecov.io/github/newbienewbie/Itminus.Tags/graph/badge.svg?component=itminus_tags_opcuaclient )](https://codecov.io/github/newbienewbie/Itminus.Tags/components?components%5B0%5D=itminus_tags_opcuaclient ) | 
| Itminus.Tags.Hjzk  | Hjzk 远程IO 通信支持 | [![codecov](https://codecov.io/github/newbienewbie/Itminus.Tags/graph/badge.svg?component=itminus_tags_hjzk )](https://codecov.io/github/newbienewbie/Itminus.Tags/components?components%5B0%5D=itminus_tags_hjzk ) | 
| Itminus.Tags.ZLan | ZLan 远程IO 通信支持 | [![codecov](https://codecov.io/github/newbienewbie/Itminus.Tags/graph/badge.svg?component=itminus_tags_zLan )](https://codecov.io/github/newbienewbie/Itminus.Tags/components?components%5B0%5D=itminus_tags_zLan ) | 
| Itminus.Tags.ComScanner | 串口通信支持 | [![codecov](https://codecov.io/github/newbienewbie/Itminus.Tags/graph/badge.svg?component=itminus_tags_comscanner )](https://codecov.io/github/newbienewbie/Itminus.Tags/components?components%5B0%5D=itminus_tags_comscanner ) | 
| Itminus.Tags.RxExtensions  | Rx.NET 扩展 | [![codecov](https://codecov.io/github/newbienewbie/Itminus.Tags/graph/badge.svg?component=itminus_tags_rx )](https://codecov.io/github/newbienewbie/Itminus.Tags/components?components%5B0%5D=itminus_tags_rx ) | 
| Itminus.Tags.R3Extensions  | R3 扩展 | [![codecov](https://codecov.io/github/newbienewbie/Itminus.Tags/graph/badge.svg?component=itminus_tags_r3 )](https://codecov.io/github/newbienewbie/Itminus.Tags/components?components%5B0%5D=itminus_tags_r3 ) | 
| Itminus.Tags.McpServer | McpServer 扩展 | [![codecov](https://codecov.io/github/newbienewbie/Itminus.Tags/graph/badge.svg?component=itminus_tags_mcpserver )](https://codecov.io/github/newbienewbie/Itminus.Tags/components?components%5B0%5D=itminus_tags_mcpserver ) | 



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
> 如果你使用`nuget`管理，请参照[示例](https://github.com/newbienewbie/Itminus.Tags.WPFDemo/blob/867a5063bc65ec16f77692d4c56ce9da5a38dc3c/nuget.config#L3-L8)，指定包源为 https://baget.stdunit.com/v3/index.json ；
> 如果你使用`paket`管理，参照本项目[paket.dependencies](https://github.com/newbienewbie/Itminus.Tags/blob/b4ef40f2952fa75d7154db03782c2b5f98be914c/paket.dependencies#L1-L2) 指定包源。
> 我个人建议你使用`paket`管理依赖，这样哪怕我和nuget.org都破产跑路了，你的本地代码也能完全断网的情况下离线编译。

警告：假设版本号是`<major>.<minor>.<patch>`:
- 在`v1.0`版本之前，每个`minor`版本的跳变，可能会引入新特性和破坏性更新。
- 在`v1.0`版本之后，每个`major`版本的跳变，可能会引入新特性和破坏性更新。


## Quick Start

你可以仅使用这个类库中的通信功能；不过我们更推荐你采用它默认的交互方式，**你只管提供描述(`xml`)，我们负责让它跑起来**。

我们提供了模板来快速创建脚手架
```bash
dotnet new install Itminus.Tags.Templates
```

然后即可以创建相关模板项目：
```bash
dotnet new tags.wpf # 这会创建一个 WPF 模板项目
dotnet new tags.web # 这会创建一个 ASP.NET Core 项目
```

优势：
- 硬件无关抽象：理论上，你可以在家里用[S7模拟器](https://github.com/newbienewbie/S7SvrSim)编写自动化测试，验证你的逻辑，最后到现场前再切换到`OpcUa`设备上(或者反过来)。或者你不想用任何模拟器的话，可以直接使用“测点即文件”的功能，用文件系统来测试你的`S7`、`OpcUa`逻辑。
- “测点即文件”: 添加`Itminus.Tags.SimpleFiles`支持，可以把测点树映射为文件树，让你轻松读写和变更配置。配合`R1W`+`IsScaned`，可以尽可能减少文件系统的访问次数。
- 支持逻辑组件插件(dll)
- 支持通过MCP方式暴露给AI：把测点项目描述作为上下文，让AI可以轻松操作你的设备

## 文档

0. 我为本类库编写了教程，部署在[tags.doc](http://tags.doc.stdunit.com)。
1. 供新手熟悉功能[WPFDemo](https://github.com/newbienewbie/Itminus.Tags.WPFDemo): 按分支演示功能。
2. 本仓库自带的[Samples](https://github.com/newbienewbie/Itminus.Tags/tree/dev/samples): 主要用于开发验证+喂狗。

## 文件夹结构

- `.config`
    - `dotnet-tools.json`: 本项目用到的 dotnet tools 配置
- `global.json`: 本项目SDK配置，目前锁定版本 `8.0.102`
- `src/`: 项目代码及测试
	- `Itminus.Tags.Core`: 核心抽象；其 `Schemas/` 目录持有项目描述 XML 的 XSD（`tagsproject.xsd`）
	- `Itminus.Tags.SchemaGenerator`: 源生成器，把各项目的 XSD 编译为 DLL 内常量（AOT/trim 友好）
	- `Itminus.Tags`: 基本功能，但和具体的硬件设备无关，只依赖于`Itminus.Tags.Core`。
	- `Itminus.Tags.RxExtensions`: `dotnet/reactive`扩展，只依赖于`Itminus.Tags.Core`
	- `Itminus.Tags.R3Extensions`: `Cysharp/R3`扩展，只依赖于`Itminus.Tags.Core`
	- `Itminus.Tags.S7`: 西门子S7协议扩展，只依赖于`Itminus.Tags` + **Sharp7**
	- `Itminus.Tags.OpcUaClient`: OpcUa客户端扩展，依赖于`Itminus.Tags` + **OpcUa**
	- `Itminus.Tags.ModbusTcp`: ModbusTcp扩展，依赖于`Itminus.Tags` + **NModbus**
	- `Itminus.Tags.Hjzk`: Hjzk IO盒子扩展，依赖于`Itminus.Tags.ModbusTcp` 
	- ... 其它硬件扩展
	- `Itminus.Tags.BlazorLib.Core`: Blazor 类库，包含核心功能抽象，以及一个极简的监控页面。
	- `Itminus.Tags.BlazorLib`: 包含一些常用硬件设备的实现。
	- `Itminus.Tags.McpServer`: 这是一个把`Itminus.Tags`暴露成 [Model Context Protocol Server](https://modelcontextprotocol.io/) 的类库。
	- `Itminus.Tags.Tests`: 上述所有子项目的测试
- `samples/`: 示例代码
- `paket.dependencies`: 用 [`paket`](https://github.com/fsprojects/Paket)管理的依赖声明
- `paket.lock`: 依赖锁定文件

## LICENSING

本仓库由许多子项目构成，根据上游依赖的不同，我们为每个子项目采用不同的授权协议。基本原则是**在尊重上游依赖包授权的前提下，选择最友好的开源授权协议** (几乎都是 **MIT**，详见各仓库下的 LICENSE)。

1. 我们自己编写的核心类库部分和部分硬件实现包，由于不涉及官方类库之外的第三方依赖，一律采用**MIT协议**。
2. 除了**OPC UA**之外，所有涉及第三方依赖的实现包，其上游依赖都是[MIT](https://github.com/NModbus/NModbus)授权，所以这里我们也放心采用**MIT协议**。
3. 目前唯一比较特殊的是**OPC UA**，我记得**早期**OPC基金会的仓库下基本都是GPL授权，不过最近我发现它们官方已经**改成了[OPC Foundation MIT License 1.00](https://github.com/OPCFoundation/UA-.NETStandard/blob/master/LICENSE.txt)**，所以我们也遵循这个开源协议——**OPC Foundation MIT License 1.00**

## 开发计划

开发计划与待办事项见 [backlog.md](backlog.md)。

