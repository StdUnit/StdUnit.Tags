using StdUnit.Tags.S7;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StdUnit.Tags.Tests.S7Tags;

public class S7DirectTagNumericEndianTests
{
    [Theory]
    [InlineData(EndianKinds.BigEndian)]
    [InlineData(EndianKinds.LittleEndian)]
    public async Task Int16_ReadAsync_RespectsEndian(EndianKinds endian)
    {
        const short expected = unchecked((short)0x1234);
        var payload = GetBytes(expected, endian);
        var fake = new FakeContinuousBytesChannel(payload);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "test-grp", IsEntry = false }, fake);
        var tag = CreateInt16Tag(endian, fake, grp);

        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal(expected, tag.Value);
        Assert.Equal(2, fake.LastReadLength);
    }

    [Theory]
    [InlineData(EndianKinds.BigEndian)]
    [InlineData(EndianKinds.LittleEndian)]
    public async Task Int16_WriteAsync_RespectsEndian(EndianKinds endian)
    {
        const short value = unchecked((short)0x1357);
        var fake = new FakeContinuousBytesChannel(new byte[2]);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "test-grp", IsEntry = false }, fake);
        var tag = CreateInt16Tag(endian, fake, grp);

        tag.Value = value;
        await tag.WriteAsync(CancellationToken.None);

        Assert.Equal(GetBytes(value, endian), fake.LastWriteBuffer);
    }

    [Theory]
    [InlineData(EndianKinds.BigEndian)]
    [InlineData(EndianKinds.LittleEndian)]
    public async Task UInt16_ReadAsync_RespectsEndian(EndianKinds endian)
    {
        const ushort expected = 0xABCD;
        var payload = GetBytes(expected, endian);
        var fake = new FakeContinuousBytesChannel(payload);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "test-grp", IsEntry = false }, fake);
        var tag = CreateUInt16Tag(endian, fake, grp);

        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal(expected, tag.Value);
        Assert.Equal(2, fake.LastReadLength);
    }

    [Theory]
    [InlineData(EndianKinds.BigEndian)]
    [InlineData(EndianKinds.LittleEndian)]
    public async Task UInt16_WriteAsync_RespectsEndian(EndianKinds endian)
    {
        const ushort value = 0x2468;
        var fake = new FakeContinuousBytesChannel(new byte[2]);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "test-grp", IsEntry = false }, fake);
        var tag = CreateUInt16Tag(endian, fake, grp);

        tag.Value = value;
        await tag.WriteAsync(CancellationToken.None);

        Assert.Equal(GetBytes(value, endian), fake.LastWriteBuffer);
    }

    [Theory]
    [InlineData(EndianKinds.BigEndian)]
    [InlineData(EndianKinds.LittleEndian)]
    public async Task Int32_ReadAsync_RespectsEndian(EndianKinds endian)
    {
        const int expected = unchecked((int)0x12345678);
        var payload = GetBytes(expected, endian);
        var fake = new FakeContinuousBytesChannel(payload);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "test-grp", IsEntry = false }, fake);
        var tag = CreateInt32Tag(endian, fake, grp);

        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal(expected, tag.Value);
        Assert.Equal(4, fake.LastReadLength);
    }

    [Theory]
    [InlineData(EndianKinds.BigEndian)]
    [InlineData(EndianKinds.LittleEndian)]
    public async Task Int32_WriteAsync_RespectsEndian(EndianKinds endian)
    {
        const int value = unchecked((int)0x0BADF00D);
        var fake = new FakeContinuousBytesChannel(new byte[4]);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "test-grp", IsEntry = false }, fake);
        var tag = CreateInt32Tag(endian, fake, grp);

        tag.Value = value;
        await tag.WriteAsync(CancellationToken.None);

        Assert.Equal(GetBytes(value, endian), fake.LastWriteBuffer);
    }

    [Theory]
    [InlineData(EndianKinds.BigEndian)]
    [InlineData(EndianKinds.LittleEndian)]
    public async Task UInt32_ReadAsync_RespectsEndian(EndianKinds endian)
    {
        const uint expected = 0x89ABCDEF;
        var payload = GetBytes(expected, endian);
        var fake = new FakeContinuousBytesChannel(payload);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "test-grp", IsEntry = false }, fake);
        var tag = CreateUInt32Tag(endian, fake, grp);

        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal(expected, tag.Value);
        Assert.Equal(4, fake.LastReadLength);
    }

    [Theory]
    [InlineData(EndianKinds.BigEndian)]
    [InlineData(EndianKinds.LittleEndian)]
    public async Task UInt32_WriteAsync_RespectsEndian(EndianKinds endian)
    {
        const uint value = 0xCAFEBABE;
        var fake = new FakeContinuousBytesChannel(new byte[4]);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "test-grp", IsEntry = false }, fake);
        var tag = CreateUInt32Tag(endian, fake, grp);

        tag.Value = value;
        await tag.WriteAsync(CancellationToken.None);

        Assert.Equal(GetBytes(value, endian), fake.LastWriteBuffer);
    }

    [Theory]
    [InlineData(EndianKinds.BigEndian)]
    [InlineData(EndianKinds.LittleEndian)]
    public async Task Int64_ReadAsync_RespectsEndian(EndianKinds endian)
    {
        const long expected = unchecked((long)0x0123456789ABCDEFL);
        var payload = GetBytes(expected, endian);
        var fake = new FakeContinuousBytesChannel(payload);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "test-grp", IsEntry = false }, fake);
        var tag = CreateInt64Tag(endian, fake, grp);

        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal(expected, tag.Value);
        Assert.Equal(8, fake.LastReadLength);
    }

    [Theory]
    [InlineData(EndianKinds.BigEndian)]
    [InlineData(EndianKinds.LittleEndian)]
    public async Task Int64_WriteAsync_RespectsEndian(EndianKinds endian)
    {
        const long value = unchecked((long)0x0F1E2D3C4B5A6978L);
        var fake = new FakeContinuousBytesChannel(new byte[8]);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "test-grp", IsEntry = false }, fake);
        var tag = CreateInt64Tag(endian, fake, grp);

        tag.Value = value;
        await tag.WriteAsync(CancellationToken.None);

        Assert.Equal(GetBytes(value, endian), fake.LastWriteBuffer);
    }

    [Theory]
    [InlineData(EndianKinds.BigEndian)]
    [InlineData(EndianKinds.LittleEndian)]
    public async Task UInt64_ReadAsync_RespectsEndian(EndianKinds endian)
    {
        const ulong expected = 0x0123456789ABCDEFUL;
        var payload = GetBytes(expected, endian);
        var fake = new FakeContinuousBytesChannel(payload);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "test-grp", IsEntry = false }, fake);
        var tag = CreateUInt64Tag(endian, fake, grp);

        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal(expected, tag.Value);
        Assert.Equal(8, fake.LastReadLength);
    }

    [Theory]
    [InlineData(EndianKinds.BigEndian)]
    [InlineData(EndianKinds.LittleEndian)]
    public async Task UInt64_WriteAsync_RespectsEndian(EndianKinds endian)
    {
        const ulong value = 0xF0E1D2C3B4A59687UL;
        var fake = new FakeContinuousBytesChannel(new byte[8]);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "test-grp", IsEntry = false }, fake);
        var tag = CreateUInt64Tag(endian, fake, grp);

        tag.Value = value;
        await tag.WriteAsync(CancellationToken.None);

        Assert.Equal(GetBytes(value, endian), fake.LastWriteBuffer);
    }

    [Theory]
    [InlineData(EndianKinds.BigEndian)]
    [InlineData(EndianKinds.LittleEndian)]
    public async Task Float_ReadAsync_RespectsEndian(EndianKinds endian)
    {
        const float expected = 123.456f;
        var payload = GetBytes(expected, endian);
        var fake = new FakeContinuousBytesChannel(payload);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "test-grp", IsEntry = false }, fake);
        var tag = CreateFloatTag(endian, fake, grp);

        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal(expected, tag.Value, 3);
        Assert.Equal(4, fake.LastReadLength);
    }

    [Theory]
    [InlineData(EndianKinds.BigEndian)]
    [InlineData(EndianKinds.LittleEndian)]
    public async Task Float_WriteAsync_RespectsEndian(EndianKinds endian)
    {
        const float value = -987.5f;
        var fake = new FakeContinuousBytesChannel(new byte[4]);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "test-grp", IsEntry = false }, fake);
        var tag = CreateFloatTag(endian, fake, grp);

        tag.Value = value;
        await tag.WriteAsync(CancellationToken.None);

        Assert.Equal(GetBytes(value, endian), fake.LastWriteBuffer);
    }

    private static Int16DirectTag CreateInt16Tag(EndianKinds endian, S7TagChannel? channel, ITagGrp grp) => new(
            new TagDescriptor
            {
                TagName = "i16",
                TagKind = BuiltinTagKinds.INT16,
                RawAddress = "DB1.200",
                EndianKind = endian,
            },
            thisChannel: null,
            parent: grp.IntoTagContainer()
        );

    private static UInt16DirectTag CreateUInt16Tag(EndianKinds endian, S7TagChannel? channel, ITagGrp grp) => new(
        new TagDescriptor
        {
            TagName = "u16",
            TagKind = BuiltinTagKinds.UINT16,
            RawAddress = "DB1.202",
            EndianKind = endian,
        },
        thisChannel: null,
        grp.IntoTagContainer()
    );

    private static Int32DirectTag CreateInt32Tag(EndianKinds endian, S7TagChannel? channel, ITagGrp grp) => new(
        new TagDescriptor
        {
            TagName = "i32",
            TagKind = BuiltinTagKinds.INT32,
            RawAddress = "DB1.204",
            EndianKind = endian,
        },
        thisChannel: null,
        grp.IntoTagContainer()
    );

    private static UInt32DirectTag CreateUInt32Tag(EndianKinds endian, IContinuousBytesBasedTagChannel channel, ITagGrp grp) => new(
        new TagDescriptor
        {
            TagName = "u32",
            TagKind = BuiltinTagKinds.UINT32,
            RawAddress = "DB1.208",
            EndianKind = endian,
        },
        thisChannel: null,
        grp.IntoTagContainer()
    );

    private static Int64DirectTag CreateInt64Tag(EndianKinds endian, IContinuousBytesBasedTagChannel channel, ITagGrp grp) => new(
        new TagDescriptor
        {
            TagName = "i64",
            TagKind = BuiltinTagKinds.INT64,
            RawAddress = "DB1.212",
            EndianKind = endian,
        },
        thisChannel: null,
        grp.IntoTagContainer()
    );

    private static UInt64DirectTag CreateUInt64Tag(EndianKinds endian, IContinuousBytesBasedTagChannel channel, ITagGrp grp) => new(
        new TagDescriptor
        {
            TagName = "u64",
            TagKind = BuiltinTagKinds.UINT64,
            RawAddress = "DB1.220",
            EndianKind = endian,
        },
        thisChannel: null,
        grp.IntoTagContainer()
    );

    private static FloatDirectTag CreateFloatTag(EndianKinds endian, IContinuousBytesBasedTagChannel channel, ITagGrp grp) => new(
        new TagDescriptor
        {
            TagName = "f32",
            TagKind = BuiltinTagKinds.FLOAT,
            RawAddress = "DB1.228",
            EndianKind = endian,
        },
        thisChannel: null,
        parent: grp.IntoTagContainer()
    );

    private static byte[] GetBytes(short value, EndianKinds endian)
    {
        var bytes = new byte[2];
        if (endian == EndianKinds.BigEndian)
        {
            BinaryPrimitives.WriteInt16BigEndian(bytes, value);
        }
        else
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes, value);
        }
        return bytes;
    }

    private static byte[] GetBytes(ushort value, EndianKinds endian)
    {
        var bytes = new byte[2];
        if (endian == EndianKinds.BigEndian)
        {
            BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        }
        else
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        }
        return bytes;
    }

    private static byte[] GetBytes(int value, EndianKinds endian)
    {
        var bytes = new byte[4];
        if (endian == EndianKinds.BigEndian)
        {
            BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        }
        else
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        }
        return bytes;
    }

    private static byte[] GetBytes(uint value, EndianKinds endian)
    {
        var bytes = new byte[4];
        if (endian == EndianKinds.BigEndian)
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        }
        else
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        }
        return bytes;
    }

    private static byte[] GetBytes(long value, EndianKinds endian)
    {
        var bytes = new byte[8];
        if (endian == EndianKinds.BigEndian)
        {
            BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        }
        else
        {
            BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        }
        return bytes;
    }

    private static byte[] GetBytes(ulong value, EndianKinds endian)
    {
        var bytes = new byte[8];
        if (endian == EndianKinds.BigEndian)
        {
            BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        }
        else
        {
            BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        }
        return bytes;
    }

    private static byte[] GetBytes(float value, EndianKinds endian)
    {
        // net472 无 BinaryPrimitives.WriteSingleBigEndian/LittleEndian（.NET 5+）。
        // 这里先取出 float 的位模式（Unsafe.As 是零分配的位重解释，
        // 不用 BitConverter.GetBytes —— 后者会堆分配，不适合作为本仓库的示范写法），
        // 再按目标字节序写入。
        var bytes = new byte[4];
        var bits = Unsafe.As<float, int>(ref value);
        if (endian == EndianKinds.BigEndian)
        {
            BinaryPrimitives.WriteInt32BigEndian(bytes, bits);
        }
        else
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes, bits);
        }
        return bytes;
    }

    private sealed class FakeContinuousBytesChannel : S7TagChannel
    {
        public FakeContinuousBytesChannel(byte[] payload)
            : base(
                new S7TagChannelDescriptor()
                {
                    Name = "fake",
                },
                new LoggerFactory().CreateLogger<S7TagChannel>()
             )
        {
            this.LastWriteBuffer = (byte[])payload.Clone();
        }

        public int LastReadLength { get; private set; }

        public byte[] LastWriteBuffer { get; private set; }

        public override Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;

        public override Task EnsureConnectedAsync(bool force, CancellationToken ct) => Task.CompletedTask;

        public override Task<byte[]> ReadAsync(string address, int count, CancellationToken ct)
        {
            if (count > this.LastWriteBuffer.Length)
            {
                throw new InvalidOperationException($"Unexpected read length={count}");
            }
            this.LastReadLength = count;
            return Task.FromResult(this.LastWriteBuffer.AsSpan(0, count).ToArray());
        }

        public override Task WriteAsync(string address, byte[] bytes, CancellationToken ct)
        {
            this.LastWriteBuffer = (byte[])bytes.Clone();
            return Task.CompletedTask;
        }

    }
}
