using StdUnit.Tags;
using StdUnit.Tags.S7;
using Xunit;

namespace StdUnit.Tags.Tests.Core.TagCbntors;

public class IntegralTagCbntorEndianTests
{
    private static TestByteTagCbnt CreateCbnt(int cacheSize)
    {
        var cbnt = new TestByteTagCbnt(new TagCbntDescriptor { Name = "g", StartAddress = "0" });
        cbnt.ResizeCache(cacheSize);
        return cbnt;
    }

    [Theory]
    [InlineData(EndianKinds.LittleEndian)]
    [InlineData(EndianKinds.BigEndian)]
    public void Int16_Roundtrip(EndianKinds endian)
    {
        var cbnt = CreateCbnt(16);
        var d = new TagDescriptor { TagName = "i16", RawAddress = "0", TagKind = BuiltinTagKinds.INT16, TagSize = 2, EndianKind = endian };
        var tag = new S7Int16TagCbntor(d, cbnt, 0);

        tag.Value = (short)-12345;
        Assert.Equal((short)-12345, (short)tag.Value!);
    }

    [Theory]
    [InlineData(EndianKinds.LittleEndian)]
    [InlineData(EndianKinds.BigEndian)]
    public void UInt16_Roundtrip(EndianKinds endian)
    {
        var cbnt = CreateCbnt(16);
        var d = new TagDescriptor { TagName = "u16", RawAddress = "0", TagKind = BuiltinTagKinds.UINT16, TagSize = 2, EndianKind = endian };
        var tag = new S7UInt16TagCbntor(d, cbnt, 0);

        tag.Value = (ushort)54321;
        Assert.Equal((ushort)54321, (ushort)tag.Value!);
    }

    [Theory]
    [InlineData(EndianKinds.LittleEndian)]
    [InlineData(EndianKinds.BigEndian)]
    public void Int32_Roundtrip(EndianKinds endian)
    {
        var cbnt = CreateCbnt(16);
        var d = new TagDescriptor { TagName = "i32", RawAddress = "0", TagKind = BuiltinTagKinds.INT32, TagSize = 4, EndianKind = endian };
        var tag = new S7Int32TagCbntor(d, cbnt, 0);

        tag.Value = -123456789;
        Assert.Equal(-123456789, (int)tag.Value!);
    }

    [Theory]
    [InlineData(EndianKinds.LittleEndian)]
    [InlineData(EndianKinds.BigEndian)]
    public void UInt32_Roundtrip(EndianKinds endian)
    {
        var cbnt = CreateCbnt(16);
        var d = new TagDescriptor { TagName = "u32", RawAddress = "0", TagKind = BuiltinTagKinds.UINT32, TagSize = 4, EndianKind = endian };
        var tag = new S7UInt32TagCbntor(d, cbnt, 0);

        tag.Value = 4000000000u;
        Assert.Equal(4000000000u, (uint)tag.Value!);
    }

    [Theory]
    [InlineData(EndianKinds.LittleEndian)]
    [InlineData(EndianKinds.BigEndian)]
    public void Int64_Roundtrip(EndianKinds endian)
    {
        var cbnt = CreateCbnt(32);
        var d = new TagDescriptor { TagName = "i64", RawAddress = "0", TagKind = BuiltinTagKinds.INT64, TagSize = 8, EndianKind = endian };
        var tag = new S7Int64TagCbntor(d, cbnt, 0);

        tag.Value = -1234567890123456789L;
        Assert.Equal(-1234567890123456789L, (long)tag.Value!);
    }

    [Theory]
    [InlineData(EndianKinds.LittleEndian)]
    [InlineData(EndianKinds.BigEndian)]
    public void UInt64_Roundtrip(EndianKinds endian)
    {
        var cbnt = CreateCbnt(32);
        var d = new TagDescriptor { TagName = "u64", RawAddress = "0", TagKind = BuiltinTagKinds.UINT64, TagSize = 8, EndianKind = endian };
        var tag = new S7UInt64TagCbntor(d, cbnt, 0);

        tag.Value = 12345678901234567890UL;
        Assert.Equal(12345678901234567890UL, (ulong)tag.Value!);
    }
}
