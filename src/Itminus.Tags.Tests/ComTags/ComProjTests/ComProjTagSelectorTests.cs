using Itminus.Tags.ComScanner;
using Itminus.Tags.ComScanner.Channels;
using Itminus.Tags.ComScanner.Tags;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Text;
using System.Xml.Linq;
using Xunit;

namespace Itminus.Tags.Tests.ComTags.ComProjTests;

public class ComProjTagSelectorTests
{
    private readonly ServiceProvider _root;

    public ComProjTagSelectorTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.ConfigTagsLoader((sp, loader) =>
            {
                // 测试自定义的串口测点构建器
                loader.AddDirectTagBuilder<AnyLoadComTagBuilder>(
                    "ComScanner",
                    b => { },
                    b => b.TagDescriptor.TagKind == "AnyLoad"
                );
                loader.AddDirectTagBuilder<AnyLoadComTagBuilder>(
                    "COM",
                    b => { },
                    b => b.TagDescriptor.TagKind == "AnyLoad"
                );
            });

            b.AddComScannerSupport();
        });

        this._root = services.BuildServiceProvider();
    }

    [Theory]
    [InlineData("ComProjSelectorTests.xml")]
    public void TestLoad(string xmlpath)
    {
        using var scope = this._root.CreateScope();
        var sp = scope.ServiceProvider;
        //var factory = sp.GetRequiredService<ITagsProjectFactory>();
        var dir = TestPaths.Fixture("ComTags", "ComProjTests");
        xmlpath = Path.Combine(dir, xmlpath);
        var root = XElement.Load(xmlpath);
        using var proj = sp.MakeProject(dir!, root);

        // Test Channels
        Assert.Equal(3, proj.Channels.Count);
        Assert.IsType<LineBasedComChannel>(proj.Channels[0]);
        Assert.IsType<LineBasedComChannel>(proj.Channels[1]);
        Assert.IsType<LineBasedComChannel>(proj.Channels[2]);

        // Test Tags
        var g = proj.Tags.SelectGrp("g");
        Assert.NotNull(g);


        #region input group
        var gun1 = g.SelectTag("1#扫码枪");
        Assert.Equal("1#扫码枪", gun1.TagName());
        Assert.Equal(BuiltinTagKinds.STR, gun1.TagKind());
        Assert.IsType<LineBasedComChannel>(gun1.Channel);
        Assert.Equal(proj.Channels[0], gun1.Channel);
        var channel1 = (LineBasedComChannel)gun1.Channel;
        Assert.Null(channel1.NewLine);
        Assert.Equal(1, channel1.Capacity);

        var gun2 = g.SelectTag("2#扫码枪");
        Assert.Equal("2#扫码枪", gun2.TagName());
        Assert.Equal(BuiltinTagKinds.STR, gun2.TagKind());
        Assert.IsType<LineBasedComChannel>(gun2.Channel);
        Assert.Equal(proj.Channels[1], gun2.Channel);
        var channel2 = (LineBasedComChannel)gun2.Channel;
        Assert.Equal("\r\n", channel2.NewLine);
        Assert.Equal(42, channel2.Capacity);
        #endregion

        #region 自定义的串口测点
        var anyload = proj.Tags.SelectTag("称重仪/1#");
        Assert.Equal("1#", anyload.TagName());
        Assert.Equal("AnyLoad", anyload.TagKind());
        // 注意：称重仪用自己的 COM-3（通道不能跨入口共用）
        Assert.Equal(proj.Channels[2], anyload.Channel);
        // 注意：默认的访问模式是RW，我们有意在xml定义中不设置访问模式，以测试自定义的测点加载逻辑
        Assert.Null(anyload.AccessMode());
        Assert.Equal(TagAccessMode.RW, anyload.SearchAccessMode());
        #endregion

    }


    // 供测试用的DirectTagBuilder，构建AnyLoad类型的串口测点
    class AnyLoadComTagBuilder : TagBuilderBase
    {
        public AnyLoadComTagBuilder()
        {
        }

        protected override ITag Fallback(ITagChannel channel)
        {
            if (channel is null)
            {
                throw new Exception($"测点({this.Name})未配置通道({this.TagDescriptor.TagName})");
            }

            var tagKind = this.TagDescriptor.TagKind;
            if (tagKind == "AnyLoad")
            {
                if (channel is not ComChannelBase<string> com)
                {
                    throw new InvalidCastException($"测点({this.Name})当前通道必须是{nameof(ComChannelBase<string>)}！实际={channel.GetType()}");
                }

                var accessMode = this.TagDescriptor.AccessMode ?? this.Parent.SearchAccessMode();

                ITag tag = accessMode switch
                {
                    TagAccessMode.WO => new ComWriteOnlyTag<string>(this.TagDescriptor, com, TagContainer.From(this.Parent), converter: str => Encoding.UTF8.GetBytes(str)),
                    _ => new ComReadOnlyTag<string>(this.TagDescriptor, com, TagContainer.From(this.Parent)),
                };
                return tag;
            }

            throw new NotImplementedException($"串口测点({this.Name})的类型({tagKind})上不支持！");
        }
    }
}
