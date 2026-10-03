using System;
using System.Linq;
using Itminus.Tags;
using Itminus.Tags.S7;
using Xunit;

namespace Itminus.Tags.Tests.Core.TagCbntors;

public class FloatTagCbntorEndianTests
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
    public void Float_Roundtrip(EndianKinds endian)
    {
        var cbnt = CreateCbnt(8);
        var d = new TagDescriptor { TagName = "f32", RawAddress = "0", TagKind = BuiltinTagKinds.FLOAT, TagSize = 4, EndianKind = endian };
        var tag = new S7FloatTagCbntor(d, cbnt, 0);

        tag.Value = 1.23456789f;
        Assert.Equal(1.23456789f, (float)tag.Value!);
    }

    [Fact]
    public void Float_BigEndian_WritesFourBytesInBigEndianOrder()
    {
        var cbnt = CreateCbnt(8);
        var d = new TagDescriptor { TagName = "f32", RawAddress = "0", TagKind = BuiltinTagKinds.FLOAT, TagSize = 4, EndianKind = EndianKinds.BigEndian };
        var tag = new S7FloatTagCbntor(d, cbnt, 0);

        const float value = 1.0f; // 0x3F800000 => big-endian bytes: [0x3F, 0x80, 0x00, 0x00]
        tag.Value = value;

        // 直接写死期望字节，不依赖 BinaryPrimitives.WriteSingleBigEndian（.NET 5+，net472 没有）
        var expected = new byte[] { 0x3F, 0x80, 0x00, 0x00 };
        var actual = cbnt.Cache.Span.Slice(0, 4).ToArray();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Float_LittleEndian_WritesFourBytesInLittleEndianOrder()
    {
        var cbnt = CreateCbnt(8);
        var d = new TagDescriptor { TagName = "f32", RawAddress = "0", TagKind = BuiltinTagKinds.FLOAT, TagSize = 4, EndianKind = EndianKinds.LittleEndian };
        var tag = new S7FloatTagCbntor(d, cbnt, 0);

        const float value = 1.0f; // 0x3F800000 => little-endian bytes: [0x00, 0x00, 0x80, 0x3F]
        tag.Value = value;

        var expected = new byte[] { 0x00, 0x00, 0x80, 0x3F };
        var actual = cbnt.Cache.Span.Slice(0, 4).ToArray();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Float_BigEndian_CacheBytesReverseOfLittleEndian()
    {
        var cbnt = CreateCbnt(8);
        var dBig = new TagDescriptor { TagName = "f32", RawAddress = "0", TagKind = BuiltinTagKinds.FLOAT, TagSize = 4, EndianKind = EndianKinds.BigEndian };
        new S7FloatTagCbntor(dBig, cbnt, 0).Value = 1.0f;

        var dLittle = new TagDescriptor { TagName = "f32", RawAddress = "0", TagKind = BuiltinTagKinds.FLOAT, TagSize = 4, EndianKind = EndianKinds.LittleEndian };
        new S7FloatTagCbntor(dLittle, cbnt, 4).Value = 2.0f;

        var actual1 = cbnt.Cache.Span.Slice(0, 4).ToArray();
        var actual2 = cbnt.Cache.Span.Slice(4, 4).ToArray();

        // 1.0f 小端字节序为 [0x00,0x00,0x80,0x3F]，大端即其反序；2.0f(0x40000000) 小端为 [0x00,0x00,0x00,0x40]
        var expected1 = new byte[] { 0x3F, 0x80, 0x00, 0x00 };
        var expected2 = new byte[] { 0x00, 0x00, 0x00, 0x40 };
        Assert.Equal(expected1, actual1);
        Assert.Equal(expected2, actual2);
    }
}
