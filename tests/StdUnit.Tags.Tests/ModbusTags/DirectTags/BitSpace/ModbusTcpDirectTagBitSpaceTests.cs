using System;
using System.Threading;
using System.Threading.Tasks;
using StdUnit.Tags.ModbusTcp;
using Moq;
using NModbus;
using Xunit;

namespace StdUnit.Tags.Tests.ModbusTags;

/// <summary>
/// 位空间里的两个直接测点：<see cref="OutputCoilDirectTag"/>（线圈 0x，可读写）
/// 与 <see cref="InputContactDirectTag"/>（离散输入 1x，只读）。<br/>
/// <br/>
/// 通过 <see cref="TestModbusTcpChannel"/> 走真实通道层 <see cref="ModbusTcpChannel.ReadAsync"/> /
/// <see cref="ModbusTcpChannel.WriteAsync"/>，底层用 Moq 的 <see cref="IModbusMaster"/> 模拟。
/// 这样地址解析、分批、位数组读写都被真实执行。
/// </summary>
public class ModbusTcpDirectTagBitSpaceTests
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

    #region OutputCoilDirectTag

    [Fact]
    public async Task OutputCoil_ReadAsync_CoilOn()
    {
        var (channel, mock) = CreateChannel();
        var container = CreateContainer(channel);
        var descriptor = new TagDescriptor
        {
            TagName = "do-v",
            RawAddress = "1~00001",
            TagKind = BuiltinTagKinds.DO,
        };

        mock
            .Setup(x => x.ReadCoilsAsync(1, (ushort)0, (ushort)1))
            .ReturnsAsync(new bool[] { true });
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new OutputCoilDirectTag(descriptor, channel, container);
        await tag.ReadAsync(CancellationToken.None);

        Assert.True(tag.Value);
    }

    [Fact]
    public async Task OutputCoil_ReadAsync_CoilOff()
    {
        var (channel, mock) = CreateChannel();
        var container = CreateContainer(channel);
        var descriptor = new TagDescriptor
        {
            TagName = "do-v",
            RawAddress = "1~00001",
            TagKind = BuiltinTagKinds.DO,
        };

        mock
            .Setup(x => x.ReadCoilsAsync(1, (ushort)0, (ushort)1))
            .ReturnsAsync(new bool[] { false });
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new OutputCoilDirectTag(descriptor, channel, container);
        await tag.ReadAsync(CancellationToken.None);

        Assert.False(tag.Value);
    }

    [Fact]
    public async Task OutputCoil_WriteAsync_WritesCoil()
    {
        var (channel, mock) = CreateChannel();
        var container = CreateContainer(channel);
        var descriptor = new TagDescriptor
        {
            TagName = "do-v",
            RawAddress = "1~00001",
            TagKind = BuiltinTagKinds.DO,
        };

        bool[]? written = null;
        mock
            .Setup(x => x.WriteMultipleCoilsAsync(1, (ushort)0, It.IsAny<bool[]>()))
            .Returns(Task.CompletedTask)
            .Callback<byte, ushort, bool[]>((_, _, data) => written = data);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new OutputCoilDirectTag(descriptor, channel, container)
        {
            Value = true,
        };
        await tag.WriteAsync(CancellationToken.None);

        Assert.NotNull(written);
        Assert.Equal(new bool[] { true }, written);
        Assert.False(tag.IsDirty);
    }

    #endregion

    #region InputContactDirectTag

    [Fact]
    public async Task InputContact_ReadAsync_InputOn()
    {
        var (channel, mock) = CreateChannel();
        var container = CreateContainer(channel);
        var descriptor = new TagDescriptor
        {
            TagName = "di-v",
            RawAddress = "1~10001",
            TagKind = BuiltinTagKinds.DI,
        };

        mock
            .Setup(x => x.ReadInputsAsync(1, (ushort)0, (ushort)1))
            .ReturnsAsync(new bool[] { true });
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new InputContactDirectTag(descriptor, channel, container);
        await tag.ReadAsync(CancellationToken.None);

        Assert.True(tag.Value);
    }

    [Fact]
    public async Task InputContact_ReadAsync_InputOff()
    {
        var (channel, mock) = CreateChannel();
        var container = CreateContainer(channel);
        var descriptor = new TagDescriptor
        {
            TagName = "di-v",
            RawAddress = "1~10001",
            TagKind = BuiltinTagKinds.DI,
        };

        mock
            .Setup(x => x.ReadInputsAsync(1, (ushort)0, (ushort)1))
            .ReturnsAsync(new bool[] { false });
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new InputContactDirectTag(descriptor, channel, container);
        await tag.ReadAsync(CancellationToken.None);

        Assert.False(tag.Value);
    }

    [Fact]
    public async Task InputContact_WriteAsync_ThrowsNotSupported()
    {
        var (channel, mock) = CreateChannel();
        var container = CreateContainer(channel);
        var descriptor = new TagDescriptor
        {
            TagName = "di-v",
            RawAddress = "1~10001",
            TagKind = BuiltinTagKinds.DI,
        };

        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var tag = new InputContactDirectTag(descriptor, channel, container);
        await Assert.ThrowsAsync<NotSupportedException>(() => tag.WriteAsync(CancellationToken.None));
    }

    #endregion
}
