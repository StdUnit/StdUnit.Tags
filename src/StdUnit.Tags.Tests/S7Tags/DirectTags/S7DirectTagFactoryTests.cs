using StdUnit.Tags.S7;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.S7Tags;

public class S7DirectTagFactoryTests
{
    private static (S7DirectTagFactory Factory, FakeContinuousBytesChannel Channel, TagGrp Grp) CreateContext(byte[]? payload = null)
    {
        payload ??= new byte[16];
        var channel = new FakeContinuousBytesChannel(payload);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "test-grp", IsEntry = false }, channel);
        var factory = new S7DirectTagFactory(grp.IntoTagContainer());
        return (factory, channel, grp);
    }

    [Fact]
    public void Create_BitTag_ShouldReturnBitDirectTag()
    {
        var (factory, _, _) = CreateContext();
        var descriptor = new TagDescriptor()
        {
            TagName = "bit-flag",
            TagKind = BuiltinTagKinds.BIT,
            RawAddress = "DB1.100.1",
        };
        var tag = factory.Create(descriptor, thisChannel: null);
        Assert.IsType<BitDirectTag>(tag);
        Assert.Equal("DB1.100.1", tag.NormalizedAddress());
    }

    [Fact]
    public void Create_BitTag_WithZeroTagSize_ShouldAutoCalculate()
    {
        var (factory, _, _) = CreateContext();
        var descriptor = new TagDescriptor()
        {
            TagName = "bit-flag",
            TagKind = BuiltinTagKinds.BIT,
            RawAddress = "DB1.100.7",
            TagSize = 0,
        };
        var tag = factory.Create(descriptor, thisChannel: null);
        // nthBit=7 -> 7/8+1 = 1
        Assert.IsType<BitDirectTag>(tag);
        Assert.Equal(1, descriptor.TagSize);
    }

    [Fact]
    public void Create_ByteTag_ShouldReturnByteDirectTag()
    {
        var (factory, _, _) = CreateContext();
        var descriptor = new TagDescriptor()
        {
            TagName = "byte-v",
            TagKind = BuiltinTagKinds.BYTE,
            RawAddress = "DB1.50",
        };
        var tag = factory.Create(descriptor, thisChannel: null);
        var byteTag = Assert.IsType<ByteDirectTag>(tag);
        Assert.Equal(1, byteTag.BufferSize);
        Assert.Equal("DB1.50", tag.NormalizedAddress());
    }

    [Fact]
    public void Create_ByteTag_WithZeroTagSize_ShouldDefaultToOne()
    {
        var (factory, _, _) = CreateContext();
        var descriptor = new TagDescriptor()
        {
            TagName = "byte-v",
            TagKind = BuiltinTagKinds.BYTE,
            RawAddress = "DB1.60",
            TagSize = 0,
        };
        factory.Create(descriptor, thisChannel: null);
        Assert.Equal(1, descriptor.TagSize);
    }

    [Fact]
    public void Create_Int16Tag_ShouldReturnInt16DirectTag()
    {
        var (factory, _, _) = CreateContext();
        var descriptor = new TagDescriptor()
        {
            TagName = "i16",
            TagKind = BuiltinTagKinds.INT16,
            RawAddress = "DB1.100",
        };
        var tag = factory.Create(descriptor, thisChannel: null);
        Assert.IsType<Int16DirectTag>(tag);
        Assert.Equal("DB1.100", tag.NormalizedAddress());
    }

    [Fact]
    public void Create_Int16Tag_WithZeroTagSize_ShouldDefaultToTwo()
    {
        var (factory, _, _) = CreateContext();
        var descriptor = new TagDescriptor()
        {
            TagName = "i16",
            TagKind = BuiltinTagKinds.INT16,
            RawAddress = "DB1.100",
            TagSize = 0,
        };
        factory.Create(descriptor, thisChannel: null);
        Assert.Equal(2, descriptor.TagSize);
    }

    [Fact]
    public void Create_UInt16Tag_ShouldReturnUInt16DirectTag()
    {
        var (factory, _, _) = CreateContext();
        var descriptor = new TagDescriptor()
        {
            TagName = "u16",
            TagKind = BuiltinTagKinds.UINT16,
            RawAddress = "DB1.102",
        };
        var tag = factory.Create(descriptor, thisChannel: null);
        Assert.IsType<UInt16DirectTag>(tag);
    }

    [Fact]
    public void Create_Int32Tag_ShouldReturnInt32DirectTag()
    {
        var (factory, _, _) = CreateContext();
        var descriptor = new TagDescriptor()
        {
            TagName = "i32",
            TagKind = BuiltinTagKinds.INT32,
            RawAddress = "DB1.104",
        };
        var tag = factory.Create(descriptor, thisChannel: null);
        Assert.IsType<Int32DirectTag>(tag);
    }

    [Fact]
    public void Create_UInt32Tag_ShouldReturnUInt32DirectTag()
    {
        var (factory, _, _) = CreateContext();
        var descriptor = new TagDescriptor()
        {
            TagName = "u32",
            TagKind = BuiltinTagKinds.UINT32,
            RawAddress = "DB1.108",
        };
        var tag = factory.Create(descriptor, thisChannel: null);
        Assert.IsType<UInt32DirectTag>(tag);
    }

    [Fact]
    public void Create_Int64Tag_ShouldReturnInt64DirectTag()
    {
        var (factory, _, _) = CreateContext();
        var descriptor = new TagDescriptor()
        {
            TagName = "i64",
            TagKind = BuiltinTagKinds.INT64,
            RawAddress = "DB1.112",
        };
        var tag = factory.Create(descriptor, thisChannel: null);
        Assert.IsType<Int64DirectTag>(tag);
    }

    [Fact]
    public void Create_UInt64Tag_ShouldReturnUInt64DirectTag()
    {
        var (factory, _, _) = CreateContext();
        var descriptor = new TagDescriptor()
        {
            TagName = "u64",
            TagKind = BuiltinTagKinds.UINT64,
            RawAddress = "DB1.120",
        };
        var tag = factory.Create(descriptor, thisChannel: null);
        Assert.IsType<UInt64DirectTag>(tag);
    }

    [Fact]
    public void Create_FloatTag_ShouldReturnFloatDirectTag()
    {
        var (factory, _, _) = CreateContext();
        var descriptor = new TagDescriptor()
        {
            TagName = "f32",
            TagKind = BuiltinTagKinds.FLOAT,
            RawAddress = "DB1.128",
        };
        var tag = factory.Create(descriptor, thisChannel: null);
        Assert.IsType<FloatDirectTag>(tag);
    }

    [Fact]
    public void Create_StrTag_ShouldReturnStrDirectTag()
    {
        var (factory, _, _) = CreateContext();
        var descriptor = new TagDescriptor()
        {
            TagName = "str-v",
            TagKind = BuiltinTagKinds.STR,
            RawAddress = "DB1.200",
            Extras = new Dictionary<string, XAttribute> { ["maxlen"] = new XAttribute("maxlen", "10") },
        };
        var tag = factory.Create(descriptor, thisChannel: null);
        var strTag = Assert.IsType<StrDirectTag>(tag);
        Assert.Equal(10, strTag.Maxlen);
        Assert.Equal(12, descriptor.TagSize); // 2 + 10
    }

    [Fact]
    public void Create_WithUnhandledTagKind_ShouldThrow()
    {
        var (factory, _, _) = CreateContext();
        var descriptor = new TagDescriptor()
        {
            TagName = "unknown",
            TagKind = "UNKNOWN_KIND",
            RawAddress = "DB1.300",
        };
        var ex = Assert.Throws<System.Exception>(() => factory.Create(descriptor, thisChannel: null));
        Assert.Contains("UNKNOWN_KIND", ex.Message);
    }

    [Fact]
    public void Create_WithThisChannel_ShouldPropagateToTag()
    {
        var (factory, channel, _) = CreateContext();
        var descriptor = new TagDescriptor()
        {
            TagName = "byte-v",
            TagKind = BuiltinTagKinds.BYTE,
            RawAddress = "DB1.50",
        };
        var tag = factory.Create(descriptor, thisChannel: channel);
        Assert.Same(channel, tag.Channel);
    }

    [Fact]
    public async Task Create_ByteTag_ReadWrite_WorksEndToEnd()
    {
        var (factory, channel, _) = CreateContext(new byte[] { 0x42 });
        var descriptor = new TagDescriptor()
        {
            TagName = "byte-v",
            TagKind = BuiltinTagKinds.BYTE,
            RawAddress = "DB1.50",
        };
        var tag = (ByteDirectTag)factory.Create(descriptor, thisChannel: null);

        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal((byte)0x42, tag.Value);

        tag.Value = 0x7F;
        await tag.WriteAsync(CancellationToken.None);

        Assert.NotNull(channel.LastWriteBuffer);
        var buf = Assert.Single(channel.LastWriteBuffer!);
        Assert.Equal(0x7F, buf);
    }

    private sealed class FakeContinuousBytesChannel : S7TagChannel
    {
        public FakeContinuousBytesChannel(byte[] payload)
            : base(new S7TagChannelDescriptor() { Name = "fake" }, new LoggerFactory().CreateLogger<S7TagChannel>())
        {
            LastWriteBuffer = (byte[])payload.Clone();
        }

        public int LastReadLength { get; private set; }
        public byte[] LastWriteBuffer { get; private set; }

        public override Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;
        public override Task EnsureConnectedAsync(bool force, CancellationToken ct) => Task.CompletedTask;

        public override Task<byte[]> ReadAsync(string address, int count, CancellationToken ct)
        {
            if (count > LastWriteBuffer.Length)
            {
                throw new System.InvalidOperationException($"Unexpected read length={count}");
            }
            LastReadLength = count;
            return Task.FromResult(LastWriteBuffer.AsSpan(0, count).ToArray());
        }

        public override Task WriteAsync(string address, byte[] bytes, CancellationToken ct)
        {
            LastWriteBuffer = (byte[])bytes.Clone();
            return Task.CompletedTask;
        }
    }
}
