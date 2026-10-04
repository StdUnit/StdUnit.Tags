namespace StdUnit.Tags;

/// <summary>
/// 自适应等待策略：<br/>
/// - 如果本轮耗时已超过扫描间隔，则返回 <see cref="TimeSpan.Zero"/>（调用者应让出调度即可）；<br/>
/// - 否则返回剩余时间（<c>scanInterval - elapsed</c>）。<br/>
/// 这样在高负载或 IO 延迟波动时不会累积排队延迟。
/// </summary>
public class AdaptivePollDelayStrategy : ITagGrpRunnerPollDelayStrategy
{
    /// <inheritdoc/>
    public TimeSpan GetDelay(TimeSpan scanInterval, TimeSpan elapsed)
    {
        // 如果已超时或刚好用完时间，无需等待
        if (elapsed >= scanInterval)
        {
            return TimeSpan.Zero;
        }

        return scanInterval - elapsed;
    }
}
