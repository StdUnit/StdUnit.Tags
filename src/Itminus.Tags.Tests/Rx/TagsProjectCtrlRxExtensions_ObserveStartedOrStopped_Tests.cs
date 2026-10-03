using System;
using System.Collections.Generic;
using Itminus.Tags.Rx;
using Moq;
using Xunit;

namespace Itminus.Tags.Tests.Rx;

public class TagsProjectCtrlRxExtensions_ObserveStartedOrStopped_Tests
{
    [Fact]
    public void ObserveStartedOrStopped_ShouldReceiveStartedAndStoppedEvents()
    {
        // Arrange
        var ctrl = new FakeRxTagsProjectCtrl();
        var received = new List<TagsProjectEventArgs>();
        using var subscription = ctrl.ObserveStartedOrStopped().Subscribe(new ListObserver(received));

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
        var ctrl = new FakeRxTagsProjectCtrl();
        var project = new Mock<ITagsProject>().Object;
        var received = new List<TagsProjectEventArgs>();
        using var subscription = ctrl.ObserveStartedOrStopped().Subscribe(new ListObserver(received));

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
        var ctrl = new FakeRxTagsProjectCtrl();
        var received = new List<TagsProjectEventArgs>();
        var subscription = ctrl.ObserveStartedOrStopped().Subscribe(new ListObserver(received));
        ctrl.FireStarted();
        Assert.Single(received);

        // Act
        subscription.Dispose();
        ctrl.FireStopped();

        // Assert
        Assert.Single(received);
        Assert.True(received[0].IsStarted);
    }

    private sealed class ListObserver(List<TagsProjectEventArgs> target) : IObserver<TagsProjectEventArgs>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(TagsProjectEventArgs value) => target.Add(value);
    }
}
