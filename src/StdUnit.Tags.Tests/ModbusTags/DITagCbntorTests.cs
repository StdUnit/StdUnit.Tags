using System;
using System.Threading;
using System.Threading.Tasks;
using StdUnit.Tags.ModbusTcp;
using Xunit;

namespace StdUnit.Tags.Tests.ModbusTags;

public class DITagCbntorTests
{
    private static (TestBoolTagCbnt cbnt, DITagCbntor tag) CreateContext()
    {
        var cbnt = new TestBoolTagCbnt(new TagCbntDescriptor { Name = "g", StartAddress = "0" });
        cbnt.ResizeCache(4);
        var descriptor = new TagDescriptor
        {
            TagName = "di1",
            RawAddress = "1~10001",
            TagKind = BuiltinTagKinds.DI,
            TagSize = 1,
        };
        var tag = new DITagCbntor(descriptor, cbnt, 0);
        return (cbnt, tag);
    }

    [Fact]
    public void Value_WhenCacheFalse_ReturnsFalse()
    {
        var (cbnt, tag) = CreateContext();
        cbnt.Cache.Span[0] = false;

        var result = tag.Value;

        Assert.Equal(false, result);
    }

    [Fact]
    public void Value_WhenCacheTrue_ReturnsTrue()
    {
        var (cbnt, tag) = CreateContext();
        cbnt.Cache.Span[0] = true;

        var result = tag.Value;

        Assert.Equal(true, result);
    }



    /// <summary>
    /// DI是只读的，不可写入
    /// </summary>
    [Fact]
    public void Value_Setter_ThrowsNotSupported()
    {
        var (_, tag) = CreateContext();

        Assert.Throws<NotSupportedException>(() => tag.Value = true);
    }
}
