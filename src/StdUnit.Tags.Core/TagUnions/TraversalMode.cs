namespace StdUnit.Tags;

/// <summary>
/// 遍历测点树的行为模式，
/// <see cref="ITagGrp.IsEnabled"/> 和 <see cref="ITagCbnt.IsEnabled"/>
/// </summary>
public enum TraversalMode
{
    /// <summary>
    /// 不检查使能：与历史行为一致，整棵子树照常读写。<br/>
    /// 手动调用（<c>tagUnion.ReadAsync(ct)</c> / <c>tagUnion.WriteAsync(ct)</c>）使用本模式，
    /// 因此被禁用的分组/组合依然可以手动读写——就像 PLC 里下了使能，人工仍可点动一样。
    /// </summary>
    IgnoreEnabled = 0,

    /// <summary>
    /// 检查使能：<b>自顶向下</b>判定，父节点未使能时其整棵子树都不再被考虑。<br/>
    /// 自动轮询（<c>TagGrpRunner</c>）使用本模式。
    /// </summary>
    RespectEnabled = 1,
}
