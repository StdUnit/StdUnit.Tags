using StdUnit.Tags.ModbusTcp;
using Microsoft.Extensions.Logging.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StdUnit.Tags.Tests.ModbusTags;

/// <summary>
/// 真实 <see cref="ModbusTcpChannel"/> 的连接生命周期（连到环回地址上的假服务端，真 socket）：
/// 已连接时 <c>EnsureConnectedAsync</c> 是空操作、断开后能按需重连、从未连接时断开/释放不报错。
/// </summary>
public class ModbusTcpChannelConnectionTests
{
    private static ModbusTcpChannel NewChannel(FakeModbusTcpServer server) => new(
        new ModbusTcpTagChannelDescriptor { Name = "mb1", IpAddr = "127.0.0.1", Port = server.Port },
        NullLogger<ModbusTcpChannel>.Instance);

    /// <summary>已连接时重复 EnsureConnected 复用同一个 master（不再建连接）</summary>
    [Fact]
    public async Task EnsureConnectedAsync_WhenAlreadyConnected_ReusesMaster()
    {
        using var server = new FakeModbusTcpServer();
        using var channel = NewChannel(server);

        await channel.EnsureConnectedAsync(false, CancellationToken.None);
        var master = channel.ModbusMaster;
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        Assert.NotNull(master);
        Assert.Same(master, channel.ModbusMaster);
    }

    /// <summary>断开后重新 EnsureConnected 会重建连接，读写照旧可用</summary>
    [Fact]
    public async Task DisconnectAsync_ThenEnsureConnected_CreatesNewConnection()
    {
        using var server = new FakeModbusTcpServer();
        server.SetRegisters(0, 0x1234);
        using var channel = NewChannel(server);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);
        var master = channel.ModbusMaster;

        await channel.DisconnectAsync(CancellationToken.None);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);
        var regs = await channel.ReadRegistersAsync("1~40001", 1, CancellationToken.None);

        Assert.NotSame(master, channel.ModbusMaster);
        Assert.Equal(new ushort[] { 0x1234 }, regs);
    }

    /// <summary>从未连接过就断开：直接完成，不抛错（之后仍能正常连接）</summary>
    [Fact]
    public async Task DisconnectAsync_WhenNeverConnected_Completes()
    {
        using var server = new FakeModbusTcpServer();
        server.SetRegisters(0, 0x5678);
        using var channel = NewChannel(server);

        await channel.DisconnectAsync(CancellationToken.None);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);
        var regs = await channel.ReadRegistersAsync("1~40001", 1, CancellationToken.None);

        Assert.Equal(new ushort[] { 0x5678 }, regs);
    }

    /// <summary>释放后能按需重连（Dispose 关掉底层 socket，但通道对象仍可用）</summary>
    [Fact]
    public async Task Dispose_ThenEnsureConnected_Reconnects()
    {
        using var server = new FakeModbusTcpServer();
        server.SetRegisters(0, 0x9ABC);
        var channel = NewChannel(server);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);
        var master = channel.ModbusMaster;

        channel.Dispose();
        await channel.EnsureConnectedAsync(false, CancellationToken.None);
        var regs = await channel.ReadRegistersAsync("1~40001", 1, CancellationToken.None);

        Assert.NotSame(master, channel.ModbusMaster);
        Assert.Equal(new ushort[] { 0x9ABC }, regs);
        channel.Dispose();
    }
}
