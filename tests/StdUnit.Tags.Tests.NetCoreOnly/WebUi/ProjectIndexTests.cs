using System;
using System.Text.Json;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using StdUnit.Tags.WebUi;
using StdUnit.Tags.WebUi.Internal;
using Xunit;

namespace StdUnit.Tags.Tests.NetCoreOnly.WebUi;

/// <summary>
/// <see cref="ProjectIndex"/>：结构树（扁平 + Parent）、入口索引、路径规则。
/// </summary>
public class ProjectIndexTests : IDisposable
{
    private readonly ServiceProvider _root;
    private readonly IServiceScope _scope;

    public ProjectIndexTests()
    {
        this._root = StreamTestContext.CreateServiceProvider();
        this._scope = this._root.CreateScope();
    }

    public void Dispose()
    {
        this._scope.Dispose();
        this._root.Dispose();
    }

    private ITagsProject CreateProject() =>
        this._scope.ServiceProvider.GetRequiredService<ITagsProjectFactory>().Create(
            StreamTestContext.ProjectRoot,
            new XElement("Project",
                StreamTestContext.Channel("fake"),
                StreamTestContext.EntryGroup("g1",
                    StreamTestContext.Tag("a"),
                    StreamTestContext.Group("sub", false,
                        StreamTestContext.Tag("b", type: "BIT", access: "RO", address: "2"))),
                // 非入口子树：出现在树里，但没有值（不会被任何 runner 轮询）
                StreamTestContext.Group("plain")));

    [Fact]
    public void Build_ProducesFlatTreeWithExpectedPathsAndMetadata()
    {
        using var project = this.CreateProject();

        var index = ProjectIndex.Build(epoch: 7, project);

        Assert.Equal(7, index.Epoch);
        Assert.True(index.Tree.IsStarted);

        var nodes = index.Tree.Nodes;
        Assert.Equal(project.Tags.TagName(), index.Tree.RootName);

        // 根：路径为空串、无父节点、深度 0。
        var root = Assert.IsType<TagGrpNodeDto>(nodes[0]);
        Assert.Equal(string.Empty, root.Path);
        Assert.Null(root.Parent);
        Assert.Equal(0, root.Depth);

        var g1 = Assert.IsType<TagGrpNodeDto>(Assert.Single(nodes, n => n.Path == "g1"));
        Assert.Equal(string.Empty, g1.Parent);
        Assert.Equal(1, g1.Depth);
        Assert.True(g1.IsEntry);
        Assert.Equal("g1", g1.EntryKey);
        Assert.Equal(10, g1.ScanIntervalMs);

        var sub = Assert.IsType<TagGrpNodeDto>(Assert.Single(nodes, n => n.Path == "g1/sub"));
        Assert.Equal("g1", sub.Parent);
        Assert.Equal(2, sub.Depth);
        Assert.False(sub.IsEntry);
        Assert.Null(sub.EntryKey);
        Assert.False(sub.IsEnabled);

        var b = Assert.IsType<TagLeafNodeDto>(Assert.Single(nodes, n => n.Path == "g1/sub/b"));
        Assert.Equal("g1/sub", b.Parent);
        Assert.Equal(3, b.Depth);
        Assert.Equal("BIT", b.TagKind);
        Assert.Equal("2", b.Address);
        Assert.True(b.IsReadOnly);

        // 非入口子树照样出现在树里（只是没有值）。
        Assert.Contains(nodes, n => n.Path == "plain");
    }

    [Fact]
    public void Tree_SerializesPolymorphicNodeShapes()
    {
        using var project = this.CreateProject();
        var index = ProjectIndex.Build(1, project);

        var json = JsonSerializer.Serialize(index.Tree, TagsStreamJsonContext.Default.TreeDto);
        using var document = JsonDocument.Parse(json);
        var nodes = document.RootElement.GetProperty("nodes").EnumerateArray().ToArray();

        // 根节点的 parent 必须显式写成 null（其余可空字段省略），前端靠它区分"根"与"字段缺失"
        var root = Assert.Single(nodes, n => n.GetProperty("path").GetString() == string.Empty);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("parent").ValueKind);

        // grp：带入口相关字段，没有 tag 专有字段
        var grp = Assert.Single(nodes, n => n.GetProperty("path").GetString() == "g1");
        Assert.Equal("grp", grp.GetProperty("kind").GetString());
        Assert.True(grp.TryGetProperty("entryKey", out _));
        Assert.True(grp.TryGetProperty("scanIntervalMs", out _));
        Assert.False(grp.TryGetProperty("tagKind", out _));

        // tag：带测点专有字段，没有 grp 专有字段
        var tag = Assert.Single(nodes, n => n.GetProperty("path").GetString() == "g1/sub/b");
        Assert.Equal("tag", tag.GetProperty("kind").GetString());
        Assert.Equal("BIT", tag.GetProperty("tagKind").GetString());
        Assert.False(tag.TryGetProperty("entryKey", out _));
        Assert.False(tag.TryGetProperty("scanIntervalMs", out _));
    }

    [Fact]
    public void Build_OnlyOutermostEntriesBecomeEntryStreams()
    {
        using var project = this.CreateProject();

        var index = ProjectIndex.Build(epoch: 1, project);

        var entry = Assert.Single(index.Entries);
        Assert.Equal("g1", entry.Key);
        Assert.Equal("g1", entry.Path);
        Assert.NotNull(index.ByKey["g1"]);

        Assert.Equal(
            new[] { "g1/a", "g1/sub/b" },
            entry.Collector.GetAll().Select(v => v.Path).OrderBy(p => p, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void Build_DoesNotLeakEntryContextBetweenSiblings()
    {
        using var project = this._scope.ServiceProvider.GetRequiredService<ITagsProjectFactory>().Create(
            StreamTestContext.ProjectRoot,
            new XElement("Project",
                StreamTestContext.Channel("fake"),
                StreamTestContext.Channel("fake2"),
                StreamTestContext.EntryGroup("g1", StreamTestContext.Tag("a")),
                // 两个入口之间的非入口子树：结构上有，但不属于任何入口（不会被轮询）
                new XElement("TagGrp",
                    new XAttribute("name", "plain"),
                    new XAttribute("isEnabled", "true"),
                    new XAttribute("channel", "fake"),
                    StreamTestContext.Tag("p")),
                StreamTestContext.EntryGroup("g2", "fake2",
                    StreamTestContext.Group("sub", StreamTestContext.Tag("b")))));

        var index = ProjectIndex.Build(epoch: 1, project);

        Assert.Equal(new[] { "g1", "g2" }, index.Entries.Select(e => e.Key).ToArray());
        Assert.Equal(new[] { "g1/a" }, EntryLeafPaths(index, "g1"));
        Assert.Equal(new[] { "g2/sub/b" }, EntryLeafPaths(index, "g2"));

        // 非入口子树的路径/深度不受前一个入口的上下文影响
        var p = Assert.IsType<TagLeafNodeDto>(Assert.Single(index.Tree.Nodes, n => n.Path == "plain/p"));
        Assert.Equal("plain", p.Parent);
        Assert.Equal(2, p.Depth);
    }

    private static string[] EntryLeafPaths(ProjectIndex index, string key) =>
        index.ByKey[key].Collector.GetAll().Select(v => v.Path).OrderBy(p => p, StringComparer.Ordinal).ToArray();
}
