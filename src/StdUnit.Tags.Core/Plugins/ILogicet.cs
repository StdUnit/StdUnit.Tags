namespace StdUnit.Tags;


/// <summary>
/// 逻辑小组件，用于对测点添加逻辑
/// </summary>
public interface ILogicet
{
    /// <summary>
    /// 运行顺序
    /// </summary>
    int Order { get; }

    /// <summary>
    /// 通道
    /// </summary>
    IReadOnlyList<ITagChannel> Channels { get; }

    /// <summary>
    /// 测点
    /// </summary>
    ITagGrp Tags { get; }

    /// <summary>
    /// 启用？
    /// </summary>
    bool Enabled { get; }

    /// <summary>
    /// 是否能匹配入口？返回true表示应该处理当前entry，否则应该跳过处理
    /// </summary>
    /// <param name="entry"></param>
    /// <returns></returns>
    bool MatchEntry(ITagGrp entry);

    /// <summary>
    /// 处理
    /// </summary>
    /// <returns></returns>
    Task ProcessAsync(ITagGrp entry, ITagChannel? thisChannel);
}
