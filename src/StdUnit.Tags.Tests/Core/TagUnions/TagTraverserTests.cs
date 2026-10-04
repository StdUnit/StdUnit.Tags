using StdUnit.Tags.ComScanner;
using StdUnit.Tags.ComScanner.Channels;
using StdUnit.Tags.ComScanner.Tags;
using StdUnit.Tags.S7;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace StdUnit.Tags.Tests.Core.TagUnions;

public class TagTraverserTests
{
    private readonly ServiceProvider _root;

    public TagTraverserTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddS7Support();
            b.AddComScannerSupport();
        });

        this._root = services.BuildServiceProvider();
    }

    [Fact]
    public void TestLoad()
    {
        var scope = this._root.CreateScope();
        var sp = scope.ServiceProvider;
        var factory = sp.GetRequiredService<ITagsProjectFactory>();
        var dir = TestPaths.Fixture("Core", "TagUnions");
        using var proj = factory.Create(dir!);

        // Test Channels
        Assert.Equal(3, proj.Channels.Count);
        Assert.IsType<S7TagChannel>(proj.Channels[0]);
        Assert.IsType<S7TagChannel>(proj.Channels[1]);
        Assert.IsType<LineBasedComChannel>(proj.Channels[2]);

        // Test Tags
        var g1 = proj.Tags.SelectGrp("扫码枪");
        var union = new TagUnion.TagGrp(g1!);
        var tags = new List<ITag>();
        var visitor = new TagTraverser(t =>
        {
            if (t is ComReadOnlyTag<string> tag)
            {
                tags.Add(tag);
            }
        });
        union.Accept(visitor);
        Assert.Single(tags);
        Assert.Equal(g1.SelectTag("输入"), tags[0]);
    }
}
