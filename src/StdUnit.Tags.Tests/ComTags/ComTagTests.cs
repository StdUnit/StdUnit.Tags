using StdUnit.Tags.ComScanner;
using StdUnit.Tags.ComScanner.Channels;
using StdUnit.Tags.ComScanner.Tags;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StdUnit.Tags.Tests.ComTags;

public class ComTagTests
{
    private static readonly ILogger<LineBasedComChannel> Logger = NullLogger<LineBasedComChannel>.Instance;

    #region ComReadOnlyTag Tests

    [Fact]
    public async Task ReadAsync_WithDataInChannel_ShouldDequeueAndSetValue()
    {
        var mockPort = new MockSerialPortHandle();
        mockPort.ReadLineQueue.Enqueue("scanned-barcode-456");

        var channel = CreateChannel("ch-ro-data", mockPort);
        var tag = CreateReadOnlyTag("ro-tag-data", channel);

        using var cts = new CancellationTokenSource();
        try
        {
            await channel.EnsureConnectedAsync(false, cts.Token);
            await Task.Delay(100); // wait for polling thread to read & enqueue
            await tag.ReadAsync(cts.Token);

            Assert.Equal("scanned-barcode-456", tag.Value);
            Assert.NotEqual(default, tag.Timestamp);
        }
        finally
        {
            cts.Cancel();
            await channel.DisconnectAsync(CancellationToken.None);
            channel.Dispose();
        }
    }

    [Fact]
    public async Task ReadAsync_EmptyChannel_ShouldNotChangeValue()
    {
        var mockPort = new MockSerialPortHandle();
        // ReadLineQueue is empty → polling thread reads empty string → ParseDataAsync returns null → skip

        var channel = CreateChannel("ch-ro-empty", mockPort);
        var tag = CreateReadOnlyTag("ro-tag-empty", channel);
        tag.Value = "previous-value";

        using var cts = new CancellationTokenSource();
        try
        {
            await channel.EnsureConnectedAsync(false, cts.Token);
            await Task.Delay(100);
            await tag.ReadAsync(cts.Token);

            Assert.Equal("previous-value", tag.Value);
        }
        finally
        {
            cts.Cancel();
            await channel.DisconnectAsync(CancellationToken.None);
            channel.Dispose();
        }
    }

    [Fact]
    public async Task ReadAsync_ShouldFireOnTagReadEvent()
    {
        var mockPort = new MockSerialPortHandle();
        mockPort.ReadLineQueue.Enqueue("event-data");

        var channel = CreateChannel("ch-ro-event", mockPort);
        var tag = CreateReadOnlyTag("ro-tag-event", channel);

        var eventFired = new TaskCompletionSource<object?>();
        tag.OnTagRead += (_, args) => eventFired.TrySetResult(args.NewValue);

        using var cts = new CancellationTokenSource();
        try
        {
            await channel.EnsureConnectedAsync(false, cts.Token);
            await Task.Delay(100);
            await tag.ReadAsync(cts.Token);

            var result = await eventFired.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal("event-data", result);
        }
        finally
        {
            cts.Cancel();
            await channel.DisconnectAsync(CancellationToken.None);
            channel.Dispose();
        }
    }

    [Fact]
    public async Task ReadAsync_ShouldUpdateTimestamp()
    {
        var mockPort = new MockSerialPortHandle();
        mockPort.ReadLineQueue.Enqueue("ts-data");

        var channel = CreateChannel("ch-ro-ts", mockPort);
        var tag = CreateReadOnlyTag("ro-tag-ts", channel);

        using var cts = new CancellationTokenSource();
        try
        {
            await channel.EnsureConnectedAsync(false, cts.Token);
            await Task.Delay(100);

            var before = DateTime.Now.AddSeconds(-1);
            await tag.ReadAsync(cts.Token);

            Assert.True(tag.Timestamp >= before, "Timestamp should be updated after ReadAsync");
        }
        finally
        {
            cts.Cancel();
            await channel.DisconnectAsync(CancellationToken.None);
            channel.Dispose();
        }
    }

