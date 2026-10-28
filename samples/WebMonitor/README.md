# WebMonitor —— WebStream 监控 demo（NativeAOT 可发布）

与 `dotnet new tags.web` 生成的模板同构，只是把 Blazor 监控页换成了 **WebStream** 的只读观测流：

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

打开 <http://localhost:5000/stdunit.tags/ui>（具名流：`/stdunit.tags/line-b/ui`）。

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
