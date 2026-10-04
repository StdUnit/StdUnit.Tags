using System;
using System.Collections.Generic;
using Xunit;
using StdUnit.Tags.Rx;
using System.Reactive;

namespace StdUnit.Tags.Tests.Rx;

public class TagR3Extensions_ObserveOnWritten_Tests
{
    [Fact]
    public void ObserveOnWritten_ShouldReceiveEvent_WhenTagWrites()
    {
        // Arrange
        var tag = new FakeRxTag();
        var received = new List<EventPattern<ITag, TagSyncEventArgs>>();

        using var subscription = tag.ObserveOnWritten().Subscribe(received.Add);

        // Act
        tag.FireOnWritten(3.14);

        // Assert
        Assert.Single(received);
        Assert.Equal(3.14, received[0].EventArgs.NewValue);
        Assert.Equal(TagSyncEventArgs.Kinds.Written, received[0].EventArgs.Kind);
    }

    [Fact]
    public void ObserveOnWritten_ShouldNotReceiveReadEvents()
    {
        // Arrange
        var tag = new FakeRxTag();
        var received = new List<EventPattern<ITag, TagSyncEventArgs>>();

        using var subscription = tag.ObserveOnWritten().Subscribe(e => received.Add(e));

        // Act
        tag.FireOnRead(100);

        // Assert
        Assert.Empty(received);
    }
}
