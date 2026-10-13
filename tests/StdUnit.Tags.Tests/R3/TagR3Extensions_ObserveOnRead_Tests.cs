using System;
using System.Collections.Generic;
using Xunit;
using StdUnit.Tags.R3;
using R3;

namespace StdUnit.Tags.Tests.R3;

public class TagR3Extensions_ObserveOnRead_Tests
{
    [Fact]
    public void ObserveOnRead_ShouldReceiveEvent_WhenTagReads()
    {
        // Arrange
        var tag = new FakeR3Tag();
        var received = new List<TagSyncEventArgs>();

        using var subscription = tag.ObserveOnRead().Subscribe(e => received.Add(e));

        // Act
        tag.FireOnRead(42);

        // Assert
        Assert.Single(received);
        Assert.Equal(42, received[0].NewValue);
        Assert.Equal(TagSyncEventArgs.Kinds.Read, received[0].Kind);
    }

    [Fact]
    public void ObserveOnRead_ShouldNotReceiveWrittenEvents()
    {
        // Arrange
        var tag = new FakeR3Tag();
        var received = new List<TagSyncEventArgs>();

        using var subscription = tag.ObserveOnRead().Subscribe(received.Add);

        // Act
        tag.FireOnWritten(99);

        // Assert
        Assert.Empty(received);
    }

    [Fact]
    public void ObserveOnRead_ShouldStopReceiving_AfterDisposal()
    {
        // Arrange
        var tag = new FakeR3Tag();
        var received = new List<TagSyncEventArgs>();

        var subscription = tag.ObserveOnRead().Subscribe(received.Add);

        tag.FireOnRead(1);
        Assert.Single(received);

        // Act: dispose subscription
        subscription.Dispose();
        tag.FireOnRead(2);

        // Assert
        Assert.Single(received); // should not have increased
        Assert.Equal(1, received[0].NewValue);
    }

    [Fact]
    public void ObserveOnRead_ShouldReceiveMultipleEvents()
    {
        // Arrange
        var tag = new FakeR3Tag();
        var received = new List<TagSyncEventArgs>();

        using var subscription = tag.ObserveOnRead().Subscribe(received.Add);

        // Act
        tag.FireOnRead("a");
        tag.FireOnRead("b");
        tag.FireOnRead("c");

        // Assert
        Assert.Equal(3, received.Count);
        Assert.Equal("a", received[0].NewValue);
        Assert.Equal("b", received[1].NewValue);
        Assert.Equal("c", received[2].NewValue);
    }
}
