namespace StdUnit.Tags;

/// <summary>
/// 固定滑动等待策略：每轮迭代后始终等待完整的扫描间隔。<br/>
/// 这是 <see cref="TagGrpRunner"/> 原有的行为。
/// </summary>
public class SlidingWaitPollDelayStrategy : ITagGrpRunnerPollDelayStrategy
{
    /// <inheritdoc/>
    public TimeSpan GetDelay(TimeSpan scanInterval, TimeSpan elapsed) => scanInterval;
}
