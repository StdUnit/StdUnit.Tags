using System.Collections.Generic;
using Itminus.Tags.R3;
using Moq;
using R3;
using Xunit;

namespace Itminus.Tags.Tests.R3;

public class TagsProjectCtrlR3Extensions_ObserveStartedOrStopped_Tests
{
    [Fact]
    public void ObserveStartedOrStopped_ShouldReceiveStartedAndStoppedEvents()
    {
        // Arrange
        var ctrl = new FakeR3TagsProjectCtrl();
        var received = new List<TagsProjectEventArgs>();
        using var subscription = ctrl.ObserveStartedOrStopped().Subscribe(received.Add);

        // Act
        ctrl.FireStarted();
        ctrl.FireStopped();

        // Assert
        Assert.Equal(2, received.Count);
        Assert.True(received[0].IsStarted);
        Assert.False(received[1].IsStarted);
        Assert.Null(received[1].Project);
    }

    [Fact]
    public void ObserveStartedOrStopped_ShouldKeepProjectPayload()
    {
        // Arrange
        var ctrl = new FakeR3TagsProjectCtrl();
        var project = new Mock<ITagsProject>().Object;
        var received = new List<TagsProjectEventArgs>();
        using var subscription = ctrl.ObserveStartedOrStopped().Subscribe(received.Add);

        // Act
        ctrl.FireStarted(project);

        // Assert
        Assert.Single(received);
        Assert.True(received[0].IsStarted);
        Assert.Same(project, received[0].Project);
    }

    [Fact]
    public void ObserveStartedOrStopped_ShouldStopReceiving_AfterDisposal()
    {
        // Arrange
        var ctrl = new FakeR3TagsProjectCtrl();
        var received = new List<TagsProjectEventArgs>();
        var subscription = ctrl.ObserveStartedOrStopped().Subscribe(received.Add);
        ctrl.FireStarted();
        Assert.Single(received);

        // Act
        subscription.Dispose();
        ctrl.FireStopped();

        // Assert
        Assert.Single(received);
        Assert.True(received[0].IsStarted);
    }
}
