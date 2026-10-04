namespace StdUnit.Tags;

/// <summary>
/// 默认的重试等待策略，基于 Logistic 函数<br/>
/// 连续失败次数较低时等待时间快速增长，
/// 达到 <see cref="Midpoint"/> 后增长趋缓，
/// 最终趋近 <see cref="MaxDelay"/>
/// </summary>
public class DefaultTagGrpRunnerRetryStrategy : ITagGrpRunnerRetryStrategy
{
    /// <summary>最小等待时间，默认 500ms</summary>
    public TimeSpan MinDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>最大等待时间，默认 30s</summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Logistic 函数的增长率 k，默认 0.8。值越大曲线越陡峭。</summary>
    public double GrowthRate { get; set; } = 0.8;

    /// <summary>Logistic 函数的中点 n₀，默认 5。此处延迟约为 (MinDelay+MaxDelay)/2。</summary>
    public int Midpoint { get; set; } = 5;

    /// <inheritdoc/>
    public TimeSpan GetDelay(int consecutiveFailureCount)
    {
        if (consecutiveFailureCount <= 0)
            return MinDelay;

        // f(x) = 1 / (1 + e^(-x))，其中 x = k * (n - n₀)
        var exponent = -GrowthRate * (consecutiveFailureCount - Midpoint);
        var ratio = 1.0 / (1.0 + Math.Exp(exponent));

        var totalMs = MinDelay.TotalMilliseconds + (MaxDelay - MinDelay).TotalMilliseconds * ratio;
        // Math.Clamp 在 net472 下不可用，此处手动收敛到 [MinDelay, MaxDelay]。
        var minMs = MinDelay.TotalMilliseconds;
        var maxMs = MaxDelay.TotalMilliseconds;
        var clamped = 
            totalMs < minMs ?  minMs : 
            totalMs > maxMs ? maxMs : totalMs;
        return TimeSpan.FromMilliseconds(clamped);
    }
}
