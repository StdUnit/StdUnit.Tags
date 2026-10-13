using StdUnit.Tags.ModbusTcp;
using Xunit;

namespace StdUnit.Tags.Tests.ModbusTags;

/// <summary>
/// 寄存器空间（3x/4x）测点工厂：钉住子测点相对组合起始地址的偏移换算。<br/>
/// 寄存器空间的 TagOffset 按<b>字节</b>算（寄存器差 × 2），CacheOffset 才是寄存器索引。
/// </summary>
public class ModbusRegisterTagFactoryTests
{
    [Theory]
    // 寄存器位：TagOffset = 字节偏移（寄存器差×2），CacheOffset = 寄存器索引，NthBit = 位号（0~15）
    [InlineData("40001", "1~40007.0", 12, 6, 0)]
    [InlineData("40001", "1~40007.8", 12, 6, 8)]
    [InlineData("40001", "1~40008.1", 14, 7, 1)]
    [InlineData("40001", "1~40009.1", 16, 8, 1)]
    [InlineData("40001", "1~40009.15", 16, 8, 15)]
    [InlineData("40011", "40021.7", 20, 10, 7)]
    public void Test_HoldingRegisters_BitTagOffset(string baseAddr, string bitAddr, int tagOffset, int cacheOffset, byte nthBit)
    {
        var cbntDesc = new TagCbntDescriptor { Name = "g1", StartAddress = baseAddr };
        ModbusRegisterTagCbntBuilder builder = new();
        builder.WithCbntDescriptor(cbntDesc).WithChannel(null!);
        var factory = new ModbusRegisterTagFactory(builder, builder.TypedCbnt);
        var bittag = (ModbusRegisterBitCbntor)factory.CreateTag(new TagDescriptor() { TagName = bitAddr, RawAddress = bitAddr, TagKind = BuiltinTagKinds.BIT, TagSize = 2 });
        Assert.Equal(cacheOffset, bittag.CacheOffset);
        Assert.Equal(tagOffset, bittag.TagOffset);
        Assert.Equal(nthBit, bittag.NthBit);
        Assert.Equal(2, bittag.TagSize());
    }

    [Theory]
    [InlineData("40001", "1~40007", 12, 6)]
    [InlineData("40001", "40030", 58, 29)]
    public void Test_Int16TagOffset(string baseAddr, string tagAddr, int tagOffset, int cacheOffset)
    {
        var cbntDesc = new TagCbntDescriptor { Name = "g1", StartAddress = baseAddr };
        ModbusRegisterTagCbntBuilder builder = new();
        builder.WithCbntDescriptor(cbntDesc).WithChannel(null!);
        var factory = new ModbusRegisterTagFactory(builder, builder.TypedCbnt);
        var tag = (ModbusRegisterCbntorBase)factory.CreateTag(new TagDescriptor() { TagName = tagAddr, RawAddress = tagAddr, TagKind = BuiltinTagKinds.INT16, TagSize = 2 });
        Assert.Equal(cacheOffset, tag.CacheOffset);
        Assert.Equal(tagOffset, tag.TagOffset);
    }

    [Theory]
    [InlineData("40001", "1~40007", 12, 6)]
    [InlineData("40001", "40030", 58, 29)]
    public void Test_FloatTagOffset(string baseAddr, string tagAddr, int tagOffset, int cacheOffset)
    {
        var cbntDesc = new TagCbntDescriptor { Name = "g1", StartAddress = baseAddr };
        ModbusRegisterTagCbntBuilder builder = new();
        builder.WithCbntDescriptor(cbntDesc).WithChannel(null!);
        var factory = new ModbusRegisterTagFactory(builder, builder.TypedCbnt);
        var tag = (ModbusRegisterCbntorBase)factory.CreateTag(new TagDescriptor() { TagName = tagAddr, RawAddress = tagAddr, TagKind = BuiltinTagKinds.FLOAT, TagSize = 4 });
        Assert.Equal(cacheOffset, tag.CacheOffset);
        Assert.Equal(tagOffset, tag.TagOffset);
    }
}
