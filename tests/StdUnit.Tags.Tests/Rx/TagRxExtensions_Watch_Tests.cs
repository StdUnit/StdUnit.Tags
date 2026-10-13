using System;
using System.Collections.Generic;
using Xunit;
using StdUnit.Tags.Rx;
using System.Reactive;

namespace StdUnit.Tags.Tests.Rx;

public class TagR3Extensions_Watch_Tests
{
    [Fact]
    public void Watch_WithStartWithCurrent_ShouldEmitInitialValue()
    {
        // Arrange
        var tag = new FakeRxTag { Value = 123, Timestamp = DateTime.UtcNow };
        var received = new List<EventPattern<ITag, TagSyncEventArgs>>();

        using var subscription = tag.Watch(startWithCurrent: true).Subscribe(received.Add);

        // Assert
        Assert.Single(received);
        Assert.Equal(123, received[0].EventArgs.NewValue);
        Assert.Equal(TagSyncEventArgs.Kinds.None, received[0].EventArgs.Kind);
    }

    [Fact]
    public void Watch_WithoutStartWithCurrent_ShouldNotEmitInitialValue()
    {
        // Arrange
        var tag = new FakeRxTag { Value = 123, Timestamp = DateTime.UtcNow };
        var received = new List<EventPattern<ITag, TagSyncEventArgs>>();

        using var subscription = tag.Watch(startWithCurrent: false).Subscribe(e => received.Add(e));

        // Assert
        Assert.Empty(received);
    }

    [Fact]
    public void Watch_DefaultStartWithCurrent_ShouldBeTrue()
    {
        // Arrange
        var tag = new FakeRxTag { Value = "default", Timestamp = DateTime.UtcNow };
        var received = new List<EventPattern<ITag, TagSyncEventArgs>>();

        using var subscription = tag.Watch().Subscribe(received.Add);

        // Assert
        Assert.Single(received);
        Assert.Equal("default", received[0].EventArgs.NewValue);
        Assert.Equal(TagSyncEventArgs.Kinds.None, received[0].EventArgs.Kind);
    }

    [Fact]
    public void Watch_ShouldMergeReadAndWrittenEvents()
    {
        // Arrange
        var tag = new FakeRxTag { Value = 0, Timestamp = DateTime.UtcNow };
        var received = new List<EventPattern<ITag, TagSyncEventArgs>>();

        using var subscription = tag.Watch(startWithCurrent: false).Subscribe(e => received.Add(e));

        // Act
        tag.FireOnRead(10);
        tag.FireOnWritten(20);
        tag.FireOnRead(30);

        // Assert
        Assert.Equal(3, received.Count);

        Assert.Equal(10, received[0].EventArgs.NewValue);
        Assert.Equal(TagSyncEventArgs.Kinds.Read, received[0].EventArgs.Kind);

        Assert.Equal(20, received[1].EventArgs.NewValue);
        Assert.Equal(TagSyncEventArgs.Kinds.Written, received[1].EventArgs.Kind);

        Assert.Equal(30, received[2].EventArgs.NewValue);
        Assert.Equal(TagSyncEventArgs.Kinds.Read, received[2].EventArgs.Kind);
    }

    [Fact]
    public void Watch_WithStartWithCurrent_ShouldPrependInitialBeforeEvents()
    {
        // Arrange
        var tag = new FakeRxTag { Value = "initial", Timestamp = DateTime.UtcNow };
        var received = new List<EventPattern<ITag, TagSyncEventArgs>>();

        using var subscription = tag.Watch(startWithCurrent: true).Subscribe(received.Add);

        // Act
        tag.FireOnRead("after-read");
        tag.FireOnWritten("after-write");

        // Assert
        Assert.Equal(3, received.Count);

        // First should be the initial value
        Assert.Equal("initial", received[0].EventArgs.NewValue);
        Assert.Equal(TagSyncEventArgs.Kinds.None, received[0].EventArgs.Kind);

        // Then the real events
        Assert.Equal("after-read", received[1].EventArgs.NewValue);
        Assert.Equal(TagSyncEventArgs.Kinds.Read, received[1].EventArgs.Kind);

        Assert.Equal("after-write", received[2].EventArgs.NewValue);
        Assert.Equal(TagSyncEventArgs.Kinds.Written, received[2].EventArgs.Kind);
    }

    [Fact]
    public void Watch_ShouldUseCurrentTagValueForInitialEvent()
    {
        // Arrange
        var tag = new FakeRxTag();
        // Set value before subscribing
        tag.Value = 999;
        tag.Timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var received = new List<EventPattern<ITag, TagSyncEventArgs>>();

        using var subscription = tag.Watch(startWithCurrent: true).Subscribe(e => received.Add(e));

        // Assert
        Assert.Single(received);
        Assert.Equal(999, received[0].EventArgs.NewValue);
        Assert.Equal(tag.Timestamp, received[0].EventArgs.Timestamp);
        Assert.Equal(TagSyncEventArgs.Kinds.None, received[0].EventArgs.Kind);
    }
}
