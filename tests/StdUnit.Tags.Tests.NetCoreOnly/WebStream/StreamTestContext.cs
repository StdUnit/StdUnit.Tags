using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using StdUnit.Tags.Tests.Fakes;
using Xunit;

namespace StdUnit.Tags.Tests.NetCoreOnly.WebStream;

/// <summary>
/// 观测流测试的公共夹具：用纯内存假驱动构建真实项目（真实 <c>TagsProject</c> + 真实 <c>TagGrpRunner</c>）。
/// </summary>
internal static class StreamTestContext
{
    public static readonly string ProjectRoot = CreateRoot();

    public static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddFakedSupport();
            b.AddFakedTagSupport();
        });
        return services.BuildServiceProvider();
    }

    public static XElement Channel(string name) =>
        new("Channel", new XAttribute("name", name), new XAttribute("driver", "fake"));

    public static XElement Group(string name, params object[] children) =>
        BuildGroup(name, isEntry: false, isEnabled: true, channel: null, children);

    public static XElement Group(string name, bool isEnabled, params object[] children) =>
        BuildGroup(name, isEntry: false, isEnabled, channel: null, children);

    public static XElement EntryGroup(string name, params object[] children) =>
        BuildGroup(name, isEntry: true, isEnabled: true, channel: "fake", children);

    public static XElement EntryGroup(string name, string channel, params object[] children) =>
        BuildGroup(name, isEntry: true, isEnabled: true, channel, children);

    private static XElement BuildGroup(string name, bool isEntry, bool isEnabled, string? channel, object[] children) =>
        new("TagGrp",
            new XAttribute("name", name),
            isEntry ? new XAttribute("isEntry", "true") : null,
            new XAttribute("isEnabled", isEnabled ? "true" : "false"),
            isEntry ? new XAttribute("channel", channel!) : null,
            isEntry ? new XAttribute("scanInterval", 10) : null,
            children);

    public static XElement Tag(string name, string type = "INT16", string access = "RW", string address = "0") =>
        new("Tag",
            new XAttribute("name", name),
            new XAttribute("type", type),
            new XAttribute("access", access),
            new XAttribute("address", address));

    private static string CreateRoot()
    {
        // 相对路径全部用 Path.Combine 分段，跨平台安全（CI 只跑 Linux）。
        var root = Path.Combine(Path.GetTempPath(), "StdUnit.Tags.WebStream.Tests");
        Directory.CreateDirectory(root);
        return root;
    }

    public static bool WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }
            Thread.Sleep(10);
        }

        return condition();
    }

    public static void StopQuietly(CancellationTokenSource cts, Task run)
    {
        cts.Cancel();
        try
        {
            run.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // 取消时轮询循环以 OCE 退出，这是既有设计。
        }
    }

    /// <summary>取消并等待轮询循环退出（唯一的例外是 OCE，属正常停机）。</summary>
    public static async Task StopAsync(CancellationTokenSource cts, Task run)
    {
        cts.Cancel();
        try
        {
            await run.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 取消时轮询循环以 OCE 退出，这是既有设计。
        }
    }

    public static ITag TagAt(ITagsProject project, string path) =>
        project.Tags.Descendant(path).AsTag();

    public static string Text(ReadOnlyMemory<byte> frame) => System.Text.Encoding.UTF8.GetString(frame.ToArray());
}

/// <summary>可控的假控制器：只用于决定"何时启动/停止"。</summary>
internal sealed class FakeProjectCtrl : ITagsProjectCtrl
{
    public ITagsProject? Project { get; private set; }

    public Func<Exception, Task<bool>>? OnStartingException { get; set; }

    public event TagsProjectStartedOrStopped? StartedOrStopped;

    public void RaiseStarted(ITagsProject project)
    {
        this.Project = project;
        this.StartedOrStopped?.Invoke(this, new TagsProjectEventArgs(true, project));
    }

    public void RaiseStopped()
    {
        this.Project = null;
        this.StartedOrStopped?.Invoke(this, new TagsProjectEventArgs(false, null));
    }

    public Task StartPollAsync(string? dir, System.Xml.Linq.XElement? root, Func<ITagsProject, IServiceProvider, CancellationToken, Task> hook) =>
        throw new NotSupportedException();

    public Task StopAsync() => Task.CompletedTask;
}
