using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StdUnit.Tags;
using StdUnit.Tags.SimpleFiles;
using StdUnit.Tags.WebUi;

// ─────────────────────────────────────────────────────────────────────────────
// NativeAOT 冒烟测试（WebUi）：本程序以 PublishAot=true 发布成原生可执行文件后运行。
// 覆盖三条"只有真 AOT 才能证明"的路径：
//   ① 结构树的**多态 JSON 序列化**（[JsonDerivedType] + 源生成）能产出 kind 判别字段；
//   ② 值流的 SSE 端点真的能连通：状态 → 全量基线 → 改值 → 增量；
//   ③ 整条链路不依赖反射（若走了反射，publish 阶段就会报 IL2026/IL3050，或运行期抛）。
// ─────────────────────────────────────────────────────────────────────────────

// 以 PublishAot=true 构建时，SDK 会把该开关写进 runtimeconfig.json，因此连"IL 构建 + JIT 运行"也是 false，
// 与真正的 AOT 宿主行为一致。
var dynamicCodeSupported = RuntimeFeature.IsDynamicCodeSupported;

var failures = new List<string>();
var tempDir = Path.Combine(Path.GetTempPath(), "AotSmokeTest.WebUi", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempDir);

WebApplication? app = null;
ITagsProjectCtrl? ctrl = null;

