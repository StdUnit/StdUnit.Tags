using StdUnit.Tags.S7;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StdUnit.Sharp7;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StdUnit.Tags.Tests.S7Tags;

public class S7TagChannelTests
{
    /// <summary>
    /// 构建 S7TagChannel 和 MockS7Client
    /// </summary>
    private static (S7TagChannel Channel, MockS7Client Mock) CreateChannel(
        bool connected = false,
        int connectToResult = 0,
        int dbReadResult = 0,
        int dbWriteResult = 0,
        int mbReadResult = 0,
        int mbWriteResult = 0,
        byte[]? dbReadBuffer = null,
        byte[]? mbReadBuffer = null,
        bool isDead = false)
    {
        var channel = new S7TagChannel(new S7TagChannelDescriptor { Name = "test-channel" }, NullLogger<S7TagChannel>.Instance);
        var mock = new MockS7Client
        {
            ConnectedValue = connected,
            ConnectToResult = connectToResult,
            DBReadResult = dbReadResult,
            DBWriteResult = dbWriteResult,
            MBReadResult = mbReadResult,
            MBWriteResult = mbWriteResult,
            DBReadBuffer = dbReadBuffer ?? [],
            MBReadBuffer = mbReadBuffer ?? [],
            IsDeadResult = isDead,
        };
        channel.ClientFactory = _ => mock; // 注入 MockS7Client
        return (channel, mock);
    }

    #region 构造函数

    [Fact]
    public void Constructor_ShouldSetProperties()
    {
        var (channel, _) = CreateChannel();
        Assert.Equal("test-channel", channel.ChannelName());
        Assert.NotNull(channel.PlcItem);
        Assert.Equal(S7Names.DriverName, channel.Driver());
    }

    [Fact]
    public void Constructor_WithoutFactory_ShouldUseDefault()
    {
        var plc = new S7PlcItem();
        var logger = NullLogger<S7TagChannel>.Instance;
        var channel = new S7TagChannel(new S7TagChannelDescriptor() { Name = "s7-ch" }, logger);
        Assert.Equal("s7-ch", channel.ChannelName());
    }

    #endregion

    #region EnsureConnectedAsync

    [Fact]
    public async Task EnsureConnectedAsync_NotForced_ClientAlive_ShouldSkip()
    {
        var (channel, mock) = CreateChannel(connected: true, isDead: false);
        // set a non-null client to simulate already connected
        channel.Client = mock;

        await channel.EnsureConnectedAsync(force: false, CancellationToken.None);

        Assert.Equal(0, mock.ConnectToCallCount);
        Assert.Equal(0, mock.DisconnectCallCount);
    }

    [Fact]
    public async Task EnsureConnectedAsync_NotForced_ClientDead_ShouldReconnect()
    {
        var (channel, mock) = CreateChannel(connected: true, isDead: true);

        await channel.EnsureConnectedAsync(force: false, CancellationToken.None);

        Assert.Equal(1, mock.ConnectToCallCount);
        Assert.Equal(1, mock.SetConnectionTypeCallCount);
    }

    [Fact]
    public async Task EnsureConnectedAsync_NotForced_ClientNull_ShouldConnect()
    {
        var (channel, mock) = CreateChannel(connected: false);

        await channel.EnsureConnectedAsync(force: false, CancellationToken.None);

        Assert.Equal(1, mock.ConnectToCallCount);
        Assert.Equal(1, mock.SetConnectionTypeCallCount);
    }

    [Fact]
    public async Task EnsureConnectedAsync_Forced_ClientExists_ShouldDisconnectThenConnect()
    {
        var (channel, mock) = CreateChannel(connected: true);
        channel.Client = mock; // simulate existing client

        await channel.EnsureConnectedAsync(force: true, CancellationToken.None);

        Assert.Equal(1, mock.DisconnectCallCount);
        Assert.Equal(1, mock.ConnectToCallCount);
    }

    [Fact]
    public async Task EnsureConnectedAsync_Forced_ClientNull_ShouldConnect()
    {
        var (channel, mock) = CreateChannel(connected: false);

        await channel.EnsureConnectedAsync(force: true, CancellationToken.None);

        Assert.Equal(1, mock.ConnectToCallCount);
        Assert.Equal(0, mock.DisconnectCallCount);
    }

