using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using StdUnit.Tags.WebStream;
using StdUnit.Tags.WebStream.Internal;
using Xunit;

namespace StdUnit.Tags.Tests.NetCoreOnly.WebStream;

/// <summary>
/// 写命令：改测点值、标记未扫描。<br/>
/// 真实 <c>TagsProject</c> + 真实 <c>TagGrpRunner</c>（假驱动），意图由轮询线程执行。
/// </summary>
public class TagsStreamCommandsTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(200);

    private readonly ServiceProvider _root;
    private readonly IServiceScope _scope;

    public TagsStreamCommandsTests()
    {
        this._root = StreamTestContext.CreateServiceProvider();
        this._scope = this._root.CreateScope();
    }

    public void Dispose()
    {
        this._scope.Dispose();
        this._root.Dispose();
    }

    private ITagsProject CreateProject(XElement xml) =>
        this._scope.ServiceProvider.GetRequiredService<ITagsProjectFactory>().Create(StreamTestContext.ProjectRoot, xml);

    private static XElement Int16Project(string access = "RW") => new(
        "Project",
        StreamTestContext.Channel("fake"),
        StreamTestContext.EntryGroup("g1", StreamTestContext.Tag("a", type: "INT16", access: access)));

    [Fact]
    public async Task WriteValue_ParsesByTagKind_AndApplies()
    {
        using var project = this.CreateProject(Int16Project());
        var ctrl = new FakeProjectCtrl();
        ctrl.RaiseStarted(project);

        using var cts = new CancellationTokenSource();
        var run = project.RunAsync(cts.Token);
        try
        {
            var outcome = await TagsStreamCommands.WriteValueAsync(
                ctrl,
                new WriteTagValueRequest("g1/a", "42"),
                Timeout);

            Assert.Equal(200, outcome.StatusCode);
            Assert.Null(outcome.Error);
            Assert.Equal((short)42, StreamTestContext.TagAt(project, "g1/a").Value);
        }
        finally
        {
            await StreamTestContext.StopAsync(cts, run);
        }
    }

    [Fact]
    public async Task WriteValue_BitKind_AcceptsBooleanText()
    {
        using var project = this.CreateProject(new XElement(
            "Project",
            StreamTestContext.Channel("fake"),
            StreamTestContext.EntryGroup("g1", StreamTestContext.Tag("b", type: "BIT"))));
        var ctrl = new FakeProjectCtrl();
        ctrl.RaiseStarted(project);

        using var cts = new CancellationTokenSource();
        var run = project.RunAsync(cts.Token);
        try
        {
            var tag = StreamTestContext.TagAt(project, "g1/b");

            Assert.Equal(200, (await TagsStreamCommands.WriteValueAsync(ctrl, new WriteTagValueRequest("g1/b", "1"), Timeout)).StatusCode);
            Assert.Equal(true, tag.Value);

            Assert.Equal(200, (await TagsStreamCommands.WriteValueAsync(ctrl, new WriteTagValueRequest("g1/b", "false"), Timeout)).StatusCode);
            Assert.Equal(false, tag.Value);
        }
        finally
        {
            await StreamTestContext.StopAsync(cts, run);
        }
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("99999999999")]
    public async Task WriteValue_UnparsableValue_IsBadRequest(string raw)
    {
        using var project = this.CreateProject(Int16Project());
        var ctrl = new FakeProjectCtrl();
        ctrl.RaiseStarted(project);

        using var cts = new CancellationTokenSource();
        var run = project.RunAsync(cts.Token);
        try
        {
            var outcome = await TagsStreamCommands.WriteValueAsync(ctrl, new WriteTagValueRequest("g1/a", raw), Timeout);

            Assert.Equal(400, outcome.StatusCode);
            Assert.NotNull(outcome.Error);
        }
        finally
        {
            await StreamTestContext.StopAsync(cts, run);
        }
    }

    [Fact]
    public async Task WriteValue_UnknownPath_IsBadRequest()
    {
        using var project = this.CreateProject(Int16Project());
        var ctrl = new FakeProjectCtrl();
        ctrl.RaiseStarted(project);

        using var cts = new CancellationTokenSource();
        var run = project.RunAsync(cts.Token);
        try
        {
            var outcome = await TagsStreamCommands.WriteValueAsync(ctrl, new WriteTagValueRequest("g1/nope", "1"), Timeout);
            Assert.Equal(400, outcome.StatusCode);
        }
        finally
        {
            await StreamTestContext.StopAsync(cts, run);
        }
    }

    [Fact]
    public async Task WriteValue_ReadOnlyTag_IsConflict()
    {
        using var project = this.CreateProject(Int16Project(access: "RO"));
        var ctrl = new FakeProjectCtrl();
        ctrl.RaiseStarted(project);

        using var cts = new CancellationTokenSource();
        var run = project.RunAsync(cts.Token);
        try
        {
            var outcome = await TagsStreamCommands.WriteValueAsync(ctrl, new WriteTagValueRequest("g1/a", "1"), Timeout);
            Assert.Equal(409, outcome.StatusCode);
        }
        finally
        {
            await StreamTestContext.StopAsync(cts, run);
        }
    }

    [Fact]
    public async Task WriteValue_ProjectNotRunning_IsConflict()
    {
        using var project = this.CreateProject(Int16Project());
        var ctrl = new FakeProjectCtrl();

        var outcome = await TagsStreamCommands.WriteValueAsync(ctrl, new WriteTagValueRequest("g1/a", "1"), Timeout);

        Assert.Equal(409, outcome.StatusCode);
        Assert.Contains("未运行", outcome.Error);
    }

    [Fact]
    public async Task WriteValue_DisabledEntry_TimesOut()
    {
        // 入口被禁用时 TagGrpRunner 直接跳过，意图永远不会被 drain
        using var project = this.CreateProject(new XElement(
            "Project",
            StreamTestContext.Channel("fake"),
            new XElement("TagGrp",
                new XAttribute("name", "g1"),
                new XAttribute("isEntry", "true"),
                new XAttribute("isEnabled", "false"),
                new XAttribute("channel", "fake"),
                new XAttribute("scanInterval", 10),
                StreamTestContext.Tag("a"))));

        var ctrl = new FakeProjectCtrl();
        ctrl.RaiseStarted(project);

        using var cts = new CancellationTokenSource();
        var run = project.RunAsync(cts.Token);
        try
        {
            var outcome = await TagsStreamCommands.WriteValueAsync(ctrl, new WriteTagValueRequest("g1/a", "1"), ShortTimeout);

            Assert.Equal(504, outcome.StatusCode);
            Assert.Contains("禁用", outcome.Error);
        }
        finally
        {
            await StreamTestContext.StopAsync(cts, run);
        }
    }

    [Fact]
    public async Task Rescan_ExecutesIntent()
    {
        using var project = this.CreateProject(Int16Project());
        var ctrl = new FakeProjectCtrl();
        ctrl.RaiseStarted(project);

        using var cts = new CancellationTokenSource();
        var run = project.RunAsync(cts.Token);
        try
        {
            var outcome = await TagsStreamCommands.RescanAsync(ctrl, new RescanTagRequest("g1/a"), Timeout);

            // 意图确实被该入口的轮询线程执行了（没超时、没抛错）。
            // "IsScanned 最终为 false" 无法在此断言：同一轮的 ReadAsync 会立刻把它置回 true。
            Assert.Equal(200, outcome.StatusCode);
            Assert.Null(outcome.Error);
        }
        finally
        {
            await StreamTestContext.StopAsync(cts, run);
        }
    }

    [Fact]
    public void ResetScanned_ClearsTagFlag()
    {
        using var project = this.CreateProject(Int16Project());
        var tag = StreamTestContext.TagAt(project, "g1/a");
        tag.IsScanned = true;

        TagsStreamCommands.ResetScanned(tag);

        Assert.False(tag.IsScanned);
    }
}