try
{
    await File.WriteAllTextAsync(Path.Combine(tempDir, "int.txt"), "1");

    var builder = WebApplication.CreateSlimBuilder(args);
    builder.Logging.ClearProviders();
    builder.WebHost.UseUrls("http://127.0.0.1:0");
    builder.Services.AddTagsProjectServices(b =>
    {
        b.AddSimpleFilesChannel();
        b.AddSimpleFilesDirectTagBuilder();
    });
    builder.Services.AddTagsStream(o =>
    {
        o.ReconcileInterval = TimeSpan.FromMilliseconds(200);
        o.HeartbeatInterval = TimeSpan.FromSeconds(30);
    });

    // 第二路具名流：验证多流的路由/页面在 AOT 下也成立（这一路的项目不启动，只检查结构与 404）。
    builder.Services.AddKeyedTagsProjectCtrl("dev");
    builder.Services.AddKeyedTagsStream("dev");

    app = builder.Build();
    app.MapTagsStream();
    await app.StartAsync();

    var baseUrl = app.Services.GetRequiredService<IServer>()
        .Features.Get<IServerAddressesFeature>()!.Addresses.First();
    Console.WriteLine($"[INFO] listening on {baseUrl}, DynamicCodeSupported={dynamicCodeSupported}");

    ctrl = app.Services.GetRequiredService<ITagsProjectCtrl>();
    _ = Task.Run(() => ctrl.StartPollAsync(tempDir, BuildProjectXml(tempDir), (_, _, _) => Task.CompletedTask));

    using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

    // ① 结构树：多态序列化。等轮询真正起来（isStarted:true）再取，避免与后台启动竞态。
    string tree = string.Empty;
    await WaitUntilAsync(
        async () =>
        {
            tree = await http.GetStringAsync($"{baseUrl}/stdunit.tags/tree", cts.Token);
            return tree.Contains("\"isStarted\":true", StringComparison.Ordinal);
        },
        "项目启动并取到结构树",
        cts.Token);

    Require(tree.Contains("\"kind\":\"grp\"", StringComparison.Ordinal), "结构树应包含 kind=grp 的多态节点");
    Require(tree.Contains("\"kind\":\"tag\"", StringComparison.Ordinal), "结构树应包含 kind=tag 的多态节点");
    Require(tree.Contains("\"entryKey\":\"g\"", StringComparison.Ordinal), "入口组应带 entryKey");
    Require(tree.Contains("\"tagKind\":\"INT32\"", StringComparison.Ordinal), "叶测点应带 tagKind");
    Require(!tree.Contains("\"tagKind\":null", StringComparison.Ordinal), "多态拆分后不应再出现 tagKind=null 的扁平字段");
    Console.WriteLine("[+] 结构树多态序列化正常");

    // ①b 内置监控页：嵌入资源读取 + 端点路径注入（同样是"不依赖反射"的路径）。
    using (var ui = await http.GetAsync($"{baseUrl}/stdunit.tags/ui", cts.Token))
    {
        Require(ui.IsSuccessStatusCode, $"监控页应返回 2xx，实际 {(int)ui.StatusCode}");
        Require(
            ui.Content.Headers.ContentType?.MediaType == "text/html",
            "监控页 Content-Type 应为 text/html");

        var html = await ui.Content.ReadAsStringAsync(cts.Token);
        Require(html.Contains("new EventSource", StringComparison.Ordinal), "监控页应内嵌 EventSource 脚本");
        Require(html.Contains("<base href=\"/stdunit.tags/\" />", StringComparison.Ordinal), "监控页应按请求路径注入 <base href>");
        Require(html.Contains("new URL('stream', document.baseURI)", StringComparison.Ordinal), "值流端点应注入相对路径并由页面解析");
        Require(html.Contains("new URL('tree', document.baseURI)", StringComparison.Ordinal), "结构树端点应注入相对路径并由页面解析");
        Require(!html.Contains("%%", StringComparison.Ordinal), "监控页不应残留未替换的占位符");
        Console.WriteLine("[+] 监控页可访问且基路径已注入");
    }

    // ①c 多流：具名流的页面基路径落在它自己的目录下；未知 key 返回 404。
    using (var ui = await http.GetAsync($"{baseUrl}/stdunit.tags/dev/ui", cts.Token))
    {
        Require(ui.IsSuccessStatusCode, $"具名流的监控页应返回 2xx，实际 {(int)ui.StatusCode}");
        var html = await ui.Content.ReadAsStringAsync(cts.Token);
        Require(
            html.Contains("<base href=\"/stdunit.tags/dev/\" />", StringComparison.Ordinal),
            "具名流的监控页基路径应带上它自己的 key");
    }

    using (var treeOfDev = await http.GetAsync($"{baseUrl}/stdunit.tags/dev/tree", cts.Token))
    {
        Require(treeOfDev.IsSuccessStatusCode, $"具名流的结构树应返回 2xx，实际 {(int)treeOfDev.StatusCode}");
        var json = await treeOfDev.Content.ReadAsStringAsync(cts.Token);
        Require(json.Contains("\"isStarted\":false", StringComparison.Ordinal), "未启动的具名流应返回空结构树");
    }

    using (var unknown = await http.GetAsync($"{baseUrl}/stdunit.tags/nope/tree", cts.Token))
    {
        Require((int)unknown.StatusCode == 404, $"未知 key 应返回 404，实际 {(int)unknown.StatusCode}");
    }

    Console.WriteLine("[+] 多流路由正常（具名页面基路径 / 具名结构树 / 未知 key 404）");

    // ② SSE：订阅 → 全量基线 → 改值 → 增量。
    using var response = await http.GetAsync(
        $"{baseUrl}/stdunit.tags/stream",
        HttpCompletionOption.ResponseHeadersRead,
        cts.Token);
    Require(response.IsSuccessStatusCode, $"值流应返回 2xx，实际 {(int)response.StatusCode}");
    Require(
        response.Content.Headers.ContentType?.MediaType == "text/event-stream",
        "值流 Content-Type 应为 text/event-stream");

    await using var body = await response.Content.ReadAsStreamAsync(cts.Token);
    using var reader = new StreamReader(body, Encoding.UTF8);

    var baseline = await ReadUntilAsync(reader, "\"full\":true", cts.Token);
    Require(baseline.Contains("event: values", StringComparison.Ordinal), "基线帧应是 values 事件");
    Require(baseline.Contains("\"raw\":\"1\"", StringComparison.Ordinal), "基线帧应带上当前值 1");
    Console.WriteLine("[+] 收到全量基线");

    await File.WriteAllTextAsync(Path.Combine(tempDir, "int.txt"), "2", cts.Token);
    var delta = await ReadUntilAsync(reader, "\"raw\":\"2\"", cts.Token);
    Require(delta.Contains("\"full\":false", StringComparison.Ordinal), "改值后应是增量帧（full=false）");
    Console.WriteLine("[+] 收到增量帧");

    // ②b 写命令：改值（请求体反序列化走源生成；变更在轮询线程上执行，随后由该轮刷到底层）
    using (var write = await http.PostAsync(
        $"{baseUrl}/stdunit.tags/value",
        new StringContent("""{"path":"g/int-v","raw":"7"}""", Encoding.UTF8, "application/json"),
        cts.Token))
    {
        var writeBody = await write.Content.ReadAsStringAsync(cts.Token);
        Require(write.IsSuccessStatusCode, $"改值应返回 2xx，实际 {(int)write.StatusCode}：{writeBody}");
        Require(writeBody.Contains("\"ok\":true", StringComparison.Ordinal), $"改值应返回 ok=true，实际 {writeBody}");
    }

    // 不去直接读文件：SimpleFiles 驱动正在写它，Windows 上会撞共享冲突。
    // 从值流里等新值出现即可证明"命令 → 轮询线程 → 刷写 → 读回"这条链走通了。
    var written = await ReadUntilAsync(reader, "\"raw\":\"7\"", cts.Token);
    Require(written.Contains("event: values", StringComparison.Ordinal), "改值后应收到 values 帧");
    Console.WriteLine("[+] 改值已生效");

    // ②c 标记未扫描
    using (var rescan = await http.PostAsync(
        $"{baseUrl}/stdunit.tags/rescan",
        new StringContent("""{"path":"g/int-v"}""", Encoding.UTF8, "application/json"),
        cts.Token))
    {
        var rescanBody = await rescan.Content.ReadAsStringAsync(cts.Token);
        Require(rescan.IsSuccessStatusCode, $"标记未扫描应返回 2xx，实际 {(int)rescan.StatusCode}：{rescanBody}");
    }

    Console.WriteLine("[+] 标记未扫描已执行");

    await ctrl.StopAsync();
    ctrl = null;

    Console.WriteLine($"[INFO] 全部通过（DynamicCodeSupported={dynamicCodeSupported}）");
}
catch (Exception ex)
{
    failures.Add($"{ex.GetType().Name}: {ex.Message}");
    Console.Error.WriteLine($"[!] 失败：{ex}");
}
finally
{
    if (ctrl is not null)
    {
        try
        {
            await ctrl.StopAsync();
        }
        catch
        {
            // 清理失败不影响结论
        }
    }

    if (app is not null)
    {
        try
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
        catch
        {
            // 清理失败不影响结论
        }
    }

    try
    {
        Directory.Delete(tempDir, recursive: true);
    }
    catch
    {
        // 临时目录残留不影响结论
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine($"WebUi AOT 冒烟测试失败（DynamicCodeSupported={dynamicCodeSupported}，{failures.Count} 项）：");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine("  - " + failure);
    }

    return 1;
}

