using Itminus.Tags.ModbusTcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NModbus;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Itminus.Tags.Tests.ModbusTags;

public class ModbusTcpDirectTagTests
{
    private readonly ServiceProvider _root;

    public ModbusTcpDirectTagTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddModbusTcpSupport();
        });

        this._root = services.BuildServiceProvider();
    }

    [Fact]
    public void BuildProject_WithDirectTagsUnderTagGrp_Works()
    {
        using var scope = this._root.CreateScope();
        var sp = scope.ServiceProvider;
        var factory = sp.GetRequiredService<ITagsProjectFactory>();

var dir = TestPaths.Fixture("ModbusTags", "DirectTags");

        using var proj = factory.Create(dir);

        Assert.Single(proj.Channels);
        Assert.IsType<ModbusTcpChannel>(proj.Channels[0]);

        var grp = proj.Tags.SelectGrp("g-direct");
        Assert.NotNull(grp);

        var bit = grp.SelectTag("bit-v");
        Assert.Equal(BuiltinTagKinds.BIT, bit.TagKind());
        Assert.Equal("1~40021.7", bit.NormalizedAddress());

        var byteTag = grp.SelectTag("byte-v");
        Assert.Equal(BuiltinTagKinds.BYTE, byteTag.TagKind());
        Assert.Equal("1~40020", byteTag.NormalizedAddress());

        var u32 = grp.SelectTag("u32-v");
        Assert.Equal(BuiltinTagKinds.UINT32, u32.TagKind());
        Assert.Equal("1~40022", u32.NormalizedAddress());
        Assert.IsType<UInt32DirectTag>(u32);

        u32.Value = 3000000000u;
        Assert.Equal(3000000000u, u32.Value);

        var i16 = grp.SelectTag("i16-v");
        Assert.Equal(BuiltinTagKinds.INT16, i16.TagKind());
        Assert.Equal("1~40024", i16.NormalizedAddress());
    }

    [Fact]
    public void UInt32DirectTag_AcceptsValuesGreaterThanIntMaxValue()
    {
        var chDescriptor = new ModbusTcpTagChannelDescriptor
        {
            Name = "ModbusTcp-1",
        };
        var channel = new ModbusTcpChannel(
            chDescriptor,
            new LoggerFactory().CreateLogger<ModbusTcpChannel>()
        );

        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp", IsEntry = true }, channel);
        var container = TagContainer.From(grp);
        var descriptor = new TagDescriptor
        {
            TagName = "u32-v",
            RawAddress = "1~40022",
            TagKind = BuiltinTagKinds.UINT32,
        };

        var tag = new UInt32DirectTag(descriptor, channel, container);

        // 测试溢出
        uint newvalue = (uint)int.MaxValue + 1;
        tag.Value = newvalue;

        Assert.Equal(newvalue, tag.Value);
        Assert.True(tag.IsDirty);
    }

    [Fact]
    public async Task UInt32DirectTag_ReadAsync_BigEndian()
    {
        var mock = new Mock<IModbusMaster>(MockBehavior.Strict);
        var channel = new TestModbusTcpChannel(new ModbusTcpTagChannelDescriptor { Name = "mb1" }, mock);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp", IsEntry = true }, channel);
        var container = TagContainer.From(grp);
        var descriptor = new TagDescriptor
        {
            TagName = "u32-v",
            RawAddress = "1~40022",
            TagKind = BuiltinTagKinds.UINT32,
            EndianKind = EndianKinds.BigEndian,
        };

        // 大端设备 0xB2D05E00：线序 [B2,D0,5E,00] → NModbus 读回 [0xB2D0, 0x5E00] → cache [D0,B2,00,5E]
        mock
            .Setup(x => x.ReadHoldingRegistersAsync(1, (ushort)21, (ushort)2))
            .ReturnsAsync(new ushort[] { 0xB2D0, 0x5E00 });
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new UInt32DirectTag(descriptor, channel, container);
        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal(0xB2D05E00u, tag.Value);
    }

    [Fact]
    public async Task UInt32DirectTag_WriteAsync_BigEndian()
    {
        var mock = new Mock<IModbusMaster>(MockBehavior.Strict);
        var channel = new TestModbusTcpChannel(new ModbusTcpTagChannelDescriptor { Name = "mb1" }, mock);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp", IsEntry = true }, channel);
        var container = TagContainer.From(grp);
        var descriptor = new TagDescriptor
        {
            TagName = "u32-v",
            RawAddress = "1~40022",
            TagKind = BuiltinTagKinds.UINT32,
            EndianKind = EndianKinds.BigEndian,
        };

        ushort[]? written = null;
        mock
            .Setup(x => x.WriteMultipleRegistersAsync(1, (ushort)21, It.IsAny<ushort[]>()))
            .Returns(Task.CompletedTask)
            .Callback<byte, ushort, ushort[]>((_, _, data) => written = data);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new UInt32DirectTag(descriptor, channel, container)
        {
            Value = 0xB2D05E00u,
        };
        await tag.WriteAsync(CancellationToken.None);

        // 大端设备写回：cache [D0,B2,00,5E] → BytesToUShorts → [0xB2D0, 0x5E00] → 线序 [B2,D0,5E,00]
        Assert.NotNull(written);
        Assert.Equal(new ushort[] { 0xB2D0, 0x5E00 }, written);
        Assert.False(tag.IsDirty);
    }

    [Fact]
    public async Task UInt32DirectTag_ReadAsync_LittleEndian()
    {
        var mock = new Mock<IModbusMaster>(MockBehavior.Strict);
        var channel = new TestModbusTcpChannel(new ModbusTcpTagChannelDescriptor { Name = "mb1" }, mock);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp", IsEntry = true }, channel);
        var container = TagContainer.From(grp);
        var descriptor = new TagDescriptor
        {
            TagName = "u32-v",
            RawAddress = "1~40022",
            TagKind = BuiltinTagKinds.UINT32,
            EndianKind = EndianKinds.LittleEndian,
        };

        // 小端设备 0x12345678：低16位 0x5678 在低寄存器R0 → 线序 [56,78,12,34] → NModbus 读回 [0x5678, 0x1234] → cache [78,56,34,12]
        mock
            .Setup(x => x.ReadHoldingRegistersAsync(1, (ushort)21, (ushort)2))
            .ReturnsAsync(new ushort[] { 0x5678, 0x1234 });
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new UInt32DirectTag(descriptor, channel, container);
        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal(0x12345678u, tag.Value);
    }

    [Fact]
    public async Task Int32DirectTag_ReadWrite_BigEndian()
    {
        var mock = new Mock<IModbusMaster>(MockBehavior.Strict);
        var channel = new TestModbusTcpChannel(new ModbusTcpTagChannelDescriptor { Name = "mb1" }, mock);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp", IsEntry = true }, channel);
        var container = TagContainer.From(grp);
        var descriptor = new TagDescriptor
        {
            TagName = "i32-v",
            RawAddress = "1~40050",
            TagKind = BuiltinTagKinds.INT32,
            EndianKind = EndianKinds.BigEndian,
        };

        // 大端设备 42=0x0000002A：线序 [00,00,00,2A] → 读回 [0x0000, 0x002A]
        mock
            .Setup(x => x.ReadHoldingRegistersAsync(1, (ushort)49, (ushort)2))
            .ReturnsAsync(new ushort[] { 0x0000, 0x002A });
        ushort[]? written = null;
        mock
            .Setup(x => x.WriteMultipleRegistersAsync(1, (ushort)49, It.IsAny<ushort[]>()))
            .Returns(Task.CompletedTask)
            .Callback<byte, ushort, ushort[]>((_, _, data) => written = data);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new Int32DirectTag(descriptor, channel, container);
        await tag.ReadAsync(CancellationToken.None);
        Assert.Equal(42, tag.Value);

        tag.Value = -42;   // 0xFFFFFFD6
        await tag.WriteAsync(CancellationToken.None);
        Assert.NotNull(written);
        Assert.Equal(new ushort[] { 0xFFFF, 0xFFD6 }, written);
        Assert.False(tag.IsDirty);
    }

    [Fact]
    public async Task Int64DirectTag_ReadWrite_BigEndian()
    {
        var mock = new Mock<IModbusMaster>(MockBehavior.Strict);
        var channel = new TestModbusTcpChannel(new ModbusTcpTagChannelDescriptor { Name = "mb1" }, mock);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp", IsEntry = true }, channel);
        var container = TagContainer.From(grp);
        var descriptor = new TagDescriptor
        {
            TagName = "i64-v",
            RawAddress = "1~40060",
            TagKind = BuiltinTagKinds.INT64,
            EndianKind = EndianKinds.BigEndian,
        };

        // 大端设备 0x000000000000002A
        mock
            .Setup(x => x.ReadHoldingRegistersAsync(1, (ushort)59, (ushort)4))
            .ReturnsAsync(new ushort[] { 0x0000, 0x0000, 0x0000, 0x002A });
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new Int64DirectTag(descriptor, channel, container);
        await tag.ReadAsync(CancellationToken.None);
        Assert.Equal(42L, tag.Value);
    }

    [Fact]
    public async Task Int64DirectTag_ReadWrite_LittleEndian()
    {
        var mock = new Mock<IModbusMaster>(MockBehavior.Strict);
        var channel = new TestModbusTcpChannel(new ModbusTcpTagChannelDescriptor { Name = "mb1" }, mock);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp", IsEntry = true }, channel);
        var container = TagContainer.From(grp);
        var descriptor = new TagDescriptor
        {
            TagName = "i64-v",
            RawAddress = "1~40060",
            TagKind = BuiltinTagKinds.INT64,
            EndianKind = EndianKinds.LittleEndian,
        };

        // 小端设备 123456789012345：线序低字节在前
        var readBytes = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(readBytes, 123456789012345L);
        var regs = new ushort[4];
        for (int i = 0; i < 4; i++) regs[i] = (ushort)(readBytes[i * 2] | (readBytes[i * 2 + 1] << 8));
        mock
            .Setup(x => x.ReadHoldingRegistersAsync(1, (ushort)59, (ushort)4))
            .ReturnsAsync(regs);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new Int64DirectTag(descriptor, channel, container);
        await tag.ReadAsync(CancellationToken.None);
        Assert.Equal(123456789012345L, tag.Value);
    }

    [Fact]
    public async Task UInt16DirectTag_ReadWrite_BigEndian()
    {
        var mock = new Mock<IModbusMaster>(MockBehavior.Strict);
        var channel = new TestModbusTcpChannel(new ModbusTcpTagChannelDescriptor { Name = "mb1" }, mock);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp", IsEntry = true }, channel);
        var container = TagContainer.From(grp);
        var descriptor = new TagDescriptor
        {
            TagName = "u16-v",
            RawAddress = "1~40070",
            TagKind = BuiltinTagKinds.UINT16,
            EndianKind = EndianKinds.BigEndian,
        };

        // 大端设备 0xFFFE：线序 [FF,FE] → 读回 0xFFFE → cache [FE,FF] → 小端读 = 0xFFFE
        mock
            .Setup(x => x.ReadHoldingRegistersAsync(1, (ushort)69, (ushort)1))
            .ReturnsAsync(new ushort[] { 0xFFFE });
        ushort[]? written = null;
        mock
            .Setup(x => x.WriteMultipleRegistersAsync(1, (ushort)69, It.IsAny<ushort[]>()))
            .Returns(Task.CompletedTask)
            .Callback<byte, ushort, ushort[]>((_, _, data) => written = data);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new UInt16DirectTag(descriptor, channel, container);
        await tag.ReadAsync(CancellationToken.None);
        Assert.Equal((ushort)0xFFFE, tag.Value);

        tag.Value = 0x1234;
        await tag.WriteAsync(CancellationToken.None);
        Assert.NotNull(written);
        Assert.Equal(new ushort[] { 0x1234 }, written);
        Assert.False(tag.IsDirty);
    }

    [Fact]
    public async Task UInt16DirectTag_ReadAsync_LittleEndian()
    {
        var mock = new Mock<IModbusMaster>(MockBehavior.Strict);
        var channel = new TestModbusTcpChannel(new ModbusTcpTagChannelDescriptor { Name = "mb1" }, mock);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp", IsEntry = true }, channel);
        var container = TagContainer.From(grp);
        var descriptor = new TagDescriptor
        {
            TagName = "u16-v",
            RawAddress = "1~40070",
            TagKind = BuiltinTagKinds.UINT16,
            EndianKind = EndianKinds.LittleEndian,
        };

        // 小端设备物理值 0xFFFE：寄存器内低字节在前 → 线序 [FE,FF] → NModbus 读回 0xFEFF → cache [FF,FE] → 大端读 = 0xFFFE
        mock
            .Setup(x => x.ReadHoldingRegistersAsync(1, (ushort)69, (ushort)1))
            .ReturnsAsync(new ushort[] { 0xFEFF });
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new UInt16DirectTag(descriptor, channel, container);
        await tag.ReadAsync(CancellationToken.None);
        Assert.Equal((ushort)0xFFFE, tag.Value);
    }

    [Fact]
    public async Task UInt64DirectTag_ReadWrite_LittleEndian()
    {
        var mock = new Mock<IModbusMaster>(MockBehavior.Strict);
        var channel = new TestModbusTcpChannel(new ModbusTcpTagChannelDescriptor { Name = "mb1" }, mock);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp", IsEntry = true }, channel);
        var container = TagContainer.From(grp);
        var descriptor = new TagDescriptor
        {
            TagName = "u64-v",
            RawAddress = "1~40080",
            TagKind = BuiltinTagKinds.UINT64,
            EndianKind = EndianKinds.LittleEndian,
        };

        // 小端设备 0x0102030405060708
        var readBytes = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(readBytes, 0x0102030405060708UL);
        var regs = new ushort[4];
        for (int i = 0; i < 4; i++) regs[i] = (ushort)(readBytes[i * 2] | (readBytes[i * 2 + 1] << 8));
        mock
            .Setup(x => x.ReadHoldingRegistersAsync(1, (ushort)79, (ushort)4))
            .ReturnsAsync(regs);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new UInt64DirectTag(descriptor, channel, container);
        await tag.ReadAsync(CancellationToken.None);
        Assert.Equal(0x0102030405060708UL, tag.Value);
    }

    [Fact]
    public async Task FloatDirectTag_ReadAsync_BigEndian()
    {
        var mock = new Mock<IModbusMaster>(MockBehavior.Strict);
        var channel = new TestModbusTcpChannel(new ModbusTcpTagChannelDescriptor { Name = "mb1" }, mock);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp", IsEntry = true }, channel);
        var container = TagContainer.From(grp);
        var descriptor = new TagDescriptor
        {
            TagName = "f-v",
            RawAddress = "1~40040",
            TagKind = BuiltinTagKinds.FLOAT,
            EndianKind = EndianKinds.BigEndian,
        };

        // 大端设备 1.5f = 0x3FC00000：线序 [3F,C0,00,00] → 读回 [0x3FC0, 0x0000]
        mock
            .Setup(x => x.ReadHoldingRegistersAsync(1, (ushort)39, (ushort)2))
            .ReturnsAsync(new ushort[] { 0x3FC0, 0x0000 });
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new FloatDirectTag(descriptor, channel, container);
        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal(1.5f, tag.Value);
    }

    [Fact]
    public async Task FloatDirectTag_WriteAsync_BigEndian()
    {
        var mock = new Mock<IModbusMaster>(MockBehavior.Strict);
        var channel = new TestModbusTcpChannel(new ModbusTcpTagChannelDescriptor { Name = "mb1" }, mock);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp", IsEntry = true }, channel);
        var container = TagContainer.From(grp);
        var descriptor = new TagDescriptor
        {
            TagName = "f-v",
            RawAddress = "1~40040",
            TagKind = BuiltinTagKinds.FLOAT,
            EndianKind = EndianKinds.BigEndian,
        };

        ushort[]? written = null;
        mock
            .Setup(x => x.WriteMultipleRegistersAsync(1, (ushort)39, It.IsAny<ushort[]>()))
            .Returns(Task.CompletedTask)
            .Callback<byte, ushort, ushort[]>((_, _, data) => written = data);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new FloatDirectTag(descriptor, channel, container)
        {
            Value = 1.5f,
        };
        await tag.WriteAsync(CancellationToken.None);

        // 大端写回：线序 [3F,C0,00,00] → cache [C0,3F,00,00] → 写 [0x3FC0, 0x0000]
        Assert.NotNull(written);
        Assert.Equal(new ushort[] { 0x3FC0, 0x0000 }, written);
        Assert.False(tag.IsDirty);
    }

    [Fact]
    public async Task ByteDirectTag_ReadAsync_HoldingRegister_LittleEndian()
    {
        var mock = new Mock<IModbusMaster>(MockBehavior.Strict);
        var channel = new TestModbusTcpChannel(new ModbusTcpTagChannelDescriptor { Name = "mb1" }, mock);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp", IsEntry = true }, channel);
        var container = TagContainer.From(grp);
        var descriptor = new TagDescriptor
        {
            TagName = "byte-v",
            RawAddress = "1~40090",
            TagKind = BuiltinTagKinds.BYTE,
            EndianKind = EndianKinds.LittleEndian,
        };

        // 寄存器 0x1234 → UShortsToBytes 小端 → [0x34, 0x12] → 低字节 0x34
        mock
            .Setup(x => x.ReadHoldingRegistersAsync(1, (ushort)89, (ushort)1))
            .ReturnsAsync(new ushort[] { 0x1234 });
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new ByteDirectTag(descriptor, channel, container);
        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal((byte)0x34, tag.Value);
    }

    [Fact]
    public async Task ByteDirectTag_ReadAsync_HoldingRegister_BigEndian()
    {
        var mock = new Mock<IModbusMaster>(MockBehavior.Strict);
        var channel = new TestModbusTcpChannel(new ModbusTcpTagChannelDescriptor { Name = "mb1" }, mock);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp", IsEntry = true }, channel);
        var container = TagContainer.From(grp);
        var descriptor = new TagDescriptor
        {
            TagName = "byte-v",
            RawAddress = "1~40090",
            TagKind = BuiltinTagKinds.BYTE,
            EndianKind = EndianKinds.BigEndian,
        };

        // 寄存器 0x1234 → [0x34, 0x12] → 高字节 0x12
        mock
            .Setup(x => x.ReadHoldingRegistersAsync(1, (ushort)89, (ushort)1))
            .ReturnsAsync(new ushort[] { 0x1234 });
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new ByteDirectTag(descriptor, channel, container);
        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal((byte)0x12, tag.Value);
    }

    [Fact]
    public async Task ByteDirectTag_ReadAsync_InputRegister_Works()
    {
        var mock = new Mock<IModbusMaster>(MockBehavior.Strict);
        var channel = new TestModbusTcpChannel(new ModbusTcpTagChannelDescriptor { Name = "mb1" }, mock);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp", IsEntry = true }, channel);
        var container = TagContainer.From(grp);
        var descriptor = new TagDescriptor
        {
            TagName = "byte-v",
            RawAddress = "1~30090",
            TagKind = BuiltinTagKinds.BYTE,
            EndianKind = EndianKinds.LittleEndian,
        };

        // 寄存器 0xABCD → [0xCD, 0xAB] → 低字节 0xCD
        mock
            .Setup(x => x.ReadInputRegistersAsync(1, (ushort)89, (ushort)1))
            .ReturnsAsync(new ushort[] { 0xABCD });
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new ByteDirectTag(descriptor, channel, container);
        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal((byte)0xCD, tag.Value);
    }

    [Fact]
    public async Task ByteDirectTag_WriteAsync_HoldingRegister_LittleEndian()
    {
        var mock = new Mock<IModbusMaster>(MockBehavior.Strict);
        var channel = new TestModbusTcpChannel(new ModbusTcpTagChannelDescriptor { Name = "mb1" }, mock);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp", IsEntry = true }, channel);
        var container = TagContainer.From(grp);
        var descriptor = new TagDescriptor
        {
            TagName = "byte-v",
            RawAddress = "1~40090",
            TagKind = BuiltinTagKinds.BYTE,
            EndianKind = EndianKinds.LittleEndian,
        };

        ushort[]? written = null;
        mock
            .Setup(x => x.ReadHoldingRegistersAsync(1, (ushort)89, (ushort)1))
            .ReturnsAsync(new ushort[] { 0x1200 });   // 旧值：高字节 0x12
        mock
            .Setup(x => x.WriteMultipleRegistersAsync(1, (ushort)89, It.IsAny<ushort[]>()))
            .Returns(Task.CompletedTask)
            .Callback<byte, ushort, ushort[]>((_, _, data) => written = data);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new ByteDirectTag(descriptor, channel, container)
        {
            Value = (byte)0xAB,
        };
        await tag.WriteAsync(CancellationToken.None);

        // LittleEndian：新低字节 0xAB + 旧高字节 0x12 → 0x12AB
        Assert.NotNull(written);
        Assert.Single(written);
        Assert.Equal((ushort)0x12AB, written![0]);
        Assert.False(tag.IsDirty);
    }

    [Fact]
    public async Task ByteDirectTag_WriteAsync_HoldingRegister_BigEndian()
    {
        var mock = new Mock<IModbusMaster>(MockBehavior.Strict);
        var channel = new TestModbusTcpChannel(new ModbusTcpTagChannelDescriptor { Name = "mb1" }, mock);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp", IsEntry = true }, channel);
        var container = TagContainer.From(grp);
        var descriptor = new TagDescriptor
        {
            TagName = "byte-v",
            RawAddress = "1~40090",
            TagKind = BuiltinTagKinds.BYTE,
            EndianKind = EndianKinds.BigEndian,
        };

        ushort[]? written = null;
        mock
            .Setup(x => x.ReadHoldingRegistersAsync(1, (ushort)89, (ushort)1))
            .ReturnsAsync(new ushort[] { 0x0034 });   // 旧值：低字节 0x34
        mock
            .Setup(x => x.WriteMultipleRegistersAsync(1, (ushort)89, It.IsAny<ushort[]>()))
            .Returns(Task.CompletedTask)
            .Callback<byte, ushort, ushort[]>((_, _, data) => written = data);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new ByteDirectTag(descriptor, channel, container)
        {
            Value = (byte)0xAB,
        };
        await tag.WriteAsync(CancellationToken.None);

        // BigEndian：新高字节 0xAB + 旧低字节 0x34 → 0xAB34
        Assert.NotNull(written);
        Assert.Single(written);
        Assert.Equal((ushort)0xAB34, written![0]);
        Assert.False(tag.IsDirty);
    }
}
