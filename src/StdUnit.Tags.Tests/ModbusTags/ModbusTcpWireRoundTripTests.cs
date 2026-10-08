using StdUnit.Tags.ModbusTcp;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Buffers.Binary;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StdUnit.Tags.Tests.ModbusTags;

/// <summary>
/// 钉住"线上字节 ↔ 通道返回/发送的内容"这一层（真实 socket + 真实 NModbus），
/// 它是所有端序语义的前提：<br/>
/// <br/>
/// Modbus 规范规定寄存器在线上是<b>大端</b>（高字节先）。NModbus 用
/// <c>ModbusUtility.NetworkBytesToHostUInt16</c>（内部是 <c>IPAddress.NetworkToHostOrder</c>）
/// 把这两个字节还原成一个 <b>数值</b>，所以 <c>ushort[]</c> 里的元素是"寄存器数值"——
/// 主机是小端（Intel）还是大端都不影响它；写方向的 <c>RegisterCollection.NetworkBytes</c> 也对称地
/// 用 <c>HostToNetworkOrder</c> 还原成线上字节。<br/>
/// <br/>
/// 换句话说：<b>主机端序在这一层被 NModbus 消化掉了</b>，上层只面对"数值"。只有当有人把
/// <c>ushort[]</c> 重新当成字节流去看（<c>MemoryMarshal.AsBytes</c> 之类）时，主机端序才会渗进来——
/// 本库没有任何这种操作。<br/>
/// <br/>
/// 这里刻意<b>手工构造线上字节</b>，而不是复用生产代码的序列化，避免"自己验自己"。
/// </summary>
public class ModbusTcpWireRoundTripTests
{
    private static ModbusTcpChannel CreateChannel(int port) =>
        new(new ModbusTcpTagChannelDescriptor { Name = "mb1", IpAddr = "127.0.0.1", Port = port },
            NullLogger<ModbusTcpChannel>.Instance);

    [Fact]
    public async Task HoldingRegisters_WireBigEndianBytes_ComeBackAsNumericValue()
    {
        // 线上 0x12 0x34 ⇒ 数值 0x1234
        using var server = new FakeModbusTcpServer();
        server.SetRegisters(0, 0x1234);
        using var channel = CreateChannel(server.Port);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var regs = await channel.ReadRegistersAsync("1~40001", 1, CancellationToken.None);

        Assert.Single(regs);
        Assert.Equal((ushort)0x1234, regs[0]);
    }

    [Fact]
    public async Task HoldingRegisters_MultipleRegisters_KeepWireOrder()
    {
        // 线上：第 1 个寄存器 12 34、第 2 个 56 78 ⇒ [0x1234, 0x5678]
        using var server = new FakeModbusTcpServer();
        server.SetRegisters(0, 0x1234, 0x5678);
        using var channel = CreateChannel(server.Port);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var regs = await channel.ReadRegistersAsync("1~40001", 2, CancellationToken.None);

        Assert.Equal(new ushort[] { 0x1234, 0x5678 }, regs);
    }

    [Fact]
    public async Task HoldingRegisters_Write_GoesOutAsBigEndianWireBytes()
    {
        using var server = new FakeModbusTcpServer();
        server.SetRegisters(0, 0x0000);
        using var channel = CreateChannel(server.Port);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        await channel.WriteRegistersAsync("1~40001", new ushort[] { 0x1234 }, CancellationToken.None);

        // 服务端按规范解出 0x1234，等价于线上是 12 34
        Assert.Equal(new ushort[] { 0x1234 }, server.GetRegisters(0, 1));
        Assert.Equal((ushort)0, server.LastRegistersWriteStart);
        Assert.Equal(new ushort[] { 0x1234 }, server.LastRegistersWritten);
    }

    [Fact]
    public async Task InputRegisters_Read_WorksTheSameWay()
    {
        using var server = new FakeModbusTcpServer();
        server.SetRegisters(0, 0xABCD);
        using var channel = CreateChannel(server.Port);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var regs = await channel.ReadRegistersAsync("1~30001", 1, CancellationToken.None);

        Assert.Equal((ushort)0xABCD, regs[0]);
    }

    [Fact]
    public async Task Coils_Read_AreLsbFirstWithinByte()
    {
        // 位在线上按 LSB 先填字节；这里让第 0/2 位为 1，NModbus 应还原成"逐位"的 bool[]
        using var server = new FakeModbusTcpServer();
        server.SetBits(0, true, false, true);
        using var channel = CreateChannel(server.Port);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var bits = await channel.ReadBitsAsync("1~00001", 3, CancellationToken.None);

        Assert.Equal(new bool[] { true, false, true }, bits);
    }

    [Fact]
    public async Task Coils_Write_RoundTripsThroughWirePacking()
    {
        using var server = new FakeModbusTcpServer();
        server.SetBits(0, false, false, false);
        using var channel = CreateChannel(server.Port);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        await channel.WriteBitsAsync("1~00001", new bool[] { true, false, true }, CancellationToken.None);

        Assert.Equal(new bool[] { true, false, true }, server.LastBitsWritten);
        Assert.Equal((ushort)0, server.LastBitsWriteStart);
    }
    /// <summary>
    /// 设备/线上的字节与上位机内存里 <c>ushort</c> 的字节布局：<b>顺序相反、数值相同</b>。<br/>
    /// 这条钉住"主机端序只在内存布局层面存在、不参与本库的语义"——拿 <c>byte[]</c> 去和设备端原样拷贝的字节
    /// 逐字节比较会得到"不一致"，但两者表达的是同一个数值，任何算术与协议编解码都不受影响。<br/>
    /// （断言与主机端序无关：<c>MemoryMarshal.AsBytes</c> 与 <c>BitConverter.GetBytes</c> 用的是同一套主机布局，
    /// <c>NetworkToHostOrder</c> 又把它还原成数值，所以在大小端主机上都成立。）
    /// </summary>
    [Fact]
    public async Task DeviceBytes_AndHostUshortMemoryLayout_AreReversedButSameValue()
    {
        // 设备端这个寄存器的字节是 12 34（规范规定的高字节在前）
        byte[] deviceBytes = { 0x12, 0x34 };

        using var server = new FakeModbusTcpServer();
        server.SetRegisters(0, 0x1234);
        using var channel = CreateChannel(server.Port);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var regs = await channel.ReadRegistersAsync("1~40001", 1, CancellationToken.None);
        var value = regs[0];

        // 上位机内存里这个 ushort 的字节布局（低地址在前）
        var hostBytes = BitConverter.GetBytes(value);
        // 把 ushort 序列原地当字节流看：得到"每个 ushort 按主机布局"拼接出的字节
        var asHostBytes = MemoryMarshal.AsBytes(regs.AsSpan()).ToArray();

        // 设备字节与主机内存字节：顺序相反
        Assert.Equal(deviceBytes[0], hostBytes[1]);
        Assert.Equal(deviceBytes[1], hostBytes[0]);
        Assert.Equal(asHostBytes, hostBytes);

        // 数值层面：两边一致
        Assert.Equal((ushort)0x1234, value);
        // 同一组主机字节按主机序读回、设备字节按大端读回，得到的都是同一个数值
        Assert.Equal(value, BitConverter.ToUInt16(hostBytes, 0));
        Assert.Equal(value, BinaryPrimitives.ReadUInt16BigEndian(deviceBytes));
        // 写方向对称：数值经"主机序 → 网络序"后的字节就是设备字节（NModbus 序列化就是这么做的）
        Assert.Equal(deviceBytes, BitConverter.GetBytes((ushort)IPAddress.HostToNetworkOrder((short)value)));
    }
}
