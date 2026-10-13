using StdUnit.Tags.S7;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.S7Tags;

public class S7TagCbntBuilderTests
{
    [Fact]
    public void Build_ShouldNormalizeRelativeAddressesToCbntAreaAndBlock()
    {
        var channelFactory = new S7TagChannelFactory(new LoggerFactory());
        var channel = channelFactory.Create(new TagChannelDescriptor()
        {
            Driver = "S7",
            Name = "S7-1",
            Extras = new Dictionary<string, XElement>() { }
        });

        var cbntbuilder = new S7TagCbntBuilder()
            .WithCbntDescriptor(new TagCbntDescriptor { Name = "cbnt1", StartAddress = "DB200.100" })
            .Configure(builder =>
            {
                var tagFactory = ((S7TagCbntBuilder)builder).MakeS7TagFactory();

                builder.AddTag(tagFactory.CreateTag(new TagDescriptor()
                {
                    TagName = "byte-tag",
                    RawAddress = "$$104",
                    TagKind = BuiltinTagKinds.BYTE,
                    TagSize = 1,
                }));

                builder.AddTag(tagFactory.CreateTag(new TagDescriptor()
                {
                    TagName = "bit-tag",
                    RawAddress = "$$106.1",
                    TagKind = BuiltinTagKinds.BIT,
                    TagSize = 1,
                }));
            });
        Assert.Equal("cbnt1", cbntbuilder.Name);
        Assert.Equal("DB200.100", cbntbuilder.StartAddress);
        Assert.Null(cbntbuilder.Parent);

        var cbnt = cbntbuilder
            .Build(channel);

        var byteTag = cbnt.SelectTag("byte-tag");
        var bitTag = cbnt.SelectTag("bit-tag");




        Assert.Equal("DB200.104", byteTag.NormalizedAddress());
        Assert.Equal("DB200.106.1", bitTag.NormalizedAddress());
        Assert.Equal(4, byteTag.TagOffset);
        Assert.Equal(6, bitTag.TagOffset);
        Assert.Equal(7, ((TagCbnt<byte>)cbnt).CacheSize);
    }

    /// <summary>
    /// 组合自身的起始地址写成相对地址（$$）时：没有可沿用的上层，子测点的 Area 会被回填成 None，
    /// 而通道层只支持 DB/MB —— 必须在加载期报错，不要拖到运行期每轮轮询才抛。
    /// </summary>
    [Fact]
    public void Build_WhenCbntStartAddressIsRelative_ThrowsTagsProjectAddressException()
    {
        var channelFactory = new S7TagChannelFactory(new LoggerFactory());
        var channel = channelFactory.Create(new TagChannelDescriptor()
        {
            Driver = "S7",
            Name = "S7-1",
            Extras = new Dictionary<string, XElement>() { }
        });

        var cbntbuilder = new S7TagCbntBuilder()
            .WithCbntDescriptor(new TagCbntDescriptor { 
                Name = "cbnt1", 
                StartAddress = "$$100" 
            })
            .Configure(builder =>
            {
                var tagFactory = ((S7TagCbntBuilder)builder).MakeS7TagFactory();

                builder.AddTag(tagFactory.CreateTag(new TagDescriptor()
                {
                    TagName = "byte-tag",
                    RawAddress = "$$104",
                    TagKind = BuiltinTagKinds.BYTE,
                    TagSize = 1,
                }));
            });

        var ex = Assert.Throws<TagsProjectAddressException>(() => cbntbuilder.Build(channel));

        Assert.Contains("TagCbnt(cbnt1)", ex.Location);
        Assert.Contains("$$100", ex.Message);
        Assert.Contains("绝对地址", ex.Message);
    }
}
