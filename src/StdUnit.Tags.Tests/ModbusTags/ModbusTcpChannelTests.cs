using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StdUnit.Tags.ModbusTcp;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NModbus;
using Xunit;

namespace StdUnit.Tags.Tests.ModbusTags;

/// <summary>
/// 测试用 ModbusTcpChannel，重写 <see cref="ModbusTcpChannel.CreateConnectionAsync"/>
/// 以返回 Moq 创建的 <see cref="IModbusMaster"/>。
/// </summary>
internal class TestModbusTcpChannel : ModbusTcpChannel
{
    public Mock<IModbusMaster> MasterMock { get; }

    public TestModbusTcpChannel(ModbusTcpTagChannelDescriptor descriptor, Mock<IModbusMaster> masterMock)
        : base(descriptor, NullLogger<ModbusTcpChannel>.Instance)
    {
        MasterMock = masterMock;
    }

    protected override Task<IModbusMaster> CreateConnectionAsync(int timeout, CancellationToken ct)
        => Task.FromResult(MasterMock.Object);
}

public class ModbusTcpChannelTests
{
    private static (TestModbusTcpChannel channel, Mock<IModbusMaster> mock) CreateChannel()
    {
        var mock = new Mock<IModbusMaster>(MockBehavior.Strict);
        var channel = new TestModbusTcpChannel(new ModbusTcpTagChannelDescriptor { Name = "mb1" }, mock);
        return (channel, mock);
    }

    #region ReadRegistersAsync — HoldingRegisters (address ~40001)

    [Fact]
    public async Task ReadRegistersAsync_HoldingRegisters_ReturnsRegisters()
    {
        var (channel, mock) = CreateChannel();
        mock
            .Setup(x => x.ReadHoldingRegistersAsync(1, (ushort)0, (ushort)2))
            .ReturnsAsync(new ushort[] { 0x1234, 0x5678 });
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var result = await channel.ReadRegistersAsync("1~40001", 2, CancellationToken.None);

        Assert.Equal(new ushort[] { 0x1234, 0x5678 }, result);
    }

