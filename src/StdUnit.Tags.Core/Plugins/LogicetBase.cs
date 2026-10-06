namespace StdUnit.Tags;

/// <summary>
/// 逻辑小组件基类
/// </summary>
public abstract class LogicetBase : ILogicet
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="channels"></param>
    /// <param name="tags"></param>
    /// <exception cref="ArgumentNullException">channels 或 tags 为 null</exception>
    public LogicetBase(IReadOnlyList<ITagChannel> channels, ITagGrp tags)
    {
        this.Channels = channels ?? throw new ArgumentNullException(nameof(channels), "构造逻辑组件时通道集不可为空");
        this.Tags = tags ?? throw new ArgumentNullException(nameof(tags), "构造逻辑组件时测点集不可为空");
    }

    /// <inheritdoc/>
    public virtual IReadOnlyList<ITagChannel> Channels { get; }

    /// <inheritdoc/>
    public virtual ITagGrp Tags { get; }

    /// <inheritdoc/>
    public abstract int Order { get; }

    /// <inheritdoc/>
    public abstract bool MatchEntry(ITagGrp entry);

    /// <inheritdoc/>
    public virtual bool Enabled { get; set; } = true;

    /// <inheritdoc/>
    public abstract Task ProcessAsync(ITagGrp entry, ITagChannel? thisChannel);
}