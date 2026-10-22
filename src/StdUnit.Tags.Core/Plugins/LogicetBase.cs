namespace StdUnit.Tags;

/// <summary>
/// 逻辑小组件基类
/// </summary>
public abstract class LogicetBase : ILogicet
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="project">逻辑组件所属的测点项目</param>
    /// <exception cref="ArgumentNullException">project 为 null</exception>
    public LogicetBase(ITagsProject project)
    {
        this.Project = project ?? throw new ArgumentNullException(nameof(project), "构造逻辑组件时项目不可为空");
    }

    /// <inheritdoc/>
    public ITagsProject Project { get; }

    /// <summary>
    /// 项目的通道列表，等价于 <c>Project.Channels</c>。<br/>
    /// </summary>
    public virtual IReadOnlyList<ITagChannel> Channels => this.Project.Channels;

    /// <summary>
    /// 项目的测点组，等价于 <c>Project.Tags</c>。<br/>
    /// </summary>
    public virtual ITagGrp Tags => this.Project.Tags;

    /// <inheritdoc/>
    public abstract int Order { get; }

    /// <inheritdoc/>
    public abstract bool MatchEntry(ITagGrp entry);

    /// <inheritdoc/>
    public virtual bool Enabled { get; set; } = true;

    /// <inheritdoc/>
    public abstract Task ProcessAsync(ITagGrp entry, ITagChannel? thisChannel, CancellationToken ct = default);
}
