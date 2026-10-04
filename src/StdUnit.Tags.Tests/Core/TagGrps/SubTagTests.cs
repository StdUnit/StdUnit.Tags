using StdUnit.Tags.S7;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.Core.TagGrps;

public class SubTagTests
{

    // 有意让这个Tag没有自己的通道，测试冒泡式访问通道
    class NoChannelTag : Tag<byte, S7TagChannel>
    {
        public NoChannelTag(TagDescriptor descriptor, TagContainer parent)
            : base(descriptor, null, parent)
        {
        }

        public override ITagChannel? Channel { get; set; }

        public override Task ReadAsync(CancellationToken ct) => Task.CompletedTask;

        public override Task WriteAsync(CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public void Test()
    {
        var channelFactory = new S7TagChannelFactory(new LoggerFactory());
        var channel = channelFactory.Create(new TagChannelDescriptor()
        {
            Driver = "S7",
            Name = "S7-1",
            Extras = new Dictionary<string, XElement>() { }
        });

        var cbnt = new S7TagCbntBuilder()
            .WithCbntDescriptor(new TagCbntDescriptor { Name = "cbnt1", StartAddress = "DB200.100.1" })
            .Configure(builder =>
            {
                var tagFactory = ((S7TagCbntBuilder)builder).MakeS7TagFactory();

                builder.AddTag(tagFactory.CreateTag(new TagDescriptor()
                {
                    TagName = "拍照-请求-标志",
                    RawAddress = "DB200.100.1",
                    TagKind = BuiltinTagKinds.BIT,
                    TagSize = 1,
                }));

                builder.AddTag(tagFactory.CreateTag(new TagDescriptor()
                {
                    TagName = "拍照-请求-料号",
                    RawAddress = "DB200.102",
                    TagKind = BuiltinTagKinds.BYTE,
                    TagSize = 1,
                }));

                builder.AddTag(tagFactory.CreateTag(new TagDescriptor()
                {
                    TagName = "拍照-请求-程序号",
                    RawAddress = "DB200.104",
                    TagKind = BuiltinTagKinds.INT16,
                    TagSize = 2,
                }));

                builder.AddTag(tagFactory.CreateTag(new TagDescriptor()
                {
                    TagName = "拍照-响应-标志",
                    RawAddress = "DB200.400.0",
                    TagKind = BuiltinTagKinds.BIT,
                    TagSize = 1,
                }));

                builder.AddTag(tagFactory.CreateTag(new TagDescriptor()
                {
                    TagName = "拍照-响应-OK",
                    RawAddress = "DB200.400.1",
                    TagKind = BuiltinTagKinds.BIT,
                    TagSize = 1,
                }));

                builder.AddTag(tagFactory.CreateTag(new TagDescriptor()
                {
                    TagName = "拍照-响应-NG",
                    RawAddress = "DB200.400.2",
                    TagKind = BuiltinTagKinds.BIT,
                    TagSize = 1,
                }));
            })
            .Build(channel)
            ;

        var root = new TagGrp(new TagGrpDescriptor { Name = "root", IsEntry = true }, channel);
        var grp1 = new TagGrp(new TagGrpDescriptor { Name = "sub1", IsEntry = false }, null);
        root.AddTag(grp1);
        var grp2 = new TagGrp(new TagGrpDescriptor { Name = "sub2", IsEntry = false }, null);
        grp1.AddTag(grp2);

        var noChannelTag = new NoChannelTag(
            new TagDescriptor()
            {
                TagName = "no-channel-tag",
                TagSize = 1,
                RawAddress = "some-address",
                TagKind = BuiltinTagKinds.BYTE,
            },
            grp2.IntoTagContainer()
        );

        grp2.AddTag(cbnt);
        grp2.AddTag(noChannelTag);



        // 测试层级式访问节点
        var tag1 = root.SelectTag("sub1/sub2/cbnt1/拍照-请求-标志");
        Assert.Equal("拍照-请求-标志", tag1.TagName());
        var tag2 = root.SelectTag("sub1/sub2/cbnt1/拍照-响应-OK");
        Assert.Equal("拍照-响应-OK", tag2.TagName());

        // 测试冒泡式访问通道
        Assert.Equal(cbnt.SearchRequiredChannel(), root.Channel);
        Assert.Equal(root.Channel, noChannelTag.SearchRequiredChannel());
    }
}
