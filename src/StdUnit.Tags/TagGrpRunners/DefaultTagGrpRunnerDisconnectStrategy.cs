namespace StdUnit.Tags;

/// <summary>
/// 默认的断开等待策略：有限超时等待。<br/>
/// 正常情况断开毫秒级完成（清理路径执行时底层锁已释放）；仅当有并发操作卡住底层读（持锁）时
/// 才会接近 <see cref="Timeout"/> 上限。超时后放弃等待、立即返回，后台线程仍会继续清理连接。<br/>
/// 与 fire-and-forget（完全不等待）相比，本策略能避免"断开未完成就重连/重启导致连接数超限"；
/// 与无限等待相比，本策略保证清理路径不会无限阻塞。
/// </summary>
public class DefaultTagGrpRunnerDisconnectStrategy : ITagGrpRunnerDisconnectStrategy
{
    /// <summary>等待断开完成的最大时长，默认 5s。</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <inheritdoc/>
    public async Task WaitDisconnectAsync(ITagChannel? channel, Task? disconnect)
    {
        if (disconnect is null)
        {
            return;
        }

        // 超时定时器不用 CancellationToken——断开本身是 CancellationToken.None 发起的，
        // 超时定时器若被取消会立即抛 OCE，退化成"不等待"。
        await Task.WhenAny(disconnect, Task.Delay(Timeout));
    }
}
