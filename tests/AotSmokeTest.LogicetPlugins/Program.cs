using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StdUnit.Tags;
using StdUnit.Tags.Logicets;
using StdUnit.Tags.ModbusTcp;

// ─────────────────────────────────────────────────────────────────────────────
// NativeAOT 冒烟测试（Logicet 插件）：本程序以 PublishAot=true 发布成原生可执行文件后运行。
// 覆盖三条路径（见 README「AOT 的插件化限制」）：
//   ① 不含 <Logicet> 的项目：静态可达的加载路径（XML 解析 / schema / 测点树 / 校验器 / DI）；
//   ② TryAddLogicet<TLogicet>()：AOT 下注册业务逻辑的推荐方式（构造函数由 DAM 标注保留）；
//   ③ 含 <Logicet> 的项目：AOT 下必须**快速失败**（TagsProjectNotSupportedException），
//      而不是把 PlatformNotSupportedException 吞成一条日志后继续；
//      非 AOT（JIT）下则应"只记日志、不阻断加载"，因此两种情况都要断言。
// ─────────────────────────────────────────────────────────────────────────────

// 判据说明（有意用运行时开关，而不是去猜"二进制是不是原生"）：
// 以 PublishAot=true 构建的项目，SDK 会把 System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported=false
// 写进 runtimeconfig.json，因此**连"IL 构建 + JIT 运行"（dotnet run）也会是 false**，
// 与真正的 AOT 宿主行为一致（动态加载不可用）。这正是本库 DynamicLoadingCompat 的判据。
var dynamicLoadingDeclared = RuntimeFeature.IsDynamicCodeSupported;

var services = new ServiceCollection();
services.AddLogging();
services.AddTagsProjectServices(b => b.AddModbusTcpSupport());
using var sp = services.BuildServiceProvider();

var factory = sp.GetRequiredService<ITagsProjectFactory>();
var projectRoot = AppContext.BaseDirectory;
var failures = new List<string>();

async Task Check(string name, Func<Task> action)
{
    Console.WriteLine($"[+]======开始测试: {name}=======");
    try
    {
        await action();
        Console.WriteLine($"[-]======[PASS]=======");
    }
    catch (Exception ex)
    {
        failures.Add($"{name} → {ex.GetType().Name}: {ex.Message}");
        Console.WriteLine($"[!]======[FAIL]: {ex}=======");
    }
    Console.WriteLine();
    Console.WriteLine();
}

// 加载一个不含插件的项目，并在其上做宿主内注册。
await Check("加载不含 <Logicet> 的项目 + TryAddLogicet<DemoLogicet>", () =>
{
    using var project = factory.Create(projectRoot, BuildProjectXml(withPlugin: false));

    if (project.Channels.Count != 1)
    {
        throw new InvalidOperationException($"通道数量应为 1，实际 {project.Channels.Count}");
    }

    if (!project.TryAddLogicet<DemoLogicet>(sp))
    {
        throw new InvalidOperationException("TryAddLogicet<DemoLogicet> 应成功（AOT 下依赖 DAM 保留的公共构造函数）");
    }

    if (!project.Logicets.OfType<DemoLogicet>().Any())
    {
        throw new InvalidOperationException("项目的 Logicets 里应包含 DemoLogicet");
    }

    return Task.CompletedTask;
});

// 配置了插件 dll（这里给一个不存在的路径，重点不在 dll 本身，而在宿主能力）。
await Check("含 <Logicet> 的项目", () =>
{
    if (dynamicLoadingDeclared)
    {
        Console.WriteLine("[INFO] 当前非 AOT 环境，可以动态加载插件。");
        using var project = factory.Create(projectRoot, BuildProjectXml(withPlugin: true));
        if (project.Logicets.Count != 0)
        {
            throw new InvalidOperationException("非 AOT 下不存在的插件 dll 应只记日志，且不产生任何 Logicet");
        }
        return Task.CompletedTask;
    }

    try
    {
        using var project = factory.Create(projectRoot, BuildProjectXml(withPlugin: true));
        throw new InvalidOperationException(
            "NativeAOT 下配置 <Logicet> 应抛 TagsProjectNotSupportedException，但项目被正常创建了");
    }
    catch (TagsProjectNotSupportedException ex)
    {
        Console.WriteLine($"[by design]——AOT场景下动态加载插件应该在加载期丢异常='{ex.Message}'");

        // 测试异常的上下文诊断信息
        if (ex.Location is null || !ex.Location.StartsWith("Logicet(", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"异常应带位置上下文 Logicet(...)，实际 Location={ex.Location ?? "<null>"}");
        }
    }

    return Task.CompletedTask;
});

if (failures.Count > 0)
{
    Console.Error.WriteLine($"Logicet 插件 AOT 冒烟测试失败（DynamicCodeSupported={dynamicLoadingDeclared}，{failures.Count} 项）：");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine("  - " + failure);
    }
    return 1;
}

Console.WriteLine($"Logicet 插件 AOT 冒烟测试通过（DynamicCodeSupported={dynamicLoadingDeclared}）。");
return 0;

static XElement BuildProjectXml(bool withPlugin)
{
    var root = new XElement("root",
        new XElement("Channel",
            new XAttribute("name", "ModbusTcp-1"),
            new XAttribute("driver", "ModbusTcp"),
            new XElement("IpAddr", "127.0.0.1"),
            new XElement("Port", "502")),
        new XElement("TagGrp",
            new XAttribute("name", "g1"),
            new XAttribute("isEntry", "true"),
            new XAttribute("isEnabled", "true"),
            new XAttribute("channel", "ModbusTcp-1"),
            new XAttribute("scanInterval", "100"),
            new XElement("TagCbnt",
                new XAttribute("name", "输入"),
                new XAttribute("address", "10001"),
                new XAttribute("access", "RO"),
                new XElement("Tag",
                    new XAttribute("name", "按钮"),
                    new XAttribute("address", "10001"),
                    new XAttribute("type", "DI")))));

    if (withPlugin)
    {
        // 路径有意指向一个不存在的 dll：AOT 下应当在**碰 dll 之前**就以宿主能力不足失败。
        root.Add(new XElement("Logicet", Path.Combine("plugins", "Demo.Logicets.dll")));
    }

    return root;
}

/// <summary>
/// 宿主内注册的示例逻辑组件。<br/>
/// 它之所以能在 AOT 下被创建，靠的是 <c>TryAddLogicet&lt;TLogicet&gt;</c> 上
/// <c>[DynamicallyAccessedMembers(PublicConstructors)]</c> 让裁剪/AOT 编译保留了本类型的公共构造函数。
/// </summary>
internal sealed class DemoLogicet : LogicetBase
{
    public DemoLogicet(ITagsProject project)
        : base(project)
    {
    }

    public override int Order => 0;

    public override bool MatchEntry(ITagGrp entry) => entry.TagName() == "g1";

    public override Task ProcessAsync(ITagGrp entry, ITagChannel? thisChannel) => Task.CompletedTask;
}
