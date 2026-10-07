using System;
using System.Threading;
using System.Threading.Tasks;
using StdUnit.Tags.ModbusTcp;
using Moq;
using NModbus;
using Xunit;

namespace StdUnit.Tags.Tests.ModbusTags;

/// <summary>
/// 寄存器空间里"寄存器中的某一位"的两个直接测点：
/// <see cref="HoldingRegisterBitDirectTag"/>（保持寄存器 4x，可读写）与
/// <see cref="InputRegisterBitDirectTag"/>（输入寄存器 3x，只读）。<br/>
/// <br/>
/// 位号 8~15 落在<b>同一个寄存器的第二个字节</b>（而不是相邻寄存器）——下面几条回归用例就是为此钉的。
/// 走真实通道层 <see cref="ModbusTcpChannel.ReadAsync"/> / <see cref="ModbusTcpChannel.WriteAsync"/>，
/// 底层用 Moq 的 <see cref="IModbusMaster"/> 模拟。
/// </summary>
public class ModbusTcpDirectTagRegisterBitTests
{
    private static (TestModbusTcpChannel channel, Mock<IModbusMaster> mock) CreateChannel()
    {
        var mock = new Mock<IModbusMaster>(MockBehavior.Strict);
        var channel = new TestModbusTcpChannel(new ModbusTcpTagChannelDescriptor { Name = "mb1" }, mock);
        return (channel, mock);
    }

    private static TagContainer CreateContainer(ITagChannel channel)
    {
        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp", IsEntry = true }, channel);
        return TagContainer.From(grp);
    }

    #region HoldingRegisterBitDirectTag

    [Fact]
    public async Task HoldingRegisterBit_ReadAsync_NthBitBelow8()
    {
        var (channel, mock) = CreateChannel();
        var container = CreateContainer(channel);
        var descriptor = new TagDescriptor
        {
            TagName = "bit-v",
            RawAddress = "1~40021.3",   // 寄存器偏移 20 的 bit3
            TagKind = BuiltinTagKinds.BIT,
        };

        // 寄存器值 0x0008 → UShortsToBytes 小端 → [0x08, 0x00] → 低字节 bit3 = 1
        mock
            .Setup(x => x.ReadHoldingRegistersAsync(1, (ushort)20, (ushort)1))
            .ReturnsAsync(new ushort[] { 0x0008 });
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new HoldingRegisterBitDirectTag(descriptor, channel, container);
        Assert.Equal((byte)3, tag.NthBit);

        await tag.ReadAsync(CancellationToken.None);

        Assert.True(tag.Value);
    }

    [Fact]
    public async Task HoldingRegisterBit_ReadAsync_NthBit8To15_UsesSecondByte()
    {
        // 回归测试：NthBit 8~15 应定位到同一寄存器的第二个字节（高字节），而非相邻寄存器
        var (channel, mock) = CreateChannel();
        var container = CreateContainer(channel);
        var descriptor = new TagDescriptor
        {
            TagName = "bit-v",
            RawAddress = "1~40021.8",
            TagKind = BuiltinTagKinds.BIT,
        };

        // 寄存器值 0x0100 → 小端 [0x00, 0x01] → 高字节 bit0 = 1
        mock
            .Setup(x => x.ReadHoldingRegistersAsync(1, (ushort)20, (ushort)1))
            .ReturnsAsync(new ushort[] { 0x0100 });
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new HoldingRegisterBitDirectTag(descriptor, channel, container);
        Assert.Equal((byte)8, tag.NthBit);

        await tag.ReadAsync(CancellationToken.None);

        Assert.True(tag.Value);
    }

    [Fact]
    public async Task HoldingRegisterBit_ReadAsync_NthBit8_WhenSecondByteClear_ReturnsFalse()
    {
        var (channel, mock) = CreateChannel();
        var container = CreateContainer(channel);
        var descriptor = new TagDescriptor
        {
            TagName = "bit-v",
            RawAddress = "1~40021.8",
            TagKind = BuiltinTagKinds.BIT,
        };

        mock
            .Setup(x => x.ReadHoldingRegistersAsync(1, (ushort)20, (ushort)1))
            .ReturnsAsync(new ushort[] { 0x0000 });
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new HoldingRegisterBitDirectTag(descriptor, channel, container);

        await tag.ReadAsync(CancellationToken.None);

        Assert.False(tag.Value);
    }

    [Fact]
    public async Task HoldingRegisterBit_WriteAsync_SetsBitBelow8()
    {
        var (channel, mock) = CreateChannel();
        var container = CreateContainer(channel);
        var descriptor = new TagDescriptor
        {
            TagName = "bit-v",
            RawAddress = "1~40021.3",
            TagKind = BuiltinTagKinds.BIT,
        };

        ushort[]? written = null;
        mock
            .Setup(x => x.ReadHoldingRegistersAsync(1, (ushort)20, (ushort)1))
            .ReturnsAsync(new ushort[] { 0x0000 });   // 原值 bit3=0
        mock
            .Setup(x => x.WriteMultipleRegistersAsync(1, (ushort)20, It.IsAny<ushort[]>()))
            .Returns(Task.CompletedTask)
            .Callback<byte, ushort, ushort[]>((_, _, data) => written = data);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new HoldingRegisterBitDirectTag(descriptor, channel, container)
        {
            Value = true,
        };
        await tag.WriteAsync(CancellationToken.None);

        Assert.NotNull(written);
        Assert.Single(written);
        Assert.Equal((ushort)0x0008, written![0]);   // 置 bit3
        Assert.False(tag.IsDirty);
    }

