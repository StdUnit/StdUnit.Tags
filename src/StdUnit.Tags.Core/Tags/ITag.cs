namespace StdUnit.Tags;

/// <summary>
/// 同步事件参数
/// </summary>
public class TagSyncEventArgs : EventArgs
{
    /// <summary>
    /// 同步类型
    /// </summary>
    public enum Kinds
    {
        /// <summary>
        /// 空
        /// </summary>
        None = 0,
        /// <summary>
        /// 读取
        /// </summary>
        Read = 1,
        /// <summary>
        /// 写入
        /// </summary>
        Written = 2,
    }

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="newValue"></param>
    /// <param name="timestamp"></param>
    /// <param name="kind"></param>
    public TagSyncEventArgs(object? newValue, DateTime timestamp, Kinds kind)
    {
        this.NewValue = newValue;
        this.Timestamp = timestamp;
        Kind = kind;
    }

    /// <summary>
    /// 新值
    /// </summary>
    public object? NewValue { get; set; }
    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; }
    /// <summary>
    /// 种类
    /// </summary>
    public Kinds Kind { get; set; }
}


/// <summary>
/// 测点同步事件委托
/// </summary>
/// <param name="sender"></param>
/// <param name="e"></param>
public delegate void TagSyncEventHandler(ITag sender, TagSyncEventArgs e);

/// <summary>
/// 测点接口
/// </summary>
public interface ITag
{
    /// <summary>
    /// 测点描述
    /// </summary>
    public TagDescriptor TagDescriptor { get; set; }

    /// <summary>
    /// 测点值 
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// 在每次从底层读取后，触发事件
    /// </summary>
    public event TagSyncEventHandler OnTagRead;

    /// <summary>
    /// 在每次向底层写入后，触发事件
    /// </summary>
    public event TagSyncEventHandler OnTagWritten;

    /// <summary>
    /// 是否被扫描过
    /// </summary>
    public bool IsScaned { get; set; }

    /// <summary>
    /// 标识数据是否发生变化，如果发生变化，会在输出阶段刷写到硬件底层
    /// </summary>
    public bool IsDirty { get; set; }

    /// <summary>
    /// 测点通道<br/>
    /// 本属性可能为null，如果需要冒泡式获取，可以使用 <see cref="ITagExtensions.SearchRequiredChannel"/> 扩展方法。<br/>
    /// </summary>
    public ITagChannel? Channel { get; }

    /// <summary>
    /// 父容器
    /// </summary>
    public TagContainer? Parent { get; set; }

    /// <summary>
    /// 从底层中读取测点值
    /// </summary>
    /// <returns></returns>
    public Task ReadAsync(CancellationToken ct);

    /// <summary>
    /// 把当前测点值刷到底层
    /// </summary>
    /// <returns></returns>
    public Task WriteAsync(CancellationToken ct);
}
