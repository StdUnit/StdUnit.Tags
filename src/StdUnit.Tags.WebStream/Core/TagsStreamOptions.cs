namespace StdUnit.Tags.WebStream;

/// <summary>
/// 只读观测流的配置。
/// </summary>
public sealed class TagsStreamOptions
{
    /// <summary>
    /// SSE 心跳间隔（发送一行注释，防反代/中间设备把空闲连接掐掉）。默认 15 秒。
    /// </summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// 与 <see cref="ITagsProjectCtrl"/> 对账的间隔：发现"项目已换/已停但没收到事件"后自行纠正状态。
    /// 默认 1 秒。
    /// </summary>
    public TimeSpan ReconcileInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 每个订阅者的发送队列容量（帧数）。队列满时丢弃并标记重同步，<b>绝不反压到轮询线程</b>。默认 64。
    /// </summary>
    public int PiperQueueCapacity { get; set; } = 64;

    /// <summary>
    /// 单帧最多包含多少个测点值；超出则拆成连续多帧（仅对增量帧生效，全量基线帧不拆）。默认 2000。
    /// </summary>
    public int MaxValuesPerFrame { get; set; } = 2000;

    /// <summary>
    /// 轮询线程 → 发送循环的待处理队列容量。队列满时丢弃该批并让所有订阅者重同步。默认 1024。
    /// </summary>
    public int PendingQueueCapacity { get; set; } = 1024;

    /// <summary>
    /// 写命令（改值 / 标记未扫描）等待"该入口的下一轮执行完意图"的时间上限。默认 5 秒。<br/>
    /// 入口被禁用或已停止轮询时意图不会被执行，超时后返回 504（意图仍留在队列里，恢复轮询后会执行）。
    /// </summary>
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