    [Fact]
    public async Task HoldingRegisterBit_WriteAsync_ClearsBitBelow8()
    {
        var (channel, mock) = CreateChannel();
        var container = CreateContainer(channel);
        var descriptor = new TagDescriptor
        {
            TagName = "bit-v",
            RawAddress = "1~40021.3",
            TagKind = BuiltinTagKinds.BIT,
        };

        ushort[]? written = null;
        mock
            .Setup(x => x.ReadHoldingRegistersAsync(1, (ushort)20, (ushort)1))
            .ReturnsAsync(new ushort[] { 0x0008 });   // 原值 bit3=1
        mock
            .Setup(x => x.WriteMultipleRegistersAsync(1, (ushort)20, It.IsAny<ushort[]>()))
            .Returns(Task.CompletedTask)
            .Callback<byte, ushort, ushort[]>((_, _, data) => written = data);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new HoldingRegisterBitDirectTag(descriptor, channel, container)
        {
            Value = false,
        };
        await tag.WriteAsync(CancellationToken.None);

        Assert.NotNull(written);
        Assert.Single(written);
        Assert.Equal((ushort)0x0000, written![0]);   // 清 bit3
        Assert.False(tag.IsDirty);
    }

    [Fact]
    public async Task HoldingRegisterBit_WriteAsync_SetsBit8To15_SecondByte()
    {
        // 回归测试：写 NthBit 8~15 应修改第二字节
        var (channel, mock) = CreateChannel();
        var container = CreateContainer(channel);
        var descriptor = new TagDescriptor
        {
            TagName = "bit-v",
            RawAddress = "1~40021.9",
            TagKind = BuiltinTagKinds.BIT,
        };

        ushort[]? written = null;
        mock
            .Setup(x => x.ReadHoldingRegistersAsync(1, (ushort)20, (ushort)1))
            .ReturnsAsync(new ushort[] { 0x0000 });   // 原值高字节 bit1=0
        mock
            .Setup(x => x.WriteMultipleRegistersAsync(1, (ushort)20, It.IsAny<ushort[]>()))
            .Returns(Task.CompletedTask)
            .Callback<byte, ushort, ushort[]>((_, _, data) => written = data);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new HoldingRegisterBitDirectTag(descriptor, channel, container)
        {
            Value = true,
        };
        await tag.WriteAsync(CancellationToken.None);

        Assert.NotNull(written);
        Assert.Single(written);
        Assert.Equal((ushort)0x0200, written![0]);   // 高字节 bit1 置位
        Assert.False(tag.IsDirty);
    }

    #endregion

    #region InputRegisterBitDirectTag

    [Fact]
    public async Task InputRegisterBit_ReadAsync_NthBitBelow8()
    {
        var (channel, mock) = CreateChannel();
        var container = CreateContainer(channel);
        var descriptor = new TagDescriptor
        {
            TagName = "irbit-v",
            RawAddress = "1~30021.2",
            TagKind = BuiltinTagKinds.BIT,
        };

        mock
            .Setup(x => x.ReadInputRegistersAsync(1, (ushort)20, (ushort)1))
            .ReturnsAsync(new ushort[] { 0x0004 });   // bit2 = 1
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new InputRegisterBitDirectTag(descriptor, channel, container);
        Assert.Equal((byte)2, tag.NthBit);

        await tag.ReadAsync(CancellationToken.None);

        Assert.True(tag.Value);
    }

    [Fact]
    public async Task InputRegisterBit_ReadAsync_NthBit8To15_UsesSecondByte()
    {
        var (channel, mock) = CreateChannel();
        var container = CreateContainer(channel);
        var descriptor = new TagDescriptor
        {
            TagName = "irbit-v",
            RawAddress = "1~30021.10",
            TagKind = BuiltinTagKinds.BIT,
        };

        mock
            .Setup(x => x.ReadInputRegistersAsync(1, (ushort)20, (ushort)1))
            .ReturnsAsync(new ushort[] { 0x0400 });   // 高字节 bit2 = 1
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new InputRegisterBitDirectTag(descriptor, channel, container);
        Assert.Equal((byte)10, tag.NthBit);

        await tag.ReadAsync(CancellationToken.None);

        Assert.True(tag.Value);
    }

    [Fact]
    public async Task InputRegisterBit_ReadAsync_BitClear_ReturnsFalse()
    {
        var (channel, mock) = CreateChannel();
        var container = CreateContainer(channel);
        var descriptor = new TagDescriptor
        {
            TagName = "irbit-v",
            RawAddress = "1~30021.0",
            TagKind = BuiltinTagKinds.BIT,
        };

        mock
            .Setup(x => x.ReadInputRegistersAsync(1, (ushort)20, (ushort)1))
            .ReturnsAsync(new ushort[] { 0x0000 });
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new InputRegisterBitDirectTag(descriptor, channel, container);
        await tag.ReadAsync(CancellationToken.None);

        Assert.False(tag.Value);
    }

    [Fact]
    public async Task InputRegisterBit_WriteAsync_ThrowsNotSupported()
    {
        var (channel, mock) = CreateChannel();
        var container = CreateContainer(channel);
        var descriptor = new TagDescriptor
        {
            TagName = "irbit-v",
            RawAddress = "1~30021.2",
            TagKind = BuiltinTagKinds.BIT,
        };

        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new InputRegisterBitDirectTag(descriptor, channel, container);
        await Assert.ThrowsAsync<NotSupportedException>(() => tag.WriteAsync(CancellationToken.None));
    }

    #endregion
}
