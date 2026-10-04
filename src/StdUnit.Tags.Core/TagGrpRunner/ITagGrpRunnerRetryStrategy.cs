namespace StdUnit.Tags;

/// <summary>
/// 测点群组运行器（<see cref="ITagGrpRunner"/>）的重试等待策略。<br/>
/// 当轮询循环发生异常时，运行器会根据连续失败次数调用此策略计算下次重试前的等待时间，
/// 以避免错误日志洪泛。
/// </summary>
public interface ITagGrpRunnerRetryStrategy
{
    /// <summary>
    /// 根据连续失败次数计算下一次重试前的等待时间。
    /// </summary>
    /// <param name="consecutiveFailureCount">连续失败次数（>= 1）</param>
    /// <returns>下次重试前的等待时间</returns>
    TimeSpan GetDelay(int consecutiveFailureCount);
}
