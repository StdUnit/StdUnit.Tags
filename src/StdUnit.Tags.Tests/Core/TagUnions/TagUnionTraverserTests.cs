using StdUnit.Tags;
using StdUnit.Tags.ComScanner;
using StdUnit.Tags.ComScanner.Channels;
using StdUnit.Tags.ComScanner.Tags;
using StdUnit.Tags.S7;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StdUnit.Tags.Tests.Core.TagUnions
{
    public class TagUnionTraverserTests
    {
        public TagUnionTraverserTests()
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

        private readonly IServiceProvider _root;

        [Fact]
        public void Traverser_Visits_All_Node_Types_In_Order()
        {
            using var scope = this._root.CreateScope();
            var sp = scope.ServiceProvider;
            var factory = sp.GetRequiredService<ITagsProjectFactory>();
            var dir = TestPaths.Fixture("Core", "TagUnions");
            var proj = factory.Create(dir);

            var groups = new List<string>();
            var cbnts = new List<string>();
            var units = new List<string>();

            var traverser = new TagUnionTraverser(
                grp => groups.Add(grp.TagName()),
                cb => cbnts.Add(cb.TagName()),
                tag => units.Add(tag.TagName())
            );

            // act
            var union = new TagUnion.TagGrp(proj.Tags);
            union.Accept(traverser);

            // assert
            Assert.Equal(new[] { "__main__", "g1", "g11", "g12", "扫码枪" }, groups);
            Assert.Equal(new[] { "拍照请求", "拍照响应" }, cbnts);
            // units: t1, c1, t2 in traversal order
            Assert.Equal(new[] {
                "拍照-请求-标志", "拍照-请求-料号", "拍照-请求-程序号",
                "拍照-响应-标志", "拍照-响应-B1", "拍照-响应-I2",
                "输入"
            }, units);
        }
    }
}
