using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using StdUnit.Tags.ModbusTcp.Compat;
using Xunit;

namespace StdUnit.Tags.Tests.ModbusTags.Compat;

/// <summary>
/// <see cref="TcpConnectionCompat"/> 的测试。<br/>
/// <br/>
/// 这个兼容类的存在意义就是「让 net472 也能取消建连」，
/// 所以除了成功路径，取消路径也要覆盖——否则两个框架的取消语义分叉不会被发现。
/// <br/>
/// 取消用例使用**已取消的 token**（而不是「连一个会挂住的地址再取消」）：
/// net472 分支在 <c>ct.Register</c> 时会同步触发回调关掉 socket，
/// net8.0 分支会立即返回已取消的 Task——两者都是确定性的，不会引入超时抖动。
/// </summary>
public class TcpConnectionCompatTests
{
    /// <summary>启动一个监听 127.0.0.1 随机端口的 listener。</summary>
    private static TcpListener StartListener(out int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return listener;
    }

    [Fact]
    public async Task ConnectAsync_ToReachableEndpoint_Completes()
    {
        var listener = StartListener(out var port);
        try
        {
            using var client = new TcpClient();

            await TcpConnectionCompat.ConnectAsync(client, IPAddress.Loopback.ToString(), port, CancellationToken.None);

            Assert.True(client.Connected);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task ConnectAsync_ThenAccept_EstablishesBothSides()
    {
        var listener = StartListener(out var port);
        try
        {
            using var client = new TcpClient();
            await TcpConnectionCompat.ConnectAsync(client, IPAddress.Loopback.ToString(), port, CancellationToken.None);

            using var accepted = await listener.AcceptTcpClientAsync();

            Assert.True(accepted.Connected);
            // 建连后应能真正收发数据（证明不是「看起来连上」）
            using var stream = client.GetStream();
            var payload = new byte[] { 0x42 };
            await stream.WriteAsync(payload, 0, payload.Length);

            var buffer = new byte[1];
            var read = await accepted.GetStream().ReadAsync(buffer, 0, 1);
            Assert.Equal(1, read);
            Assert.Equal(0x42, buffer[0]);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task ConnectAsync_WhenTokenAlreadyCancelled_ThrowsOperationCanceled()
    {
        // 目标地址可达与否都不重要：token 已取消，必须在建立/等待连接之前就放弃。
        var listener = StartListener(out var port);
        try
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => TcpConnectionCompat.ConnectAsync(client, IPAddress.Loopback.ToString(), port, cts.Token));
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task ConnectAsync_UnreachableEndpoint_Throws()
    {
        // 未监听的端口：连接被拒绝，必须把 SocketException 原样暴露给调用方
        // （不能被兼容层「吞掉」或误包装成取消异常）
        var listener = StartListener(out var port);
        listener.Stop(); // 立刻释放，确保端口无人监听

        using var client = new TcpClient();

        await Assert.ThrowsAsync<SocketException>(
            () => TcpConnectionCompat.ConnectAsync(client, IPAddress.Loopback.ToString(), port, CancellationToken.None));
    }

    [Fact]
    public async Task ConnectAsync_WhenCancelled_DoesNotThrowCancellationOnSuccessPath()
    {
        // token 未取消时，成功路径不应受到 Register/回调机制的任何干扰
        var listener = StartListener(out var port);
        try
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource();

            await TcpConnectionCompat.ConnectAsync(client, IPAddress.Loopback.ToString(), port, cts.Token);

            Assert.True(client.Connected);
        }
        finally
        {
            listener.Stop();
        }
    }
}
