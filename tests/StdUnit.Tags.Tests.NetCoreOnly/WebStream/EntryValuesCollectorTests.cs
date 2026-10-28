using System;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using StdUnit.Tags.WebStream;
using StdUnit.Tags.WebStream.Internal;
using Xunit;

namespace StdUnit.Tags.Tests.NetCoreOnly.WebStream;

/// <summary>
/// <see cref="EntryValuesCollector"/>：只报告真正变化的点、容器标志变化、全量取值。
/// </summary>
public class EntryValuesCollectorTests : IDisposable
{
    private readonly ServiceProvider _root;
    private readonly IServiceScope _scope;

    public EntryValuesCollectorTests()
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
                    StreamTestContext.Group("sub",
                        StreamTestContext.Tag("b", type: "BIT", address: "2")))));

    [Fact]
    public void Diff_ReportsOnlyChangedValues_AndIsIdempotent()
    {
        using var project = this.CreateProject();
        var index = ProjectIndex.Build(1, project);
        var collector = Assert.Single(index.Entries).Collector;

        // 构建时已按当前值预填：没有任何变化。
        Assert.Null(collector.Diff());

        StreamTestContext.TagAt(project, "g1/a").Value = (short)42;

        var delta = collector.Diff();
        var value = Assert.Single(delta!.Values!);
        Assert.Equal("g1/a", value.Path);
        Assert.Equal("INT16", value.TagKind);
        Assert.Equal("42", value.Raw);
        Assert.Empty(delta.ContainerFlags);

        // 同样的状态再算一次：没有变化。
        Assert.Null(collector.Diff());
    }

    [Fact]
    public void Diff_ReportsDirtyAndScannedChanges()
    {
        using var project = this.CreateProject();
        var index = ProjectIndex.Build(1, project);
        var collector = Assert.Single(index.Entries).Collector;
        var tag = StreamTestContext.TagAt(project, "g1/sub/b");

        tag.IsDirty = true;
        var delta = collector.Diff();
        var value = Assert.Single(delta!.Values!);
        Assert.Equal("g1/sub/b", value.Path);
        Assert.True(value.IsDirty);

        tag.IsScanned = true;
        var delta2 = collector.Diff();
        Assert.True(Assert.Single(delta2!.Values!).IsScanned);
    }

    [Fact]
    public void Diff_ReportsContainerEnabledChanges()
    {
        using var project = this.CreateProject();
        var index = ProjectIndex.Build(1, project);
        var collector = Assert.Single(index.Entries).Collector;
        var sub = project.Tags.Descendant("g1/sub").AsTagGrp();

        sub.IsEnabled = false;

        var delta = collector.Diff();
        Assert.Empty(delta!.Values);
        var flag = Assert.Single(delta.ContainerFlags);
        Assert.Equal("g1/sub", flag.Path);
        Assert.False(flag.IsEnabled);

        Assert.Null(collector.Diff());
    }

    [Fact]
    public void GetAll_ReturnsEveryLeafWithCurrentValue()
    {
        using var project = this.CreateProject();
        var index = ProjectIndex.Build(1, project);
        var collector = Assert.Single(index.Entries).Collector;

        StreamTestContext.TagAt(project, "g1/a").Value = (short)7;

        var all = collector.GetAll();
        Assert.Equal(
            new[] { "g1/a", "g1/sub/b" },
            all.Select(v => v.Path).OrderBy(p => p, StringComparer.Ordinal).ToArray());
        Assert.Equal("7", Assert.Single(all, v => v.Path == "g1/a").Raw);
    }
}
