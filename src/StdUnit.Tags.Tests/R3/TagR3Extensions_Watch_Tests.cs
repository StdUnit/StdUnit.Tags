using System;
using System.Collections.Generic;
using Xunit;
using StdUnit.Tags.R3;
using R3;

namespace StdUnit.Tags.Tests.R3;

public class TagR3Extensions_Watch_Tests
{
    [Fact]
    public void Watch_WithStartWithCurrent_ShouldEmitInitialValue()
    {
        // Arrange
        var tag = new FakeR3Tag { Value = 123, Timestamp = DateTime.UtcNow };
        var received = new List<TagSyncEventArgs>();

        using var subscription = tag.Watch(startWithCurrent: true).Subscribe(received.Add);

        // Assert
        Assert.Single(received);
        Assert.Equal(123, received[0].NewValue);
        Assert.Equal(TagSyncEventArgs.Kinds.None, received[0].Kind);
    }

    [Fact]
    public void Watch_WithoutStartWithCurrent_ShouldNotEmitInitialValue()
    {
        // Arrange
        var tag = new FakeR3Tag { Value = 123, Timestamp = DateTime.UtcNow };
        var received = new List<TagSyncEventArgs>();

        using var subscription = tag.Watch(startWithCurrent: false).Subscribe(received.Add);

        // Assert
        Assert.Empty(received);
    }

    [Fact]
    public void Watch_DefaultStartWithCurrent_ShouldBeTrue()
    {
        // Arrange
        var tag = new FakeR3Tag { Value = "default", Timestamp = DateTime.UtcNow };
        var received = new List<TagSyncEventArgs>();

        using var subscription = tag.Watch().Subscribe(received.Add);

        // Assert
        Assert.Single(received);
        Assert.Equal("default", received[0].NewValue);
        Assert.Equal(TagSyncEventArgs.Kinds.None, received[0].Kind);
    }

    [Fact]
    public void Watch_ShouldMergeReadAndWrittenEvents()
    {
        // Arrange
        var tag = new FakeR3Tag { Value = 0, Timestamp = DateTime.UtcNow };
        var received = new List<TagSyncEventArgs>();

        using var subscription = tag.Watch(startWithCurrent: false).Subscribe(received.Add);

        // Act
        tag.FireOnRead(10);
        tag.FireOnWritten(20);
        tag.FireOnRead(30);

        // Assert
        Assert.Equal(3, received.Count);

        Assert.Equal(10, received[0].NewValue);
        Assert.Equal(TagSyncEventArgs.Kinds.Read, received[0].Kind);

        Assert.Equal(20, received[1].NewValue);
        Assert.Equal(TagSyncEventArgs.Kinds.Written, received[1].Kind);

        Assert.Equal(30, received[2].NewValue);
        Assert.Equal(TagSyncEventArgs.Kinds.Read, received[2].Kind);
    }

    [Fact]
    public void Watch_WithStartWithCurrent_ShouldPrependInitialBeforeEvents()
    {
        // Arrange
        var tag = new FakeR3Tag { Value = "initial", Timestamp = DateTime.UtcNow };
        var received = new List<TagSyncEventArgs>();

        using var subscription = tag.Watch(startWithCurrent: true).Subscribe(received.Add);

        // Act
        tag.FireOnRead("after-read");
        tag.FireOnWritten("after-write");

        // Assert
        Assert.Equal(3, received.Count);

        // First should be the initial value
        Assert.Equal("initial", received[0].NewValue);
        Assert.Equal(TagSyncEventArgs.Kinds.None, received[0].Kind);

        // Then the real events
        Assert.Equal("after-read", received[1].NewValue);
        Assert.Equal(TagSyncEventArgs.Kinds.Read, received[1].Kind);

        Assert.Equal("after-write", received[2].NewValue);
        Assert.Equal(TagSyncEventArgs.Kinds.Written, received[2].Kind);
    }

    [Fact]
    public void Watch_ShouldUseCurrentTagValueForInitialEvent()
    {
        // Arrange
        var tag = new FakeR3Tag();
        // Set value before subscribing
        tag.Value = 999;
        tag.Timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var received = new List<TagSyncEventArgs>();

        using var subscription = tag.Watch(startWithCurrent: true).Subscribe(received.Add);

        // Assert
        Assert.Single(received);
        Assert.Equal(999, received[0].NewValue);
        Assert.Equal(tag.Timestamp, received[0].Timestamp);
        Assert.Equal(TagSyncEventArgs.Kinds.None, received[0].Kind);
    }
}