Console.WriteLine($"WebUi AOT 冒烟测试通过（DynamicCodeSupported={dynamicCodeSupported}）。");
return 0;

void Require(bool condition, string message)
{
    if (!condition)
    {
        failures.Add(message);
        throw new InvalidOperationException(message);
    }
}

static async Task WaitUntilAsync(Func<Task<bool>> probe, string what, CancellationToken ct)
{
    var deadline = DateTime.UtcNow.AddSeconds(30);
    while (DateTime.UtcNow < deadline)
    {
        ct.ThrowIfCancellationRequested();
        if (await probe())
        {
            return;
        }

        await Task.Delay(50, ct);
    }

    throw new TimeoutException($"等待超时：{what}");
}

/// <summary>一直读 SSE 的文本行，直到累积内容里出现 <paramref name="needle"/>。</summary>
static async Task<string> ReadUntilAsync(StreamReader reader, string needle, CancellationToken ct)
{
    var buffer = new StringBuilder();
    while (!ct.IsCancellationRequested)
    {
        var line = await reader.ReadLineAsync(ct);
        if (line is null)
        {
            throw new IOException($"连接在收到 '{needle}' 之前就结束了。已收到：{buffer}");
        }

        buffer.AppendLine(line);
        if (buffer.ToString().Contains(needle, StringComparison.Ordinal))
        {
            return buffer.ToString();
        }
    }

    throw new TimeoutException($"等待超时：{needle}");
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
                new XAttribute("type", "INT32"))));
