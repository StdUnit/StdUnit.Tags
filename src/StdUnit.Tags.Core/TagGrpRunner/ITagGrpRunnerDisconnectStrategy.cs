namespace StdUnit.Tags;

/// <summary>
/// 测点群组运行器（<see cref="ITagGrpRunner"/>）的断开等待策略。<br/>
/// 轮询循环因异常或取消进入清理路径时，运行器会先断开通道再退出/重试。
/// 由于 PLC/Modbus 设备多有连接数限制（S7-1200/1500 默认仅 1 个连接、串口设备独占），
/// 若 fire-and-forget 后立刻重连或重启，上一连接可能未断开 → 连接数超限失败。
/// 本策略决定清理路径如何等待断开完成。<br/>
/// 只负责"如何等待"，不负责发起断开——发起断开由调用者负责。
/// </summary>
public interface ITagGrpRunnerDisconnectStrategy
{
    /// <summary>
    /// 等待通道断开完成。<br/>
    /// <paramref name="channel"/> 为 null 或 <paramref name="disconnect"/> 为 null 时表示无需断开，应立即返回。<br/>
    /// 实现可以：直接 await（无限等待直到断开完成）、有限超时等待、或立即返回（fire-and-forget）。
    /// </summary>
    /// <param name="channel">发生异常的通道；为 null 表示无需断开</param>
    /// <param name="disconnect">通道的 <see cref="ITagChannel.DisconnectAsync"/> 返回的 Task；为 null 表示无需断开</param>
    Task WaitDisconnectAsync(ITagChannel? channel, Task? disconnect);
}
