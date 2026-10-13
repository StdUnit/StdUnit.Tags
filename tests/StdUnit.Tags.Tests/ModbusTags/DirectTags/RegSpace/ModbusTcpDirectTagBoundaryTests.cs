using StdUnit.Tags;
using StdUnit.Tags.ModbusTcp;
using Moq;
using NModbus;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StdUnit.Tags.Tests.ModbusTags;

/// <summary>
/// 直接测点的边界：<c>endian</c> 记法不认识时必须报错（而不是按某种"默认"读出一个错值）、
/// 多寄存器数值的写路径（含地址解析缓存）。
/// </summary>
public class ModbusTcpDirectTagBoundaryTests
{
    private static (TestModbusTcpChannel Channel, Mock<IModbusMaster> Mock, TagContainer Container) CreateChannel()
    {
        var mock = new Mock<IModbusMaster>(MockBehavior.Strict);
        var channel = new TestModbusTcpChannel(new ModbusTcpTagChannelDescriptor { Name = "mb1" }, mock);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp", IsEntry = true }, channel);
        return (channel, mock, TagContainer.From(grp));
    }

    private static TagDescriptor Descriptor(string kind, int tagSize, EndianKinds endian) => new()
    {
        TagName = "v",
        RawAddress = "1~40001",
        TagKind = kind,
        TagSize = tagSize,
        EndianKind = endian,
    };

    /// <summary>枚举里没有的字节序取值（如手写配置解析出界）</summary>
    private const EndianKinds UnknownEndian = (EndianKinds)99;

    /// <summary><c>endian</c> 不是 BigEndian / LittleEndian 时读写都点名报错</summary>
    [Theory]
    [InlineData(BuiltinTagKinds.INT16, 2)]
    [InlineData(BuiltinTagKinds.UINT16, 2)]
    public async Task Read_WithUnknownEndianKind_Throws(string kind, int tagSize)
    {
        var (channel, mock, container) = CreateChannel();
        mock
            .Setup(x => x.ReadHoldingRegistersAsync(1, (ushort)0, (ushort)1))
            .ReturnsAsync(new ushort[] { 0x1234 });
        await channel.EnsureConnectedAsync(false, CancellationToken.None);
        var descriptor = Descriptor(kind, tagSize, UnknownEndian);

        var tag = new ModbusTcpDirectTagFactory(container).Create(descriptor, channel);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => tag.ReadAsync(CancellationToken.None));
        Assert.Contains("99", ex.Message);
    }

    /// <summary>16 位写路径同样拒绝不认识的 <c>endian</c></summary>
    [Fact]
    public async Task Write_WithUnknownEndianKind_Throws()
    {
        var (channel, _, container) = CreateChannel();
        await channel.EnsureConnectedAsync(false, CancellationToken.None);
        var tag = new ModbusTcpDirectTagFactory(container).Create(
            Descriptor(BuiltinTagKinds.INT16, 2, UnknownEndian),
            channel);

        tag.Value = (short)1;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => tag.WriteAsync(CancellationToken.None));
        Assert.Contains("99", ex.Message);
    }

    /// <summary>64 位写：一次写 4 个寄存器；重复写复用已解析的地址（走 <c>GetAddress</c> 的缓存分支）</summary>
    [Fact]
    public async Task UInt64DirectTag_WriteAsync_SendsFourRegistersTwice()
    {
        var (channel, mock, container) = CreateChannel();
        var sent = new System.Collections.Generic.List<ushort[]>();
        mock
            .Setup(x => x.WriteMultipleRegistersAsync(1, (ushort)0, It.IsAny<ushort[]>()))
            .Returns(Task.CompletedTask)
            .Callback<byte, ushort, ushort[]>((_, _, data) => sent.Add(data));
        await channel.EnsureConnectedAsync(false, CancellationToken.None);
        var tag = new ModbusTcpDirectTagFactory(container).Create(
            Descriptor(BuiltinTagKinds.UINT64, 8, EndianKinds.BigEndian),
            channel);
        tag.Value = 0x0102030405060708UL;

        await tag.WriteAsync(CancellationToken.None);
        await tag.WriteAsync(CancellationToken.None);

        Assert.Equal(2, sent.Count);
        Assert.Equal(new ushort[] { 0x0102, 0x0304, 0x0506, 0x0708 }, sent[0]);
    }
}