    [Fact]
    public async Task EnsureConnectedAsync_ConnectFails_ShouldThrow()
    {
        var (channel, mock) = CreateChannel(connectToResult: -1); // non-zero = error

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => channel.EnsureConnectedAsync(force: false, CancellationToken.None));
        Assert.Contains("test-channel", ex.Message);
    }

    [Fact]
    public async Task EnsureConnectedAsync_ShouldSetClientOnSuccess()
    {
        var (channel, mock) = CreateChannel(connectToResult: 0);

        await channel.EnsureConnectedAsync(force: false, CancellationToken.None);

        Assert.Same(mock, channel.Client);
    }

    #endregion

    #region DisconnectAsync

    [Fact]
    public async Task DisconnectAsync_WhenClientNull_ShouldBeNoOp()
    {
        var (channel, mock) = CreateChannel();

        await channel.DisconnectAsync(CancellationToken.None);

        Assert.Equal(0, mock.DisconnectCallCount);
    }

    [Fact]
    public async Task DisconnectAsync_WhenClientExists_ShouldDisconnectAndClear()
    {
        var (channel, mock) = CreateChannel(connected: true);
        channel.Client = mock;

        await channel.DisconnectAsync(CancellationToken.None);

        Assert.Equal(1, mock.DisconnectCallCount);
        Assert.Null(channel.Client);
    }

    [Fact]
    public async Task DisconnectAsync_WhenDisconnectThrows_ShouldStillClearClient()
    {
        var (channel, mock) = CreateChannel(connected: true);
        channel.Client = mock;
        mock.DisconnectAction = () => throw new InvalidOperationException("disconnect failed");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => channel.DisconnectAsync(CancellationToken.None));

        Assert.Equal("disconnect failed", ex.Message);
        // Client 被清空即使断开操作抛出异常
        Assert.Null(channel.Client);
    }

    #endregion

    #region ReadAsync - DB Area

    [Fact]
    public async Task ReadAsync_DBArea_ShouldReturnBytes()
    {
        var expected = new byte[] { 0x01, 0x02, 0x03 };
        var (channel, mock) = CreateChannel(dbReadResult: 0, dbReadBuffer: expected);
        channel.Client = mock;

        var result = await channel.ReadAsync("DB1.100", 3, CancellationToken.None);

        Assert.Equal(expected, result);
        Assert.Equal(1, mock.DBReadCallCount);
    }

    [Fact]
    public async Task ReadAsync_DBArea_ClientNull_ShouldThrow()
    {
        var (channel, _) = CreateChannel();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => channel.ReadAsync("DB1.100", 3, CancellationToken.None));
    }

    [Fact]
    public async Task ReadAsync_DBArea_ErrorCode_ShouldThrow()
    {
        var (channel, mock) = CreateChannel(dbReadResult: -2);
        channel.Client = mock;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => channel.ReadAsync("DB1.100", 3, CancellationToken.None));
        Assert.Contains("Unknown error", ex.Message);
    }

    #endregion

    #region ReadAsync - MB Area

    [Fact]
    public async Task ReadAsync_MBArea_ShouldReturnBytes()
    {
        var expected = new byte[] { 0x0A, 0x0B };
        var (channel, mock) = CreateChannel(mbReadResult: 0, mbReadBuffer: expected);
        channel.Client = mock;

        var result = await channel.ReadAsync("MB.100", 2, CancellationToken.None);

        Assert.Equal(expected, result);
        Assert.Equal(1, mock.MBReadCallCount);
    }

    [Fact]
    public async Task ReadAsync_MBArea_ErrorCode_ShouldThrow()
    {
        var (channel, mock) = CreateChannel(mbReadResult: -3);
        channel.Client = mock;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => channel.ReadAsync("MB.100", 2, CancellationToken.None));
    }

    #endregion


    #region WriteAsync - DB Area

    [Fact]
    public async Task WriteAsync_DBArea_ShouldWriteBytes()
    {
        var (channel, mock) = CreateChannel(dbWriteResult: 0);
        channel.Client = mock;
        var data = new byte[] { 0x10, 0x20 };

        await channel.WriteAsync("DB1.200", data, CancellationToken.None);

        Assert.Equal(1, mock.DBWriteCallCount);
    }

    [Fact]
    public async Task WriteAsync_DBArea_ClientNull_ShouldThrow()
    {
        var (channel, _) = CreateChannel();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => channel.WriteAsync("DB1.200", [0x10], CancellationToken.None));
    }

    [Fact]
    public async Task WriteAsync_DBArea_ErrorCode_ShouldThrow()
    {
        var (channel, mock) = CreateChannel(dbWriteResult: -4);
        channel.Client = mock;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => channel.WriteAsync("DB1.200", [0x10], CancellationToken.None));
    }

    #endregion

    #region WriteAsync - MB Area

    [Fact]
    public async Task WriteAsync_MBArea_ShouldWriteBytes()
    {
        var (channel, mock) = CreateChannel(mbWriteResult: 0);
        channel.Client = mock;

        await channel.WriteAsync("MB.200", [0x30], CancellationToken.None);

        Assert.Equal(1, mock.MBWriteCallCount);
    }

    [Fact]
    public async Task WriteAsync_MBArea_ErrorCode_ShouldThrow()
    {
        var (channel, mock) = CreateChannel(mbWriteResult: -5);
        channel.Client = mock;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => channel.WriteAsync("MB.200", [0x30], CancellationToken.None));
    }

    #endregion



    #region Dispose

    [Fact]
    public void Dispose_WhenClientConnected_ShouldDisconnect()
    {
        var (channel, mock) = CreateChannel(connected: true);
        channel.Client = mock;

        channel.Dispose();

        Assert.Equal(1, mock.DisconnectCallCount);
        Assert.Null(channel.Client);
    }

    [Fact]
    public void Dispose_WhenClientNotConnected_ShouldNotDisconnect()
    {
        var (channel, mock) = CreateChannel(connected: false);
        channel.Client = mock;

        channel.Dispose();

        Assert.Equal(0, mock.DisconnectCallCount);
        Assert.Null(channel.Client);
    }

    [Fact]
    public void Dispose_WhenClientNull_ShouldNotThrow()
    {
        var (channel, _) = CreateChannel();

        // should not throw
        channel.Dispose();
    }

    [Fact]
    public void Dispose_WhenDisconnectThrows_ShouldNotThrow()
    {
        var (channel, mock) = CreateChannel(connected: true);
        channel.Client = mock;
        mock.DisconnectAction = () => throw new InvalidOperationException("fail");

        // exception is caught and logged
        channel.Dispose();

        Assert.Null(channel.Client);
    }

    #endregion

    #region Thread Safety

    [Fact]
    public async Task ConcurrentOperations_ShouldBeSerialized()
    {
        var (channel, mock) = CreateChannel(connected: true);
        channel.Client = mock;

        int concurrent = 0;
        int maxConcurrent = 0;
        mock.DBReadAction = () =>
        {
            Interlocked.Increment(ref concurrent);
            // capture max concurrent
            var current = Volatile.Read(ref concurrent);
            var max = Volatile.Read(ref maxConcurrent);
            while (current > max)
            {
                Interlocked.CompareExchange(ref maxConcurrent, current, max);
                max = Volatile.Read(ref maxConcurrent);
                current = Volatile.Read(ref concurrent);
            }
            Thread.Sleep(50);
            Interlocked.Decrement(ref concurrent);
        };

        var t1 = channel.ReadAsync("DB1.100", 1, CancellationToken.None);
        var t2 = channel.ReadAsync("DB1.100", 1, CancellationToken.None);

        await Task.WhenAll(t1, t2);

        // SemaphoreSlim serializes access, so max concurrent should be 1
        Assert.Equal(1, maxConcurrent);
    }

    #endregion
}

