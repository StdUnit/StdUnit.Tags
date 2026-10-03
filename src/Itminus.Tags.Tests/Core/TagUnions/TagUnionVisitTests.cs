using Itminus.Tags.ComScanner;
using Itminus.Tags.ComScanner.Channels;
using Itminus.Tags.S7;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Itminus.Tags.Tests.Core.TagUnions;

public class TagUnionVisitTests
{
    private readonly ServiceProvider _root;

    public TagUnionVisitTests()
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



    class MyVisitor : TraversingVisitorBase
    {
        private readonly Action<ITagGrp> visitGrp;
        private readonly Action<ITagCbnt> visitCbnt;
        private readonly Action<ITag> visitTag;

        public MyVisitor(Action<ITagGrp> visitGrp, Action<ITagCbnt> visitCbnt, Action<ITag> visitTag)
        {
            this.visitGrp = visitGrp;
            this.visitCbnt = visitCbnt;
            this.visitTag = visitTag;
        }

        protected override void Process(ITagGrp grp) => this.visitGrp(grp);

        protected override void Process(ITagCbnt cbnt) => this.visitCbnt(cbnt);

        protected override void Process(ITag tag) => this.visitTag(tag);
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
        var groups = new List<ITagGrp>();
        var tags = new List<ITag>();
        var cbnts = new List<ITagCbnt>();
        var g1 = proj.Tags.SelectGrp("g1");
        var union = new TagUnion.TagGrp(g1!); ;
        var visitor = new MyVisitor(
            grp => groups.Add(grp),
            cbnt => cbnts.Add(cbnt),
            tag => tags.Add(tag)
            );
        union.Accept(visitor);
        Assert.Equal(3, groups.Count);
        Assert.Equal(2, cbnts.Count);
        Assert.Equal(6, tags.Count);
    }
}
