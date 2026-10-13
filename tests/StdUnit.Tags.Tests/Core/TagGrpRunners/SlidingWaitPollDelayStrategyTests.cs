using System;
using Xunit;

namespace StdUnit.Tags.Tests.Core.TagGrpRunners;

public class SlidingWaitPollDelayStrategyTests
{
    [Fact]
    public void SlidingWait_ReturnsScanInterval()
    {
        // Arrange
        var strategy = new SlidingWaitPollDelayStrategy();

        // Act
        var delay = strategy.GetDelay(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(10));

        // Assert
        Assert.Equal(TimeSpan.FromMilliseconds(50), delay);
    }

    [Fact]
    public void SlidingWait_IgnoresElapsed()
    {
        // Arrange
        var strategy = new SlidingWaitPollDelayStrategy();

        // Act — elapsed 不同但返回值相同
        var delayFast = strategy.GetDelay(TimeSpan.FromMilliseconds(30), TimeSpan.FromMilliseconds(1));
        var delaySlow = strategy.GetDelay(TimeSpan.FromMilliseconds(30), TimeSpan.FromMilliseconds(100));

        // Assert
        Assert.Equal(delayFast, delaySlow);
    }
}
