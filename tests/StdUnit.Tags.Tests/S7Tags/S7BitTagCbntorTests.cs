using StdUnit.Tags.S7;
using System;
using Xunit;

namespace StdUnit.Tags.Tests.S7Tags;

/// <summary>
/// <see cref="S7BitTagCbntor"/> 的取值/赋值：按 <see cref="TagCbntor.CacheOffset"/> 定位缓存字节的第 NthBit 位，
/// 只改动目标位、保留同字节其它位，并打上脏标记。
/// </summary>
public class S7BitTagCbntorTests
{
    private static (S7TagCbnt Cbnt, S7BitTagCbntor Tag) Create(int cacheSize = 4, int tagOffset = 1, int cacheOffset = 1, byte nthBit = 3)
    {
        var cbnt = new S7TagCbnt(new TagCbntDescriptor { Name = "cbnt1", StartAddress = "DB200.100" });
        cbnt.ResizeCache(cacheSize);

        var descriptor = new TagDescriptor
        {
            TagName = "bit-tag",
            TagKind = BuiltinTagKinds.BIT,
            RawAddress = $"DB200.{100 + tagOffset}.{nthBit}",
            TagSize = 1,
        };
        var tag = new S7BitTagCbntor(descriptor, cbnt, tagOffset, cacheOffset, nthBit);
        return (cbnt, tag);
    }

    [Fact]
    public void Value_SetTrue_ShouldSetTargetBitAndMarkDirty()
    {
        var (cbnt, tag) = Create(cacheOffset: 1, nthBit: 3);

        tag.Value = true;

        Assert.Equal(0b0000_1000, cbnt.Cache.Span[1]);
        Assert.True(tag.IsDirty);
        Assert.True(cbnt.IsDirty);
        Assert.NotEqual(default, tag.Timestamp);
        Assert.True((DateTime.UtcNow - tag.Timestamp).Duration() < TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Value_SetFalse_ShouldClearTargetBitOnly()
    {
        var (cbnt, tag) = Create(cacheOffset: 1, nthBit: 3);
        cbnt.Cache.Span[1] = 0xFF;

        tag.Value = false;

        Assert.Equal(0b1111_0111, cbnt.Cache.Span[1]);
        Assert.True(tag.IsDirty);
        Assert.True(cbnt.IsDirty);
    }

    [Fact]
    public void Value_SetTrue_ShouldNotTouchOtherBytes()
    {
        var (cbnt, tag) = Create(cacheOffset: 1, nthBit: 0);
        cbnt.Cache.Span[0] = 0xAA;
        cbnt.Cache.Span[2] = 0xBB;

        tag.Value = true;

        Assert.Equal(0xAA, cbnt.Cache.Span[0]);
        Assert.Equal(0b0000_0001, cbnt.Cache.Span[1]);
        Assert.Equal(0xBB, cbnt.Cache.Span[2]);
    }

    /// <summary>
    /// 跨字节位（地址写成 DB200.101.9 之类）在工厂里会归一化成 NthBit=1、CacheOffset=TagOffset+1，
    /// 赋值必须按 CacheOffset 写缓存，而不是按 TagOffset。
    /// </summary>
    [Fact]
    public void Value_SetTrue_ShouldWriteAtCacheOffsetNotTagOffset()
    {
        var (cbnt, tag) = Create(tagOffset: 1, cacheOffset: 2, nthBit: 1);

        tag.Value = true;

        Assert.Equal(0, cbnt.Cache.Span[1]);
        Assert.Equal(0b0000_0010, cbnt.Cache.Span[2]);
    }

    [Fact]
    public void Value_Get_ShouldReadTargetBit()
    {
        var (cbnt, tag) = Create(cacheOffset: 1, nthBit: 3);
        cbnt.Cache.Span[1] = 0b0000_1000;

        Assert.True((bool)tag.Value!);

        cbnt.Cache.Span[1] = 0b0000_0000;
        Assert.False((bool)tag.Value!);
    }

    [Fact]
    public void Value_SetNonBool_ShouldThrowArgumentException()
    {
        var (cbnt, tag) = Create(cacheOffset: 1, nthBit: 3);

        var ex = Assert.Throws<ArgumentException>(() => tag.Value = 1);

        Assert.Equal("value", ex.ParamName);
        Assert.False(tag.IsDirty);
        Assert.False(cbnt.IsDirty);
        Assert.Equal(0, cbnt.Cache.Span[1]);
    }

    [Fact]
    public void Value_SetNull_ShouldThrowArgumentException()
    {
        var (_, tag) = Create(cacheOffset: 1, nthBit: 3);

        var ex = Assert.Throws<ArgumentException>(() => tag.Value = null);

        Assert.Equal("value", ex.ParamName);
    }
}