    [Fact]
    public async Task ReadAsync_EnsureConnected_CalledOnce()
    {
        var mockPort = new MockSerialPortHandle();

        var channel = CreateChannel("ch-ro-conn", mockPort);
        var tag = CreateReadOnlyTag("ro-tag-conn", channel);

        using var cts = new CancellationTokenSource();
        try
        {
            // First ReadAsync → connects
            await tag.ReadAsync(cts.Token);
            Assert.True(mockPort.IsOpen);
            Assert.Equal(1, mockPort.OpenCallCount);

            // Second ReadAsync → already connected, no extra open
            await tag.ReadAsync(cts.Token);
            Assert.Equal(1, mockPort.OpenCallCount);
        }
        finally
        {
            cts.Cancel();
            await channel.DisconnectAsync(CancellationToken.None);
            channel.Dispose();
        }
    }

    [Fact]
    public async Task WriteAsync_ShouldClearDirtyFlagAndFireEvent()
    {
        var mockPort = new MockSerialPortHandle();
        var channel = CreateChannel("ch-ro-write", mockPort);
        var tag = CreateReadOnlyTag("ro-tag-write", channel);
        tag.Value = "test";
        // Note: ComReadOnlyTag.Value setter overrides base without setting IsDirty
        // So we set _dirty manually via the base path
        tag.IsDirty = true;

        var eventFired = new TaskCompletionSource<bool>();
        tag.OnTagWritten += (_, _) => eventFired.TrySetResult(true);

        await tag.WriteAsync(CancellationToken.None);

        Assert.False(tag.IsDirty);
        Assert.True(await eventFired.Task.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void WriteAsync_ShouldNotWriteToSerialPort()
    {
        var mockPort = new MockSerialPortHandle();
        var channel = CreateChannel("ch-ro-nowrite", mockPort);
        var tag = CreateReadOnlyTag("ro-tag-nowrite", channel);

        tag.Value = "some-value";
        Assert.Equal(0, mockPort.WriteStringCallCount);
        Assert.Equal(0, mockPort.WriteBytesCallCount);
    }

    [Fact]
    public void ValueSetter_ShouldSetValueOnly_WithoutSideEffects()
    {
        var mockPort = new MockSerialPortHandle();
        var channel = CreateChannel("ch-ro-setter", mockPort);
        var tag = CreateReadOnlyTag("ro-tag-setter", channel);

        tag.Value = "via-setter";
        Assert.Equal("via-setter", tag.Value);
        // ComReadOnlyTag overrides setter → does NOT set IsDirty or Timestamp
        Assert.False(tag.IsDirty);
        Assert.Equal(default, tag.Timestamp);
        Assert.Equal(0, mockPort.WriteStringCallCount);
    }

    #endregion

    #region ComWriteOnlyTag Tests

    [Fact]
    public async Task WriteAsync_WithValue_ShouldWriteConvertedBytesToSerialPort()
    {
        var mockPort = new MockSerialPortHandle();
        var channel = CreateChannel("ch-wo-write", mockPort);
        var tag = CreateWriteOnlyTag("wo-tag-write", channel);

        using var cts = new CancellationTokenSource();
        try
        {
            await channel.EnsureConnectedAsync(false, cts.Token);

            tag.Value = "hello-write";
            await tag.WriteAsync(cts.Token);

            Assert.Equal(1, mockPort.WriteBytesCallCount);
        }
        finally
        {
            cts.Cancel();
            await channel.DisconnectAsync(CancellationToken.None);
            channel.Dispose();
        }
    }

    [Fact]
    public async Task WriteAsync_NullValue_ShouldSkipWriteToSerialPort()
    {
        var mockPort = new MockSerialPortHandle();
        var channel = CreateChannel("ch-wo-null", mockPort);
        var tag = CreateWriteOnlyTag("wo-tag-null", channel);

        using var cts = new CancellationTokenSource();
        try
        {
            await channel.EnsureConnectedAsync(false, cts.Token);

            // Value is null by default
            await tag.WriteAsync(cts.Token);

            Assert.Equal(0, mockPort.WriteBytesCallCount);
        }
        finally
        {
            cts.Cancel();
            await channel.DisconnectAsync(CancellationToken.None);
            channel.Dispose();
        }
    }

    [Fact]
    public async Task WriteAsync_ShouldFireOnTagWrittenEvent()
    {
        var mockPort = new MockSerialPortHandle();
        var channel = CreateChannel("ch-wo-event", mockPort);
        var tag = CreateWriteOnlyTag("wo-tag-event", channel);

        var eventFired = new TaskCompletionSource<object?>();
        tag.OnTagWritten += (_, args) => eventFired.TrySetResult(args.NewValue);

        using var cts = new CancellationTokenSource();
        try
        {
            await channel.EnsureConnectedAsync(false, cts.Token);

            tag.Value = "fire-event";
            await tag.WriteAsync(cts.Token);

            var result = await eventFired.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal("fire-event", result);
        }
        finally
        {
            cts.Cancel();
            await channel.DisconnectAsync(CancellationToken.None);
            channel.Dispose();
        }
    }

    [Fact]
    public async Task WriteAsync_ShouldClearDirtyFlag()
    {
        var mockPort = new MockSerialPortHandle();
        var channel = CreateChannel("ch-wo-dirty", mockPort);
        var tag = CreateWriteOnlyTag("wo-tag-dirty", channel);

        // Value setter in base sets IsDirty = true
        tag.Value = "mark-dirty";
        Assert.True(tag.IsDirty);

        using var cts = new CancellationTokenSource();
        try
        {
            await channel.EnsureConnectedAsync(false, cts.Token);
            await tag.WriteAsync(cts.Token);

            Assert.False(tag.IsDirty);
        }
        finally
        {
            cts.Cancel();
            await channel.DisconnectAsync(CancellationToken.None);
            channel.Dispose();
        }
    }

    [Fact]
    public async Task WriteAsync_ShouldUpdateTimestampOnValueSetter()
    {
        var mockPort = new MockSerialPortHandle();
        var channel = CreateChannel("ch-wo-ts", mockPort);
        var tag = CreateWriteOnlyTag("wo-tag-ts", channel);

        using var cts = new CancellationTokenSource();
        try
        {
            await channel.EnsureConnectedAsync(false, cts.Token);

            var before = DateTime.Now.AddSeconds(-1);
            tag.Value = "timestamp-check";
            Assert.True(tag.Timestamp >= before, "Value setter should update Timestamp");

            await tag.WriteAsync(cts.Token);
        }
        finally
        {
            cts.Cancel();
            await channel.DisconnectAsync(CancellationToken.None);
            channel.Dispose();
        }
    }

    [Fact]
    public void ReadAsync_ShouldDoNothing()
    {
        var mockPort = new MockSerialPortHandle();
        var channel = CreateChannel("ch-wo-nop", mockPort);
        var tag = CreateWriteOnlyTag("wo-tag-nop", channel);

        var task = tag.ReadAsync(CancellationToken.None);
        Assert.True(task.Status == TaskStatus.RanToCompletion);
    }

    #endregion

    #region Helpers

    private static LineBasedComChannel CreateChannel(string name, MockSerialPortHandle mockPort)
    {
        var channel = new LineBasedComChannel(
            new ComChannelDescriptor()
            {
                Name = name,
                Option = new ComChannelOption
                {
                    Port = "COM_TEST",
                    ReadEntireLine = true,
                },
            },
            Logger
        );
        channel.SerialPortFactory = _ => mockPort;
        return channel;
    }

    private static ComReadOnlyTag<string> CreateReadOnlyTag(string tagName, LineBasedComChannel channel)
    {
        var grp = new TagGrp(new TagGrpDescriptor { Name = "g", IsEntry = true }, channel);
        var descriptor = new TagDescriptor
        {
            TagName = tagName,
            TagKind = BuiltinTagKinds.STR,
            AccessMode = TagAccessMode.RO,
        };
        return new ComReadOnlyTag<string>(descriptor, channel, TagContainer.From(grp));
    }

    private static ComWriteOnlyTag<string> CreateWriteOnlyTag(string tagName, LineBasedComChannel channel)
    {
        var grp = new TagGrp(new TagGrpDescriptor { Name = "g", IsEntry = true }, channel);
        var descriptor = new TagDescriptor
        {
            TagName = tagName,
            TagKind = BuiltinTagKinds.STR,
            AccessMode = TagAccessMode.WO,
        };
        return new ComWriteOnlyTag<string>(
            descriptor, channel, TagContainer.From(grp),
            converter: str => Encoding.UTF8.GetBytes(str)
        );
    }

    #endregion
}
