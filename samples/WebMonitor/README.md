# WebMonitor —— WebUi 监控 demo（NativeAOT 可发布）

与 `dotnet new tags.web` 生成的模板同构，只是把 Blazor 监控页换成了 **WebUi**（监控页 + 设计器，AOT 友好）：

```
WebMonitor/
  Program.cs                  # 宿主与管线：AddTags() + MapTagsStream()
  appsettings.json
  Properties/launchSettings.json
  Tags/                       # 测点相关的一切
    _Extensions.cs            # DI 注册入口：驱动、控制器、观测流、后台任务
    TagBackgroundService.cs   # 启停两路轮询 + 演示用的值模拟器
    TagsPathDefines.cs        # 入口名 / key / 测点路径常量
    main/index.xml            # 不具名流观测的项目
    line-b/index.xml          # 具名流（key=line-b）观测的项目
```

## 页面

| 页面 | URL | 说明 |
|---|---|---|
| 监控页（不具名流） | `/stdunit.tags/ui` | 结构树 + SSE 值流 |
| 监控页（具名 `line-b`） | `/stdunit.tags/line-b/ui` | 同上，观测 `Tags/line-b` |
| **设计器** | `/stdunit.tags/designer` | 通道 + 测点设计、导入既有项目 XML、导出项目 XML |
| 设计器元数据 | `/stdunit.tags/catalog` | 设计器用的 schema 内省结果（JSON） |

设计器与具体流无关（schema 是全局的），所以只有不具名的一份。页面里的通道字段完全由
`/catalog` 驱动——新增驱动不用改页面。它的驱动列表来自各驱动的 `ITagsProjectSchemaProvider`，
所以示例用 `AddSimpleFilesSupport()`（而不是 `AddSimpleFilesChannel()` +
`AddSimpleFilesDirectTagBuilder()`）：它是后两者的超集，会一并注册 schema provider。

设计器支持把既有 `index.xml` 粘贴/选择文件导入（"尽力解析"）：认得出的按 schema 回填到表单，
认不出的驱动字段、扩展属性、未知元素**原样保留**，导出时再写回去（XML 注释会丢失）。
未注册驱动的通道不会被丢弃，只会在导出页提示"驱动不在目录里"。

## 两路观测流

| 流 | 控制器 | 项目 | 监控页 |
|---|---|---|---|
| 不具名 | `AddTagsProjectServices` 注册的 `ITagsProjectCtrl` | `Tags/main` | `/stdunit.tags/ui` |
| 具名 `line-b` | `AddKeyedTagsProjectCtrl("line-b")` | `Tags/line-b` | `/stdunit.tags/line-b/ui` |

数据都是磁盘文本文件（SimpleFiles，`autoCreateFile` 会自动建），`TagBackgroundService`
每秒通过 `ITagsProject.WriteIntent` 把值投递到轮询线程上改一次，所以页面上能看到值实时跳动。

## 运行

```powershell
dotnet run --project samples/WebMonitor/WebMonitor.csproj
```

打开 <http://localhost:5000/stdunit.tags/ui>（具名流：`/stdunit.tags/line-b/ui`；
设计器：<http://localhost:5000/stdunit.tags/designer>）。

## AOT 发布

```powershell
# Linux 需要 clang + zlib1g-dev
dotnet publish samples/WebMonitor/WebMonitor.csproj -c Release -r linux-x64
dotnet publish samples/WebMonitor/WebMonitor.csproj -c Release -r win-x64
```

产物是原生可执行文件（不依赖 .NET 运行时）。示例刻意避开反射路径：Minimal API 的
`RequestDelegate` 重载、源生成 JSON（`TagsStreamJsonContext`）、嵌入的单文件监控页、
`XElement` 代码里建项目描述（不依赖内容文件的相对路径）。CI 的 `aot-smoke` job 会 publish 本示例。

> 具名流的 key 同时是路由段；本示例还把它当目录名用（`Tags/line-b`），所以**别用 Windows
> 保留设备名**（`aux`/`con`/`nul`/`com1`…）——`Directory.CreateDirectory("...\line-b")` 换成 `aux`
> 会变成设备路径 `\\.\aux` 并抛 `DirectoryNotFoundException`。库本身只用 key 做路由段，不校验这一点。
