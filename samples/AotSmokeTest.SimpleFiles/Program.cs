using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StdUnit.Tags;
using StdUnit.Tags.SimpleFiles;

// ─────────────────────────────────────────────────────────────────────────────
// NativeAOT 冒烟测试（SimpleFiles）：本程序以 PublishAot=true 发布成原生可执行文件后运行。
// 覆盖三条路径：
//   ① 基本类型测点（走驱动内部的直接测点工厂）读写；
//   ② JSON 测点读写：元数据来自调用方登记一次的源生成 JsonSerializerContext，
//      这是 SimpleFiles 的 IL2026/IL3050 修法在 AOT 下的实证；
//   ③ AOT 下**忘了**登记 JSON 元数据时，必须在构建测点时快速失败
//      （TagsProjectNotSupportedException），而不是等第一次读写才抛 STJ 的
//      "Reflection-based serialization has been disabled"。
// ─────────────────────────────────────────────────────────────────────────────

// 判据说明（有意用运行时开关，而不是去猜"二进制是不是原生"）：
// 以 PublishAot=true 构建的项目，SDK 会把 System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported=false
// 写进 runtimeconfig.json，因此**连"IL 构建 + JIT 运行"（dotnet run）也会是 false**，
// 与真正的 AOT 宿主行为一致。注意 JSON 的可用性判据是 STJ 自己的
// JsonSerializer.IsReflectionEnabledByDefault（这个开关同样被写进 runtimeconfig）。
var dynamicLoadingDeclared = RuntimeFeature.IsDynamicCodeSupported;

var failures = new List<string>();
var tempDir = Path.Combine(Path.GetTempPath(), "AotSmokeTest.SimpleFiles", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempDir);

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

try
{
    // ① 基本类型测点：不需要任何元数据。
    await Check("基本类型测点读写（INT32）", async () =>
    {
        var file = Path.Combine(tempDir, "int.txt");
        await File.WriteAllTextAsync(file, "42");

        using var project = CreateProvider(registerJsonContext: true)
            .CreateScope().ServiceProvider
            .GetRequiredService<ITagsProjectFactory>()
            .Create(AppContext.BaseDirectory, BuildProjectXml(tempDir));

        var tag = project.Tags.SelectGrp("g").SelectTag("int-v")
            ?? throw new InvalidOperationException("未找到 INT32 测点");

        await tag.ReadAsync(CancellationToken.None);
        if (tag.Value is not int read || read != 42)
        {
            throw new InvalidOperationException($"读取结果不正确：{tag.Value}");
        }

        tag.Value = 7;
        await tag.WriteAsync(CancellationToken.None);
        var text = await File.ReadAllTextAsync(file);
        if (text != "7")
        {
            throw new InvalidOperationException($"写入结果不正确：{text}");
        }
    });

    // ② JSON 测点：元数据来自登记一次的 SmokeJsonContext（不是反射）。
    await Check("JSON 测点读写（源生成 JsonTypeInfo）", async () =>
    {
        var file = Path.Combine(tempDir, "point.json");
        await File.WriteAllTextAsync(file, """{"X":7,"Y":9}""");

        using var project = CreateProvider(registerJsonContext: true)
            .CreateScope().ServiceProvider
            .GetRequiredService<ITagsProjectFactory>()
            .Create(AppContext.BaseDirectory, BuildProjectXml(tempDir));

        var tag = project.Tags.SelectGrp("g").SelectTag("json-v")
            ?? throw new InvalidOperationException("未找到 JSON 测点");

        await tag.ReadAsync(CancellationToken.None);
        if (tag.Value is not SmokeJson read || read.X != 7 || read.Y != 9)
        {
            throw new InvalidOperationException($"反序列化结果不正确：{tag.Value}");
        }

        tag.Value = new SmokeJson(3, 4);
        await tag.WriteAsync(CancellationToken.None);
        var text = await File.ReadAllTextAsync(file);
        if (text != """{"X":3,"Y":4}""")
        {
            throw new InvalidOperationException($"序列化结果不正确：{text}");
        }

        Console.WriteLine($"[INFO] JSON 读写往返成功：{file}");
    });

    // ③ 忘了登记元数据的兜底。
    await Check("JSON 测点缺元数据时的行为", () =>
    {
        var factory = CreateProvider(registerJsonContext: false)
            .CreateScope().ServiceProvider
            .GetRequiredService<ITagsProjectFactory>();

        if (dynamicLoadingDeclared)
        {
            // 普通 JIT 宿主：反射可用，应该照常构建出来（"非 AOT 用户零改动"的那条路）
            using var project = factory.Create(AppContext.BaseDirectory, BuildProjectXml(tempDir));
            if (project.Tags.SelectGrp("g").SelectTag("json-v") is null)
            {
                throw new InvalidOperationException("普通 JIT 宿主下未登记元数据也应能构建 JSON 测点（走反射）");
            }
            Console.WriteLine("[INFO] 当前非 AOT 环境，JSON 测点走反射路径。");
            return Task.CompletedTask;
        }

        try
        {
            using var project = factory.Create(AppContext.BaseDirectory, BuildProjectXml(tempDir));
            throw new InvalidOperationException(
                "AOT 下未登记 JSON 元数据应抛 TagsProjectNotSupportedException，但项目被正常创建了");
        }
        catch (TagsProjectNotSupportedException ex)
        {
            Console.WriteLine($"[by design]——AOT 下缺 JSON 元数据应在加载期丢异常='{ex.Message}'（Location={ex.Location}）");
        }

        return Task.CompletedTask;
    });
}
finally
{
    Directory.Delete(tempDir, recursive: true);
}

