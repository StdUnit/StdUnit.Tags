using System;
using System.Linq;
using Xunit;

namespace Itminus.Tags.Tests.Core.TagGrpRunners;

public class DefaultTagGrpRunnerRetryStrategyTests
{
    [Fact]
    public void GetDelay_ReturnsIncreasingValues()
    {
        // Arrange
        var strategy = new DefaultTagGrpRunnerRetryStrategy
        {
            MinDelay = TimeSpan.FromMilliseconds(100),
            MaxDelay = TimeSpan.FromSeconds(10),
            GrowthRate = 1.0,
            Midpoint = 4,
        };

        // Act
        var delays = Enumerable.Range(1, 8)
            .Select(strategy.GetDelay)
            .ToList();

        // Assert — 延迟应严格递增
        for (int i = 1; i < delays.Count; i++)
        {
            Assert.True(delays[i] > delays[i - 1],
                $"d[{i}]={delays[i]:hh\\:mm\\:ss\\.fff} 应大于 d[{i - 1}]={delays[i - 1]:hh\\:mm\\:ss\\.fff}");
        }
    }

    [Fact]
    public void GetDelay_Plateaus_AtMax()
    {
        // Arrange
        var maxDelay = TimeSpan.FromSeconds(5);
        var strategy = new DefaultTagGrpRunnerRetryStrategy
        {
            MinDelay = TimeSpan.FromMilliseconds(100),
            MaxDelay = maxDelay,
            GrowthRate = 2.0,
            Midpoint = 3,
        };

        // Act
        var delays = Enumerable.Range(1, 15)
            .Select(strategy.GetDelay)
            .ToList();

        // Assert — 最终趋近但不超过 MaxDelay
        foreach (var d in delays)
        {
            Assert.True(d <= maxDelay,
                $"延迟 {d:hh\\:mm\\:ss\\.fff} 不应超过 MaxDelay={maxDelay:hh\\:mm\\:ss\\.fff}");
        }
        // 最后的值应非常接近 MaxDelay（>= 90%）
        var last = delays[^1];
        Assert.True(last >= maxDelay * 0.9,
            $"最终延迟 {last:hh\\:mm\\:ss\\.fff} 应接近 {maxDelay:hh\\:mm\\:ss\\.fff}");
    }

    [Fact]
    public void GetDelay_ReturnsMinDelay_ForNonPositiveCount()
    {
        // Arrange
        var strategy = new DefaultTagGrpRunnerRetryStrategy
        {
            MinDelay = TimeSpan.FromMilliseconds(200),
            MaxDelay = TimeSpan.FromSeconds(10),
        };

        // Act & Assert
        var delay = strategy.GetDelay(0);
        Assert.Equal(strategy.MinDelay, delay);

        delay = strategy.GetDelay(-1);
        Assert.Equal(strategy.MinDelay, delay);
    }

    [Fact]
    public void GetDelay_ReturnsMinDelay_ForFirstFailure()
    {
        // Arrange
        var strategy = new DefaultTagGrpRunnerRetryStrategy
        {
            MinDelay = TimeSpan.FromMilliseconds(500),
            MaxDelay = TimeSpan.FromSeconds(30),
        };

        // Act
        var delay = strategy.GetDelay(1);

        // Assert — minDelay 附近
        Assert.True(delay >= strategy.MinDelay,
            $"第1次失败的延迟 {delay:hh\\:mm\\:ss\\.fff} 应 >= MinDelay");
        Assert.True(delay < TimeSpan.FromSeconds(5),
            $"第1次失败的延迟应小于5s，实际 {delay:hh\\:mm\\:ss\\.fff}");
    }

    [Fact]
    public void GetDelay_Halfway_AtMidpoint()
    {
        // Arrange
        var strategy = new DefaultTagGrpRunnerRetryStrategy
        {
            MinDelay = TimeSpan.Zero,
            MaxDelay = TimeSpan.FromSeconds(10),
            GrowthRate = 1.0,
            Midpoint = 5,
        };

        // Act
        var delay = strategy.GetDelay(5);

        // Assert — 中点附近应接近 (min+max)/2 ≈ 5s
        var half = TimeSpan.FromSeconds(5);
        var tolerance = TimeSpan.FromSeconds(1);
        Assert.True(Math.Abs((delay - half).TotalMilliseconds) <= tolerance.TotalMilliseconds,
            $"中点处的延迟 {delay:hh\\:mm\\:ss\\.fff} 应接近 {half:hh\\:mm\\:ss\\.fff}");
    }

    [Fact]
    public void GetDelay_AlwaysWithinConfiguredRange()
    {
        // Arrange
        var strategy = new DefaultTagGrpRunnerRetryStrategy
        {
            MinDelay = TimeSpan.FromMilliseconds(300),
            MaxDelay = TimeSpan.FromSeconds(20),
            GrowthRate = 1.2,
            Midpoint = 4,
        };

        // Act & Assert — 收敛（clamp）后的值必须始终落在 [MinDelay, MaxDelay] 内
        foreach (var n in Enumerable.Range(1, 60))
        {
            var delay = strategy.GetDelay(n);
            Assert.True(delay >= strategy.MinDelay,
                $"n={n} 的延迟 {delay:hh\\:mm\\:ss\\.fff} 不应小于 MinDelay={strategy.MinDelay:hh\\:mm\\:ss\\.fff}");
            Assert.True(delay <= strategy.MaxDelay,
                $"n={n} 的延迟 {delay:hh\\:mm\\:ss\\.fff} 不应大于 MaxDelay={strategy.MaxDelay:hh\\:mm\\:ss\\.fff}");
        }
    }

    [Fact]
    public void GetDelay_WhenMinDelayGreaterThanMaxDelay_DoesNotThrow()
    {
        // 旧实现用 Math.Clamp(v, min, max)：min > max 时会抛 ArgumentException。
        // net472 没有 Math.Clamp，改为手写收敛后，这种错误配置不再是异常路径。
        // 这里把“不抛异常且返回 MinDelay”这一现状钉住。
        var strategy = new DefaultTagGrpRunnerRetryStrategy
        {
            MinDelay = TimeSpan.FromSeconds(10),
            MaxDelay = TimeSpan.FromSeconds(1),
        };

        var delay = strategy.GetDelay(1);

        Assert.Equal(strategy.MinDelay, delay);
    }
}