    [Fact]
    public async Task ReadRegistersAsync_HoldingRegisters_OverPduLimit_Batches()
    {
        var (channel, mock) = CreateChannel();
        // 126 个寄存器 > 125 单帧上限 → 应分 2 批：125 + 1
        mock
            .Setup(x => x.ReadHoldingRegistersAsync(1, (ushort)0, (ushort)125))
            .ReturnsAsync(Enumerable.Repeat((ushort)1, 125).ToArray());
        mock
            .Setup(x => x.ReadHoldingRegistersAsync(1, (ushort)125, (ushort)1))
            .ReturnsAsync(new ushort[] { 2 });
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var result = await channel.ReadRegistersAsync("1~40001", 126, CancellationToken.None);

        Assert.Equal(126, result.Length);
        Assert.Equal(2, result[125]);
        mock.Verify(x => x.ReadHoldingRegistersAsync(It.IsAny<byte>(), It.IsAny<ushort>(), It.IsAny<ushort>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ReadRegistersAsync_HoldingRegisters_ExactlyPduLimit_SingleCall()
    {
        var (channel, mock) = CreateChannel();
        mock
            .Setup(x => x.ReadHoldingRegistersAsync(1, (ushort)0, (ushort)125))
            .ReturnsAsync(Enumerable.Repeat((ushort)7, 125).ToArray());
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var result = await channel.ReadRegistersAsync("1~40001", 125, CancellationToken.None);

        Assert.Equal(125, result.Length);
        mock.Verify(x => x.ReadHoldingRegistersAsync(It.IsAny<byte>(), It.IsAny<ushort>(), It.IsAny<ushort>()), Times.Once);
    }

    [Fact]
    public async Task ReadRegistersAsync_HoldingRegisters_CustomMaxReadRegisters_BatchesAtConfiguredSize()
    {
        var mock = new Mock<IModbusMaster>(MockBehavior.Strict);
        var channel = new TestModbusTcpChannel(new ModbusTcpTagChannelDescriptor { Name = "mb1", MaxReadRegisters = 50 }, mock);
        mock
            .Setup(x => x.ReadHoldingRegistersAsync(1, (ushort)0, (ushort)50))
            .ReturnsAsync(Enumerable.Repeat((ushort)3, 50).ToArray());
        mock
            .Setup(x => x.ReadHoldingRegistersAsync(1, (ushort)50, (ushort)50))
            .ReturnsAsync(Enumerable.Repeat((ushort)4, 50).ToArray());
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var result = await channel.ReadRegistersAsync("1~40001", 100, CancellationToken.None);

        Assert.Equal(100, result.Length);
        mock.Verify(x => x.ReadHoldingRegistersAsync(It.IsAny<byte>(), It.IsAny<ushort>(), It.IsAny<ushort>()), Times.Exactly(2));
    }

    #endregion

    #region ReadRegistersAsync — InputRegisters

    [Fact]
    public async Task ReadRegistersAsync_InputRegisters_ReturnsRegisters()
    {
        var (channel, mock) = CreateChannel();
        mock
            .Setup(x => x.ReadInputRegistersAsync(1, (ushort)0, (ushort)1))
            .ReturnsAsync(new ushort[] { 0xABCD });
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var result = await channel.ReadRegistersAsync("1~30001", 1, CancellationToken.None);

        Assert.Equal(new ushort[] { 0xABCD }, result);
    }

    [Fact]
    public async Task ReadRegistersAsync_InputRegisters_OverPduLimit_Batches()
    {
        var (channel, mock) = CreateChannel();
        mock
            .Setup(x => x.ReadInputRegistersAsync(1, (ushort)0, (ushort)125))
            .ReturnsAsync(Enumerable.Repeat((ushort)9, 125).ToArray());
        mock
            .Setup(x => x.ReadInputRegistersAsync(1, (ushort)125, (ushort)1))
            .ReturnsAsync(new ushort[] { 8 });
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var result = await channel.ReadRegistersAsync("1~30001", 126, CancellationToken.None);

        Assert.Equal(126, result.Length);
        Assert.Equal(8, result[125]);
        mock.Verify(x => x.ReadInputRegistersAsync(It.IsAny<byte>(), It.IsAny<ushort>(), It.IsAny<ushort>()), Times.Exactly(2));
    }

    #endregion

    #region ReadBitsAsync — InputContacts (10001)

    [Fact]
    public async Task ReadBitsAsync_InputContacts_ReturnsBools()
    {
        var (channel, mock) = CreateChannel();
        mock
            .Setup(x => x.ReadInputsAsync(1, (ushort)0, (ushort)2))
            .ReturnsAsync(new bool[] { true, false });
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var result = await channel.ReadBitsAsync("1~10001", 2, CancellationToken.None);

        Assert.Equal(new bool[] { true, false }, result);
    }

    [Fact]
    public async Task ReadBitsAsync_InputContacts_OverPduLimit_Batches()
    {
        var (channel, mock) = CreateChannel();
        // 2001 点 > 2000 单帧上限 → 应分 2 批：2000 + 1
        mock
            .Setup(x => x.ReadInputsAsync(1, (ushort)0, (ushort)2000))
            .ReturnsAsync(Enumerable.Repeat(false, 2000).ToArray());
        mock
            .Setup(x => x.ReadInputsAsync(1, (ushort)2000, (ushort)1))
            .ReturnsAsync(new bool[] { true });
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var result = await channel.ReadBitsAsync("1~10001", 2001, CancellationToken.None);

        Assert.Equal(2001, result.Length);
        Assert.True(result[2000]);
        mock.Verify(x => x.ReadInputsAsync(It.IsAny<byte>(), It.IsAny<ushort>(), It.IsAny<ushort>()), Times.Exactly(2));
    }

    #endregion

    #region ReadBitsAsync — OutputCoils (00001)

    [Fact]
    public async Task ReadBitsAsync_OutputCoils_ReturnsBools()
    {
        var (channel, mock) = CreateChannel();
        mock
            .Setup(x => x.ReadCoilsAsync(1, (ushort)0, (ushort)2))
            .ReturnsAsync(new bool[] { false, true });
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var result = await channel.ReadBitsAsync("1~00001", 2, CancellationToken.None);

        Assert.Equal(new bool[] { false, true }, result);
    }

    [Fact]
    public async Task ReadBitsAsync_OutputCoils_OverPduLimit_Batches()
    {
        var (channel, mock) = CreateChannel();
        mock
            .Setup(x => x.ReadCoilsAsync(1, (ushort)0, (ushort)2000))
            .ReturnsAsync(Enumerable.Repeat(false, 2000).ToArray());
        mock
            .Setup(x => x.ReadCoilsAsync(1, (ushort)2000, (ushort)1))
            .ReturnsAsync(new bool[] { true });
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var result = await channel.ReadBitsAsync("1~00001", 2001, CancellationToken.None);

        Assert.Equal(2001, result.Length);
        Assert.True(result[2000]);
        mock.Verify(x => x.ReadCoilsAsync(It.IsAny<byte>(), It.IsAny<ushort>(), It.IsAny<ushort>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ReadBitsAsync_OutputCoils_CustomMaxReadBits_BatchesAtConfiguredSize()
    {
        var mock = new Mock<IModbusMaster>(MockBehavior.Strict);
        var channel = new TestModbusTcpChannel(new ModbusTcpTagChannelDescriptor { Name = "mb1", MaxReadBits = 500 }, mock);
        mock
            .Setup(x => x.ReadCoilsAsync(1, (ushort)0, (ushort)500))
            .ReturnsAsync(Enumerable.Repeat(true, 500).ToArray());
        mock
            .Setup(x => x.ReadCoilsAsync(1, (ushort)500, (ushort)500))
            .ReturnsAsync(Enumerable.Repeat(false, 500).ToArray());
        mock
            .Setup(x => x.ReadCoilsAsync(1, (ushort)1000, (ushort)200))
            .ReturnsAsync(Enumerable.Repeat(true, 200).ToArray());
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var result = await channel.ReadBitsAsync("1~00001", 1200, CancellationToken.None);

        Assert.Equal(1200, result.Length);
        Assert.True(result[0]);
        Assert.False(result[500]);
        Assert.True(result[1000]);
        mock.Verify(x => x.ReadCoilsAsync(It.IsAny<byte>(), It.IsAny<ushort>(), It.IsAny<ushort>()), Times.Exactly(3));
    }

    #endregion

    #region WriteRegistersAsync — HoldingRegisters

    [Fact]
    public async Task WriteRegistersAsync_SmallPayload_SingleCall()
    {
        var (channel, mock) = CreateChannel();
        ushort[]? captured = null;
        mock
            .Setup(x => x.WriteMultipleRegistersAsync((byte)1, (ushort)0, It.IsAny<ushort[]>()))
            .Returns(Task.CompletedTask)
            .Callback<byte, ushort, ushort[]>((_, _, data) => captured = data);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        // 4 ushorts ≤ 123 → 单次整体写入
        await channel.WriteRegistersAsync("1~40001", new ushort[] { 0x0201, 0x0403, 0x0605, 0x0807 }, CancellationToken.None);

        mock.Verify(x => x.WriteMultipleRegistersAsync(It.IsAny<byte>(), It.IsAny<ushort>(), It.IsAny<ushort[]>()), Times.Once);
        Assert.NotNull(captured);
        Assert.Equal(4, captured.Length);
        Assert.Equal((ushort)0x0201, captured![0]);
        Assert.Equal((ushort)0x0807, captured[3]);
    }

    [Fact]
    public async Task WriteRegistersAsync_Empty_DoesNothing()
    {
        var (channel, mock) = CreateChannel();
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        await channel.WriteRegistersAsync("1~40001", Array.Empty<ushort>(), CancellationToken.None);

        mock.Verify(x => x.WriteMultipleRegistersAsync(It.IsAny<byte>(), It.IsAny<ushort>(), It.IsAny<ushort[]>()), Times.Never);
    }

    [Fact]
    public async Task WriteRegistersAsync_LargePayload_SplitsIntoChunks()
    {
        var (channel, mock) = CreateChannel();
        var offsets = new List<ushort>();
        var lengths = new List<int>();
        mock
            .Setup(x => x.WriteMultipleRegistersAsync((byte)1, It.IsAny<ushort>(), It.IsAny<ushort[]>()))
            .Returns(Task.CompletedTask)
            .Callback<byte, ushort, ushort[]>((_, start, data) =>
            {
                offsets.Add(start);
                lengths.Add(data.Length);
            });
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var regs = Enumerable.Range(0, 124).Select(i => (ushort)i).ToArray();
        await channel.WriteRegistersAsync("1~40001", regs, CancellationToken.None);

        // 第一次写入 123 个，第二次写入剩余的 1 个
        Assert.Equal(2, offsets.Count);
        Assert.Equal(0, offsets[0]);
        Assert.Equal(123, lengths[0]);
        Assert.Equal(123, offsets[1]);
        Assert.Equal(1, lengths[1]);
    }

    [Fact]
    public async Task WriteRegistersAsync_CustomMaxWriteRegisters_ChunksCorrectly()
    {
        var mock = new Mock<IModbusMaster>(MockBehavior.Strict);
        var channel = new TestModbusTcpChannel(new ModbusTcpTagChannelDescriptor { Name = "mb1", MaxWriteRegisters = 2 }, mock);
        mock
            .Setup(x => x.WriteMultipleRegistersAsync(
                (byte)1,
                It.IsAny<ushort>(),
                It.IsAny<ushort[]>()
            ))
            .Returns(Task.CompletedTask);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        // 4 ushorts → 应分 2 批写入（每批 2 个）
        await channel.WriteRegistersAsync("1~40001", new ushort[] { 1, 2, 3, 4 }, CancellationToken.None);

        mock.Verify(
            x => x.WriteMultipleRegistersAsync(1, 0, It.Is<ushort[]>(d => d.Length == 2)),
            Times.Once);
        mock.Verify(
            x => x.WriteMultipleRegistersAsync(1, 2, It.Is<ushort[]>(d => d.Length == 2)),
            Times.Once);
    }

    #endregion

    #region WriteBitsAsync — OutputCoils

    [Fact]
    public async Task WriteBitsAsync_OutputCoils_WritesBools()
    {
        var (channel, mock) = CreateChannel();
        bool[]? captured = null;
        mock
            .Setup(x => x.WriteMultipleCoilsAsync((byte)1, (ushort)0, It.IsAny<bool[]>()))
            .Returns(Task.CompletedTask)
            .Callback<byte, ushort, bool[]>((_, _, data) => captured = data);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        await channel.WriteBitsAsync("1~00001", new bool[] { true, false, true }, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(new bool[] { true, false, true }, captured);
    }

    #endregion

    #region Unsupported areas

    [Fact]
    public async Task WriteRegistersAsync_InputRegisters_Throws()
    {
        var (channel, _) = CreateChannel();
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            channel.WriteRegistersAsync("1~30001", new ushort[] { 1 }, CancellationToken.None));
    }

    [Fact]
    public async Task WriteBitsAsync_InputContacts_Throws()
    {
        var (channel, _) = CreateChannel();
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            channel.WriteBitsAsync("1~10001", new bool[] { true }, CancellationToken.None));
    }

    [Fact]
    public async Task ReadRegistersAsync_Coils_Throws()
    {
        var (channel, _) = CreateChannel();
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            channel.ReadRegistersAsync("1~00001", 1, CancellationToken.None));
    }

    #endregion
}