if (failures.Count > 0)
{
    Console.Error.WriteLine($"SimpleFiles AOT 冒烟测试失败（DynamicCodeSupported={dynamicLoadingDeclared}，{failures.Count} 项）：");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine("  - " + failure);
    }
    return 1;
}

Console.WriteLine($"SimpleFiles AOT 冒烟测试通过（DynamicCodeSupported={dynamicLoadingDeclared}）。");
return 0;

// registerJsonContext=false 复现"AOT 下忘了登记源生成元数据"这一错法（见上面第 ③ 条断言）。
static ServiceProvider CreateProvider(bool registerJsonContext)
{
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddTagsProjectServices(b =>
    {
        b.AddSimpleFilesChannel();
        if (registerJsonContext)
        {
            b.AddJsonSerializerContext(SmokeJsonContext.Default);
        }

        // 顺序有意如此：带 predicate 的 JSON 构建器先注册，默认（基本类型）构建器兜底。
        b.AddSimpleFilesDirectTagBuilder(
            predicate: tagBuilder => tagBuilder.TagDescriptor.TagKind == "JSON",
            configure: jsonBuilder => jsonBuilder.WithJsonTagFactory<SmokeJson>())
         .AddSimpleFilesDirectTagBuilder();
    });
    return services.BuildServiceProvider();
}

static XElement BuildProjectXml(string baseDir)
    => new("root",
        new XElement("Channel",
            new XAttribute("name", "sf-1"),
            new XAttribute("driver", "SimpleFiles"),
            new XElement("BaseDir", baseDir)),
        new XElement("TagGrp",
            new XAttribute("name", "g"),
            new XAttribute("isEntry", "true"),
            new XAttribute("isEnabled", "true"),
            new XAttribute("channel", "sf-1"),
            new XAttribute("scanInterval", "100"),
            new XElement("Tag",
                new XAttribute("name", "int-v"),
                new XAttribute("address", "int.txt"),
                new XAttribute("type", "INT32")),
            new XElement("Tag",
                new XAttribute("name", "json-v"),
                new XAttribute("address", "point.json"),
                new XAttribute("type", "JSON"))));

/// <summary>
/// JSON 测点的值类型（示例 POCO）。
/// </summary>
internal sealed record SmokeJson(int X, int Y);

/// <summary>
/// <see cref="SmokeJson"/> 的源生成元数据。<br/>
/// JSON 测点在 AOT 下必须由调用方登记它（<c>AddJsonSerializerContext(SmokeJsonContext.Default)</c>），
/// 因为反射版 <c>JsonSerializer</c> 在 NativeAOT / 裁剪下不可用。
/// </summary>
[JsonSerializable(typeof(SmokeJson))]
internal sealed partial class SmokeJsonContext : JsonSerializerContext
{
}