/// <summary>
/// 继承 S7Client 并覆写虚方法，用于 S7TagChannel 单元测试
/// </summary>
internal class MockS7Client : S7Client
{
    public bool ConnectedValue { get; set; }
    public bool IsDeadResult { get; set; }
    public int ConnectToResult { get; set; }
    public int SetConnectionTypeResult { get; set; }
    public int DBReadResult { get; set; }
    public int DBWriteResult { get; set; }
    public int MBReadResult { get; set; }
    public int MBWriteResult { get; set; }
    public byte[] DBReadBuffer { get; set; } = [];
    public byte[] MBReadBuffer { get; set; } = [];

    public Action? DisconnectAction { get; set; }
    public Action? DBReadAction { get; set; }

    public int ConnectToCallCount { get; private set; }
    public int SetConnectionTypeCallCount { get; private set; }
    public int DisconnectCallCount { get; private set; }
    public int DBReadCallCount { get; private set; }
    public int DBWriteCallCount { get; private set; }
    public int MBReadCallCount { get; private set; }
    public int MBWriteCallCount { get; private set; }

    public override bool Connected => ConnectedValue;

    public override int ConnectTo(string address, int rack, int slot)
    {
        ConnectToCallCount++;
        return ConnectToResult;
    }

    public override int SetConnectionType(ushort connectionType)
    {
        SetConnectionTypeCallCount++;
        return SetConnectionTypeResult;
    }

    public override int Disconnect()
    {
        DisconnectCallCount++;
        var action = DisconnectAction;
        DisconnectAction = null; // 防止 finalizer 意外重入
        action?.Invoke();
        return 0;
    }

    public override bool IsDead(int microseconds = 0) => IsDeadResult;

    public override int DBRead(int dbNumber, int start, int size, byte[] buffer)
    {
        DBReadCallCount++;
        DBReadAction?.Invoke();
        if (DBReadBuffer.Length > 0)
        {
            var copyLen = Math.Min(size, DBReadBuffer.Length);
            Array.Copy(DBReadBuffer, 0, buffer, 0, copyLen);
        }
        return DBReadResult;
    }

    public override int DBWrite(int dbNumber, int start, int size, byte[] buffer)
    {
        DBWriteCallCount++;
        return DBWriteResult;
    }

    public override int MBRead(int start, int size, byte[] buffer)
    {
        MBReadCallCount++;
        if (MBReadBuffer.Length > 0)
        {
            var copyLen = Math.Min(size, MBReadBuffer.Length);
            Array.Copy(MBReadBuffer, 0, buffer, 0, copyLen);
        }
        return MBReadResult;
    }

    public override int MBWrite(int start, int size, byte[] buffer)
    {
        MBWriteCallCount++;
        return MBWriteResult;
    }
}
