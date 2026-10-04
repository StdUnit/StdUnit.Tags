using System;
using Xunit;

namespace StdUnit.Tags.Tests.Core.TagGrpRunners;

public class AdaptivePollDelayStrategyTests
{
    [Fact]
    public void Adaptive_WhenUnderInterval_ReturnsRemaining()
    {
        // Arrange — elapsed=10ms, interval=50ms → 40ms
        var strategy = new AdaptivePollDelayStrategy();

        // Act
        var delay = strategy.GetDelay(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(10));

        // Assert
        Assert.Equal(TimeSpan.FromMilliseconds(40), delay);
    }

    [Fact]
    public void Adaptive_WhenOverInterval_ReturnsZero()
    {
        // Arrange — elapsed=60ms > interval=50ms → 0
        var strategy = new AdaptivePollDelayStrategy();

        // Act
        var delay = strategy.GetDelay(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(60));

        // Assert
        Assert.Equal(TimeSpan.Zero, delay);
    }

    [Fact]
    public void Adaptive_WhenExactlyAtInterval_ReturnsZero()
    {
        // Arrange
        var strategy = new AdaptivePollDelayStrategy();

        // Act
        var delay = strategy.GetDelay(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.Equal(TimeSpan.Zero, delay);
    }

    [Fact]
    public void Adaptive_WhenNotOverInterval_ReturnsPositive()
    {
        // Arrange
        var strategy = new AdaptivePollDelayStrategy();

        // Act
        var delay = strategy.GetDelay(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(30));

        // Assert — 返回值应为 70 且 > 0
        Assert.Equal(TimeSpan.FromMilliseconds(70), delay);
        Assert.True(delay > TimeSpan.Zero);
    }
}
