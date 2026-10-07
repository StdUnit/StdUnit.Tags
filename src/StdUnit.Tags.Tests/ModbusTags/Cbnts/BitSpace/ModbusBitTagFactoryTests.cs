using StdUnit.Tags.ModbusTcp;
using Xunit;

namespace StdUnit.Tags.Tests.ModbusTags;

/// <summary>
/// 位空间（线圈 0x / 离散输入 1x）测点工厂：钉住 DI 子测点相对组合起始地址的偏移换算。<br/>
/// DI/DO 的缓存是 bool 数组，偏移就是"第几个位"，没有字节序与字节换算。
/// </summary>
public class ModbusBitTagFactoryTests
{
    [Theory]
    [InlineData("10011", "10021", 10, 10)]
    [InlineData("1~10011", "10021", 10, 10)]
    public void Test_Input_BitTagOffset(string baseAddr, string bitAddr, int tagOffset, int cacheOffset)
    {
        var cbntDesc = new TagCbntDescriptor { Name = "g1", StartAddress = baseAddr };
        ModbusBitTagCbntBuilder builder = new();
        builder.WithCbntDescriptor(cbntDesc).WithChannel(null!);
        var factory = new ModbusBitTagFactory(builder, builder.TypedCbnt);
        var tag = factory.CreateDITag(new TagDescriptor() { TagName = bitAddr, RawAddress = bitAddr, TagKind = BuiltinTagKinds.DI, TagSize = 1 });
        Assert.Equal(cacheOffset, tag.CacheOffset);
        Assert.Equal(tagOffset, tag.TagOffset);
        Assert.Equal(1, tag.TagSize());
    }
}
