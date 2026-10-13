using System;
using System.Threading;
using System.Threading.Tasks;
using StdUnit.Tags.ModbusTcp;
using Xunit;

namespace StdUnit.Tags.Tests.ModbusTags;

public class DOTagCbntorTests
{
    private static (TestBoolTagCbnt cbnt, DOTagCbntor tag) CreateContext()
    {
        var cbnt = new TestBoolTagCbnt(new TagCbntDescriptor { Name = "g", StartAddress = "0" });
        cbnt.ResizeCache(4);
        var descriptor = new TagDescriptor
        {
            TagName = "do1",
            RawAddress = "1~00001",
            TagKind = BuiltinTagKinds.DO,
            TagSize = 1,
        };
        var tag = new DOTagCbntor(descriptor, cbnt, 0);
        return (cbnt, tag);
    }

    [Fact]
    public void Value_WhenCacheFalse_ReturnsFalse()
    {
        var (cbnt, tag) = CreateContext();
        cbnt.Cache.Span[0] = false;

        Assert.Equal(false, tag.Value);
    }

    [Fact]
    public void Value_WhenCacheTrue_ReturnsTrue()
    {
        var (cbnt, tag) = CreateContext();
        cbnt.Cache.Span[0] = true;

        Assert.Equal(true, tag.Value);
    }

    [Fact]
    public void Value_SetTrue_WritesToCache()
    {
        var (cbnt, tag) = CreateContext();

        tag.Value = true;

        Assert.True(cbnt.Cache.Span[0]);
    }

    [Fact]
    public void Value_SetFalse_WritesToCache()
    {
        var (cbnt, tag) = CreateContext();
        cbnt.Cache.Span[0] = true;

        tag.Value = false;

        Assert.False(cbnt.Cache.Span[0]);
    }

    [Fact]
    public void Value_SetNonBool_Throws()
    {
        var (_, tag) = CreateContext();

        var ex = Assert.Throws<ArgumentException>(() => tag.Value = 42);
        Assert.Contains("Bit类型", ex.Message);
    }

    [Fact]
    public void Value_Set_MarksDirty()
    {
        var (cbnt, tag) = CreateContext();
        // 初始时已读取过，设置成同样的值
        cbnt.Cache.Span[0] = false;

        tag.Value = true;

        // Value setter 总会调用 MarkDirty
        Assert.True(tag.IsDirty);
        Assert.True(cbnt.IsDirty);
    }

    [Fact]
    public void Value_Set_UpdatesTimestamp()
    {
        var (_, tag) = CreateContext();

        tag.Value = true;

        Assert.NotEqual(default, tag.Timestamp);
    }
}
