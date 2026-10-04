using System;
using System.Collections.Generic;
using Xunit;
using StdUnit.Tags.R3;
using R3;

namespace StdUnit.Tags.Tests.R3;

public class TagR3Extensions_ObserveOnWritten_Tests
{
    [Fact]
    public void ObserveOnWritten_ShouldReceiveEvent_WhenTagWrites()
    {
        // Arrange
        var tag = new FakeR3Tag();
        var received = new List<TagSyncEventArgs>();

        using var subscription = tag.ObserveOnWritten().Subscribe(received.Add);

        // Act
        tag.FireOnWritten(3.14);

        // Assert
        Assert.Single(received);
        Assert.Equal(3.14, received[0].NewValue);
        Assert.Equal(TagSyncEventArgs.Kinds.Written, received[0].Kind);
    }

    [Fact]
    public void ObserveOnWritten_ShouldNotReceiveReadEvents()
    {
        // Arrange
        var tag = new FakeR3Tag();
        var received = new List<TagSyncEventArgs>();

        using var subscription = tag.ObserveOnWritten().Subscribe(received.Add);

        // Act
        tag.FireOnRead(100);

        // Assert
        Assert.Empty(received);
    }
}
