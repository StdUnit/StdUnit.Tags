using StdUnit.Tags.ComScanner;
using StdUnit.Tags.ComScanner.Channels;
using StdUnit.Tags.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StdUnit.Tags.Tests.ComTags;

public class LineBasedComChannelTests
{
    private static readonly ILogger<LineBasedComChannel> Logger = NullLogger<LineBasedComChannel>.Instance;

    /// <summary>
    /// 默认 ReadEntireLine 应为 true（保持向后兼容）
    /// </summary>
    [Fact]
    public void ReadEntireLine_DefaultValue_ShouldBeTrue()
    {
        var descriptor = new ComChannelDescriptor
        {
            Name = "ch",
            Option = new ComChannelOption
            {
                Port = "COM_TEST",
            },
        };
        var channel = new LineBasedComChannel(descriptor, Logger);
        Assert.True(channel.ReadEntireLine);
    }

    /// <summary>
    /// ChannelName 应通过构造函数正确传递
    /// </summary>
    [Fact]
    public void Constructor_ShouldSetChannelName()
    {
        var descriptor = new ComChannelDescriptor
        {
            Name = "my-channel",
            Option = new ComChannelOption
            {
                Port = "COM_TEST",
            },
        };
        var channel = new LineBasedComChannel(descriptor, Logger);
        Assert.Equal("my-channel", channel.ChannelName());
    }

    /// <summary>
    /// Driver 应返回 ComDriverNames.DriverName
    /// </summary>
    [Fact]
    public void Driver_ShouldReturnComDriverName()
    {
        var descriptor = new ComChannelDescriptor
        {
            Name = "ch",
            Option = new ComChannelOption
            {
                Port = "COM_TEST",
            },
        };
        var channel = new LineBasedComChannel(descriptor, Logger);
        Assert.Equal(ComDriverNames.DriverName, channel.Driver());
    }

    /// <summary>
    /// EnsureConnectedAsync 通过 SerialPortFactory 注入 Mock → 应打开串口
    /// </summary>
    [Fact]
    public async Task EnsureConnectedAsync_ShouldOpenSerialPort()
    {
        var mockPort = new MockSerialPortHandle();
        var descriptor = new ComChannelDescriptor
        {
            Name = "ch-open",
            Option = new ComChannelOption
            {
                Port = "COM_TEST",
            },
        };
        var channel = new LineBasedComChannel(descriptor, Logger);
        channel.SerialPortFactory = _ => mockPort;

        using var cts = new CancellationTokenSource();
        try
        {
            await channel.EnsureConnectedAsync(false, cts.Token);

            Assert.Equal(1, mockPort.OpenCallCount);
            Assert.NotNull(channel.SerialPort);
        }
        finally
        {
            cts.Cancel();
            await channel.DisconnectAsync(CancellationToken.None);
            channel.Dispose();
        }
    }

    /// <summary>
    /// DisconnectAsync 应关闭 SerialPort 并将引用置为 null
    /// </summary>
    [Fact]
    public async Task DisconnectAsync_ShouldCloseSerialPort()
    {
        var mockPort = new MockSerialPortHandle();
        var descriptor = new ComChannelDescriptor
        {
            Name = "ch-open",
            Option = new ComChannelOption
            {
                Port = "COM_TEST",
            },
        };
        var channel = new LineBasedComChannel(descriptor, Logger);
        channel.SerialPortFactory = _ => mockPort;

        using var cts = new CancellationTokenSource();
        try
        {
            await channel.EnsureConnectedAsync(false, cts.Token);
            await channel.DisconnectAsync(CancellationToken.None);

            Assert.Equal(1, mockPort.CloseCallCount);
            Assert.Null(channel.SerialPort);
        }
        finally
        {
            cts.Cancel();
            channel.Dispose();
        }
    }

    /// <summary>
    /// NewLine 应被设置到串口句柄上
    /// </summary>
    [Fact]
    public async Task EnsureConnectedAsync_ShouldSetNewLine()
    {
        var mockPort = new MockSerialPortHandle();
        var descriptor = new ComChannelDescriptor
        {
            Name = "ch-newline",
            Option = new ComChannelOption
            {
                Port = "COM_TEST",
                NewLine = "abcdefg",
            },
        };
        var channel = new LineBasedComChannel(descriptor, Logger);
        channel.SerialPortFactory = _ => mockPort;

        using var cts = new CancellationTokenSource();
        try
        {
            await channel.EnsureConnectedAsync(false, cts.Token);

            Assert.True(mockPort.IsOpen);
            Assert.Equal("abcdefg", mockPort.NewLine);
        }
        finally
        {
            cts.Cancel();
            await channel.DisconnectAsync(CancellationToken.None);
            channel.Dispose();
        }
    }

    /// <summary>
    /// ReadEntireLine=true → ParseDataAsync 应调用 ReadLine()
    /// </summary>
    [Fact]
    public async Task ReadEntireLineTrue_ShouldCallReadLine()
    {
        var mockPort = new MockSerialPortHandle();
        mockPort.ReadLineQueue.Enqueue("scanned-barcode-123");

        var descriptor = new ComChannelDescriptor
        {
            Name = "ch-line",
            Option = new ComChannelOption
            {
                Port = "COM_TEST",
            },
        };
        var channel = new LineBasedComChannel(descriptor, Logger);
        channel.SerialPortFactory = _ => mockPort;
        channel.ReadEntireLine = true;

        using var cts = new CancellationTokenSource();
        var received = new TaskCompletionSource<string>();
        channel.DataReceived += (_, data) => received.TrySetResult(data);

        try
        {
            await channel.EnsureConnectedAsync(false, cts.Token);

            var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal("scanned-barcode-123", result);
        }
        finally
        {
            cts.Cancel();
            await channel.DisconnectAsync(CancellationToken.None);
            channel.Dispose();
        }
    }

