using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using StdUnit.Tags.WebUi;
using StdUnit.Tags.WebUi.Internal;
using Xunit;

namespace StdUnit.Tags.Tests.NetCoreOnly.WebUi;

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

    [Fact]
    public void ResetScanned_ChildOfTagCbnt_BubblesToTheContainer()
    {
        using var project = this.CreateProject(new XElement(
            "Project",
            StreamTestContext.Channel("fake"),
            StreamTestContext.EntryGroup(
                "g1",
                new XElement(
                    "TagCbnt",
                    new XAttribute("name", "c"),
                    new XAttribute("address", "0"),
                    StreamTestContext.Tag("x", address: "0")))));

        var cbnt = project.Tags.Descendant("g1/c").AsTagCbnt();
        var child = StreamTestContext.TagAt(project, "g1/c/x");
        cbnt.IsScanned = true;
        child.IsScanned = true;

        // 组合子属于组合：标"未扫描"时必须冒泡到组合（整块缓存会一起重读）。
        TagsStreamCommands.ResetScanned(child);

        Assert.False(cbnt.IsScanned);
    }

    /// <summary>每种内置类型都按 <c>TagKind</c> 精确解析（库本身不做跨类型转换）。</summary>
    [Theory]
    [InlineData("BYTE", "200", typeof(byte))]
    [InlineData("INT16", "-12345", typeof(short))]
    [InlineData("UINT16", "54321", typeof(ushort))]
    [InlineData("INT32", "-100000", typeof(int))]
    [InlineData("UINT32", "100000", typeof(uint))]
    [InlineData("INT64", "-5000000000", typeof(long))]
    [InlineData("UINT64", "5000000000", typeof(ulong))]
    [InlineData("FLOAT", "1.5", typeof(float))]
    [InlineData("DOUBLE", "2.25", typeof(double))]
    [InlineData("STR", "hello", typeof(string))]
    [InlineData("BIT", "true", typeof(bool))]
    [InlineData("DI", "1", typeof(bool))]
    [InlineData("DO", "0", typeof(bool))]
    public async Task WriteValue_ParsesEveryBuiltinKind(string type, string raw, Type expectedType)
    {
        using var project = this.CreateProject(new XElement(
            "Project",
            StreamTestContext.Channel("fake"),
            StreamTestContext.EntryGroup("g1", StreamTestContext.Tag("a", type: type))));

        var ctrl = new FakeProjectCtrl();
        ctrl.RaiseStarted(project);

        using var cts = new CancellationTokenSource();
        var run = project.RunAsync(cts.Token);
        try
        {
            var outcome = await TagsStreamCommands.WriteValueAsync(ctrl, new WriteTagValueRequest("g1/a", raw), Timeout);

            Assert.Equal(200, outcome.StatusCode);
            Assert.Null(outcome.Error);

            var value = StreamTestContext.TagAt(project, "g1/a").Value;
            Assert.NotNull(value);
            Assert.Equal(expectedType, value!.GetType());
        }
        finally
        {
            await StreamTestContext.StopAsync(cts, run);
        }
    }

    [Fact]
    public async Task WriteValue_NullPath_IsBadRequest()
    {
        var ctrl = new FakeProjectCtrl();

        var outcome = await TagsStreamCommands.WriteValueAsync(ctrl, new WriteTagValueRequest(null, "1"), Timeout);

        Assert.Equal(400, outcome.StatusCode);
        Assert.Contains("path 不能为空", outcome.Error);
    }

    [Fact]
    public async Task WriteValue_NullRaw_IsBadRequest()
    {
        using var project = this.CreateProject(Int16Project());
        var ctrl = new FakeProjectCtrl();
        ctrl.RaiseStarted(project);

        var outcome = await TagsStreamCommands.WriteValueAsync(ctrl, new WriteTagValueRequest("g1/a", null), Timeout);

        Assert.Equal(400, outcome.StatusCode);
        Assert.Contains("raw 不能为空", outcome.Error);
    }

    [Fact]
    public async Task WriteValue_NonLeafPath_IsBadRequest()
    {
        using var project = this.CreateProject(Int16Project());
        var ctrl = new FakeProjectCtrl();
        ctrl.RaiseStarted(project);

        // 指向测点组（及其根）都不是叶测点
        Assert.Equal(400, (await TagsStreamCommands.WriteValueAsync(ctrl, new WriteTagValueRequest("g1", "1"), Timeout)).StatusCode);
        Assert.Equal(400, (await TagsStreamCommands.RescanAsync(ctrl, new RescanTagRequest("g1"), Timeout)).StatusCode);
    }

    [Fact]
    public async Task WriteValue_TagOutsideAnyEntry_IsConflict()
    {
        using var project = this.CreateProject(new XElement(
            "Project",
            StreamTestContext.Channel("fake"),
            new XElement(
                "TagGrp",
                new XAttribute("name", "plain"),
                new XAttribute("isEnabled", "true"),
                new XAttribute("channel", "fake"),
                StreamTestContext.Tag("a"))));

        var ctrl = new FakeProjectCtrl();
        ctrl.RaiseStarted(project);

        var outcome = await TagsStreamCommands.WriteValueAsync(ctrl, new WriteTagValueRequest("plain/a", "1"), Timeout);

        Assert.Equal(409, outcome.StatusCode);
        Assert.Contains("不在任何入口内", outcome.Error);
    }

    [Fact]
    public async Task WriteValue_BitTextNotParseable_IsBadRequest()
    {
        using var project = this.CreateProject(new XElement(
            "Project",
            StreamTestContext.Channel("fake"),
            StreamTestContext.EntryGroup("g1", StreamTestContext.Tag("b", type: "BIT"))));
        var ctrl = new FakeProjectCtrl();
        ctrl.RaiseStarted(project);

        var outcome = await TagsStreamCommands.WriteValueAsync(ctrl, new WriteTagValueRequest("g1/b", "maybe"), Timeout);

        Assert.Equal(400, outcome.StatusCode);
        Assert.Contains("布尔", outcome.Error);
    }

    [Fact]
    public async Task WriteValue_CustomKind_IsNotWritableAsText()
    {
        using var project = this.CreateProject(new XElement(
            "Project",
            StreamTestContext.Channel("fake"),
            StreamTestContext.EntryGroup("g1", StreamTestContext.Tag("j", type: "JSON"))));
        var ctrl = new FakeProjectCtrl();
        ctrl.RaiseStarted(project);

        var outcome = await TagsStreamCommands.WriteValueAsync(ctrl, new WriteTagValueRequest("g1/j", "{}"), Timeout);

        Assert.Equal(400, outcome.StatusCode);
        Assert.Contains("不支持用文本写入的类型", outcome.Error);
    }
}
