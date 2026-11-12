using StdUnit.Tags.ModbusTcp;
using StdUnit.Tags.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NModbus;
using System;
using System.Net.Sockets;
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

    /// <summary>
    /// <c>Dispose()</c> 与 <c>DisconnectAsync()</c> 是同一条释放路径：不拿 <c>Connected</c> 当守卫、
    /// 释放后把 master 一并清空，且重复释放幂等。
    /// </summary>
    [Fact]
    public async Task Dispose_ReleasesConnectionAndIsIdempotent()
    {
        using var server = new FakeModbusTcpServer();
        using var channel = NewChannel(server);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);
        Assert.NotNull(channel.ModbusMaster);

        channel.Dispose();

        Assert.Null(channel.ModbusMaster);
        channel.Dispose();
        channel.Dispose();
    }

    /// <summary>
    /// 底层 <see cref="TcpClient.Close"/> 抛异常时，显式的 <c>DisconnectAsync</c> 必须以故障 Task 报告
    /// （调用方要看得到"这次断开失败了"），并且连接引用照样被清掉。
    /// </summary>
    [Fact]
    public async Task DisconnectAsync_WhenCloseThrows_ReturnsFaultedTask()
    {
        var channel = NewThrowingChannel(NullLogger<ModbusTcpChannel>.Instance);
        channel.TcpClient = new ThrowingTcpClient();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => channel.DisconnectAsync(CancellationToken.None));

        Assert.Equal("close failed", ex.Message);
        Assert.Null(channel.TcpClient);
    }

    /// <summary>
    /// 同一次失败落在 <c>Dispose()</c> 上时只留痕不外抛（.NET 的释放惯例）。
    /// </summary>
    [Fact]
    public void Dispose_WhenCloseThrows_ShouldNotThrowButLog()
    {
        var logs = new CapturingLoggerProvider();
        using var factory = LoggerFactory.Create(b => b.AddProvider(logs));
        var channel = NewThrowingChannel(factory.CreateLogger<ModbusTcpChannel>());
        channel.TcpClient = new ThrowingTcpClient();

        channel.Dispose();

        Assert.Null(channel.TcpClient);
        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("释放异常"));
    }

    private static TestModbusTcpChannel NewThrowingChannel(ILogger<ModbusTcpChannel> logger) =>
        new(
            new ModbusTcpTagChannelDescriptor { Name = "mb-boom", IpAddr = "127.0.0.1", Port = 1 },
            new Mock<IModbusMaster>(),
            logger);
}

/// <summary>
/// 关闭即抛的 <see cref="TcpClient"/>：真实 socket 的 <c>Close()</c> 几乎不会失败，
/// 只能用替身构造"关闭失败"这条路径（<c>Dispose(bool)</c> 是 protected virtual）。
/// </summary>
internal sealed class ThrowingTcpClient : TcpClient
{
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            throw new InvalidOperationException("close failed");
        }

        // finalizer 走这里：绝不能在终结器线程上抛，否则整个进程都会挂掉。
        base.Dispose(false);
    }
}
