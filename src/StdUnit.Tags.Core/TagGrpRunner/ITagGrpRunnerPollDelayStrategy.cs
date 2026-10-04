namespace StdUnit.Tags;

/// <summary>
/// 测点群组运行器（<see cref="ITagGrpRunner"/>）的轮询间隔等待策略。<br/>
/// 只负责计算理论等待时长，不负责实际等待——执行等待由调用者负责。<br/>
/// 例如超时时返回 <see cref="TimeSpan.Zero"/>，未超时返回 <c>scanInterval - elapsed</c>。
/// </summary>
public interface ITagGrpRunnerPollDelayStrategy
{
    /// <summary>
    /// 根据扫描间隔和本轮实际耗时，计算下一次轮询前应等待的时长。<br/>
    /// 返回 <see cref="TimeSpan.Zero"/> 表示无需等待（仅让出调度即可）。
    /// </summary>
    /// <param name="scanInterval">配置的扫描间隔</param>
    /// <param name="elapsed">本轮迭代的实际耗时</param>
    /// <returns>理论等待时长</returns>
    TimeSpan GetDelay(TimeSpan scanInterval, TimeSpan elapsed);
}