    /// <summary>
    /// ReadEntireLine=false → ParseDataAsync 应调用 ReadExisting()
    /// </summary>
    [Fact]
    public async Task ReadEntireLineFalse_ShouldCallReadExisting()
    {
        var mockPort = new MockSerialPortHandle();
        mockPort.ReadExistingReturnValue = "raw-buffer-data";

        var descriptor = new ComChannelDescriptor
        {
            Name = "ch-existing",
            Option = new ComChannelOption
            {
                Port = "COM_TEST",
                ReadEntireLine = false,
            },
        };
        var channel = new LineBasedComChannel(descriptor, Logger);
        channel.SerialPortFactory = _ => mockPort;

        using var cts = new CancellationTokenSource();
        var received = new TaskCompletionSource<string>();
        channel.DataReceived += (_, data) => received.TrySetResult(data);

        try
        {
            await channel.EnsureConnectedAsync(false, cts.Token);

            var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal("raw-buffer-data", result);
        }
        finally
        {
            cts.Cancel();
            await channel.DisconnectAsync(CancellationToken.None);
            channel.Dispose();
        }
    }

    /// <summary>
    /// WriteAsync 应调用串口句柄的 Write(string)
    /// </summary>
    [Fact]
    public async Task WriteAsync_ShouldCallSerialPortWrite()
    {
        var mockPort = new MockSerialPortHandle();

        var descriptor = new ComChannelDescriptor
        {
            Name = "ch-write",
            Option = new ComChannelOption
            {
                Port = "COM_TEST",
            },
        };
        var channel = new LineBasedComChannel(descriptor, Logger);
        channel.SerialPortFactory = _ => mockPort;

        using var cts = new CancellationTokenSource();
        try
        {
            await channel.EnsureConnectedAsync(false, cts.Token);
            await channel.WriteAsync("AT+CMD\r");

            Assert.Equal(1, mockPort.WriteStringCallCount);
            Assert.Equal("AT+CMD\r", mockPort.LastWrittenString);
        }
        finally
        {
            cts.Cancel();
            await channel.DisconnectAsync(CancellationToken.None);
            channel.Dispose();
        }
    }

    /// <summary>
    /// DisconnectAsync 在未连接时应安全执行，不抛异常
    /// </summary>
    [Fact]
    public async Task DisconnectAsync_WhenNotConnected_ShouldNotThrow()
    {
        var descriptor = new ComChannelDescriptor
        {
            Name = "ch-safe",
            Option = new ComChannelOption
            {
                Port = "COM_TEST",
            },
        };
        var channel = new LineBasedComChannel(descriptor, Logger);
        await channel.DisconnectAsync(CancellationToken.None);
        Assert.Null(channel.SerialPort);
    }

    /// <summary>
    /// Dispose 在未连接时应安全执行，不抛异常
    /// </summary>
    [Fact]
    public void Dispose_WhenNotConnected_ShouldNotThrow()
    {
        var descriptor = new ComChannelDescriptor
        {
            Name = "ch-safe",
            Option = new ComChannelOption
            {
                Port = "COM_TEST",
            },
        };
        var channel = new LineBasedComChannel(descriptor, Logger);
        channel.Dispose();
    }

    /// <summary>
    /// 轮询线程读串口失败时：日志必须带通道名，并把<b>异常对象本身</b>交给日志系统
    /// （只 log <c>ex.Message</c> 会丢掉异常类型与堆栈，现场无法定位）。
    /// </summary>
    [Fact]
    public async Task PollError_ShouldLogChannelNameAndExceptionObject()
    {
        var mockPort = new MockSerialPortHandle { FallbackToDefaultOnEmptyQueue = false };
        var descriptor = new ComChannelDescriptor
        {
            Name = "ch-poll",
            Option = new ComChannelOption
            {
                Port = "COM_TEST",
            },
        };
        var logs = new CapturingLoggerProvider();
        using var factory = LoggerFactory.Create(b => b.AddProvider(logs));
        var channel = new LineBasedComChannel(descriptor, factory.CreateLogger<LineBasedComChannel>());
        channel.SerialPortFactory = _ => mockPort;

        using var cts = new CancellationTokenSource();
        try
        {
            await channel.EnsureConnectedAsync(false, cts.Token);

            // 轮询跑在独立线程上，等它把错误记下来
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (logs.Entries.Count == 0 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20);
            }

            var entry = Assert.Single(logs.Entries);
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.Contains("ch-poll", entry.Message);
            var ex = Assert.IsType<InvalidOperationException>(entry.Exception);
            Assert.Contains("ReadLineQueue", ex.Message);
        }
        finally
        {
            cts.Cancel();
            await channel.DisconnectAsync(CancellationToken.None);
            channel.Dispose();
        }
    }
}
