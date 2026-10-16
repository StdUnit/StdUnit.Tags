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
        // 连接失败必须能定位到"哪个通道、连哪个端点、底层错误码"
        Assert.Contains("test-channel", ex.Message);
        Assert.Contains("连接PLC失败", ex.Message);
        Assert.Contains("Rack=", ex.Message);
        Assert.Contains("OsSockerError=", ex.Message);
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

    #region Read - DB Area

    [Fact]
    public void Read_DBArea_ShouldReturnBytes()
    {
        var expected = new byte[] { 0x01, 0x02, 0x03 };
        var (channel, mock) = CreateChannel(dbReadResult: 0, dbReadBuffer: expected);
        channel.Client = mock;

        var buffer = new byte[3];
        channel.Read("DB1.100", buffer, CancellationToken.None);

        Assert.Equal(expected, buffer);
        Assert.Equal(1, mock.DBReadCallCount);
    }

    [Fact]
    public void Read_DBArea_ClientNull_ShouldThrow()
    {
        var (channel, _) = CreateChannel();

        var ex = Assert.Throws<InvalidOperationException>(
            () => channel.Read("DB1.100", new byte[3], CancellationToken.None));
        // 多通道入口里，消息必须能定位到通道与地址
        Assert.Contains("test-channel", ex.Message);
        Assert.Contains("DB1.100", ex.Message);
    }

    [Fact]
    public void Read_DBArea_ErrorCode_ShouldThrow()
    {
        var (channel, mock) = CreateChannel(dbReadResult: -2);
        channel.Client = mock;

        var ex = Assert.Throws<InvalidOperationException>(
            () => channel.Read("DB1.100", new byte[3], CancellationToken.None));
        Assert.Contains("Unknown error", ex.Message);
        // 通道名 + 操作 + 地址 + 长度 + 错误码明细，缺一不可（否则轮询日志里定位不到现场）
        Assert.Contains("test-channel", ex.Message);
        Assert.Contains("读取失败", ex.Message);
        Assert.Contains("DB1.100", ex.Message);
        Assert.Contains("长度=3", ex.Message);
        Assert.Contains("OsSockerError=", ex.Message);
    }

    #endregion

    #region Read - MB Area

    [Fact]
    public void Read_MBArea_ShouldReturnBytes()
    {
        var expected = new byte[] { 0x0A, 0x0B };
        var (channel, mock) = CreateChannel(mbReadResult: 0, mbReadBuffer: expected);
        channel.Client = mock;

        var buffer = new byte[2];
        channel.Read("MB.100", buffer, CancellationToken.None);

        Assert.Equal(expected, buffer);
        Assert.Equal(1, mock.MBReadCallCount);
    }

    [Fact]
    public void Read_MBArea_ErrorCode_ShouldThrow()
    {
        var (channel, mock) = CreateChannel(mbReadResult: -3);
        channel.Client = mock;

        var ex = Assert.Throws<InvalidOperationException>(
            () => channel.Read("MB.100", new byte[2], CancellationToken.None));
        Assert.Contains("test-channel", ex.Message);
        Assert.Contains("读取失败", ex.Message);
        Assert.Contains("MB.100", ex.Message);
        Assert.Contains("长度=2", ex.Message);
    }

    #endregion


    #region Write - DB Area

    [Fact]
    public void Write_DBArea_ShouldWriteBytes()
    {
        var (channel, mock) = CreateChannel(dbWriteResult: 0);
        channel.Client = mock;
        var data = new byte[] { 0x10, 0x20 };

        channel.Write("DB1.200", data, CancellationToken.None);

        Assert.Equal(1, mock.DBWriteCallCount);
    }

    [Fact]
    public void Write_DBArea_ClientNull_ShouldThrow()
    {
        var (channel, _) = CreateChannel();

        var ex = Assert.Throws<InvalidOperationException>(
            () => channel.Write("DB1.200", new byte[] { 0x10 }, CancellationToken.None));
        Assert.Contains("test-channel", ex.Message);
        Assert.Contains("DB1.200", ex.Message);
    }

    [Fact]
    public void Write_DBArea_ErrorCode_ShouldThrow()
    {
        var (channel, mock) = CreateChannel(dbWriteResult: -4);
        channel.Client = mock;

        var ex = Assert.Throws<InvalidOperationException>(
            () => channel.Write("DB1.200", new byte[] { 0x10 }, CancellationToken.None));
        Assert.Contains("test-channel", ex.Message);
        Assert.Contains("写入失败", ex.Message);
        Assert.Contains("DB1.200", ex.Message);
        Assert.Contains("长度=1", ex.Message);
    }

    #endregion

    #region Write - MB Area

    [Fact]
    public void Write_MBArea_ShouldWriteBytes()
    {
        var (channel, mock) = CreateChannel(mbWriteResult: 0);
        channel.Client = mock;

        channel.Write("MB.200", new byte[] { 0x30 }, CancellationToken.None);

        Assert.Equal(1, mock.MBWriteCallCount);
    }

    [Fact]
    public void Write_MBArea_ErrorCode_ShouldThrow()
    {
        var (channel, mock) = CreateChannel(mbWriteResult: -5);
        channel.Client = mock;

        var ex = Assert.Throws<InvalidOperationException>(
            () => channel.Write("MB.200", new byte[] { 0x30 }, CancellationToken.None));
        Assert.Contains("test-channel", ex.Message);
        Assert.Contains("写入失败", ex.Message);
        Assert.Contains("MB.200", ex.Message);
        Assert.Contains("长度=1", ex.Message);
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

        var b1 = new byte[1];
        var b2 = new byte[1];
        var t1 = Task.Run(() => channel.Read("DB1.100", b1, CancellationToken.None));
        var t2 = Task.Run(() => channel.Read("DB1.100", b2, CancellationToken.None));

        await Task.WhenAll(t1, t2);

        // SemaphoreSlim serializes access, so max concurrent should be 1
        Assert.Equal(1, maxConcurrent);
    }

    [Fact]
    public async Task Read_WhenIoMutexBusyAndTokenCancelled_ShouldThrowOperationCanceled()
    {
        var (channel, mock) = CreateChannel(dbReadResult: 0);
        channel.Client = mock;

        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        mock.DBReadAction = () =>
        {
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(30)); // 兜底：测试失败时也不至于把整个测试进程挂死
        };

        // 第一次读取进到底层后会一直持有 I/O 互斥体
        var holding = Task.Run(() => channel.Read("DB1.100", new byte[1], CancellationToken.None));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)), "第一次读取没能进入底层");

        try
        {
            // 等待互斥体必须能被取消：底层 S7 调用是同步阻塞的，交给它的 ct 只是"等锁"时的逃生口
            using (var readCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)))
            {
                var waiting = Task.Run(() => channel.Read("DB1.100", new byte[1], readCts.Token));
                await AssertCancelledWithin(waiting);
            }

            using (var writeCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)))
            {
                var writing = Task.Run(() => channel.Write("DB1.200", new byte[1], writeCts.Token));
                await AssertCancelledWithin(writing);
            }
        }
        finally
        {
            release.Set();
        }

        await holding;

        // 5 秒内就该拿到 OperationCanceledException。
        // 若 Read/Write 退化成不可取消的 Wait()，它会一直等到 finally 放开互斥体才结束，
        // 于是先完成的是 Task.Delay ⇒ Assert.Same 失败，而不是靠"等到底"误判成通过。
        static async Task AssertCancelledWithin(Task awaited)
        {
            var winner = await Task.WhenAny(awaited, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(awaited, winner);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => awaited);
        }
    }

    #endregion

    #region 不支持的地址区域

    [Fact]
    public void Read_UnsupportedArea_ShouldThrowNotImplemented()
    {
        var (channel, mock) = CreateChannel(dbReadResult: 0);
        channel.Client = mock;

        // DB / MB 之外的区域（$$ 相对地址）：必须显式失败，不能静默乱读
        var ex = Assert.Throws<NotImplementedException>(
            () => channel.Read("$$100", new byte[1], CancellationToken.None));

        Assert.Contains("不支持的地址区域类型", ex.Message);
        Assert.Contains("$$100", ex.Message);
        Assert.Equal(0, mock.DBReadCallCount);
        Assert.Equal(0, mock.MBReadCallCount);
    }

    [Fact]
    public void Write_UnsupportedArea_ShouldThrowNotImplemented()
    {
        var (channel, mock) = CreateChannel(dbWriteResult: 0);
        channel.Client = mock;

        var ex = Assert.Throws<NotImplementedException>(
            () => channel.Write("$$100", new byte[] { 0x01 }, CancellationToken.None));

        Assert.Contains("不支持的地址区域类型", ex.Message);
        Assert.Contains("$$100", ex.Message);
        Assert.Equal(0, mock.DBWriteCallCount);
        Assert.Equal(0, mock.MBWriteCallCount);
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

    public override int DBRead(int dbNumber, int start, int size, Span<byte> buffer)
    {
        DBReadCallCount++;
        DBReadAction?.Invoke();
        if (DBReadBuffer.Length > 0)
        {
            var copyLen = Math.Min(size, DBReadBuffer.Length);
            DBReadBuffer.AsSpan(0, copyLen).CopyTo(buffer);
        }
        return DBReadResult;
    }

    public override int DBWrite(int dbNumber, int start, int size, Span<byte> buffer)
    {
        DBWriteCallCount++;
        return DBWriteResult;
    }

    public override int MBRead(int start, int size, Span<byte> buffer)
    {
        MBReadCallCount++;
        if (MBReadBuffer.Length > 0)
        {
            var copyLen = Math.Min(size, MBReadBuffer.Length);
            MBReadBuffer.AsSpan(0, copyLen).CopyTo(buffer);
        }
        return MBReadResult;
    }

    public override int MBWrite(int start, int size, Span<byte> buffer)
    {
        MBWriteCallCount++;
        return MBWriteResult;
    }
}
