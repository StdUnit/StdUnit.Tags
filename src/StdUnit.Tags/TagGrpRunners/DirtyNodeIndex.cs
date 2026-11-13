using System.Collections.Generic;

namespace StdUnit.Tags;

/// <summary>
/// 入口子树的"可写节点"扁平索引，把每轮判脏从"递归走 Children + TagUnion 分发"降为"扫两个数组"。<br/>
/// 只收录 <see cref="ITag"/> 与 <see cref="ITagCbnt"/>——这两类正是写单元：<c>WriteAsync</c> 只看它们各自的
/// <c>IsDirty</c> 决定是否写；组合本身可以独立为脏（整块缓存重写），所以必须收录。<br/>
/// 树结构假定加载后不变；索引在运行器每次（重）启动时重建。
/// </summary>
internal sealed class DirtyNodeIndex
{
    private readonly ITag[] _tags;
    private readonly ITagCbnt[] _cbnts;

    private DirtyNodeIndex(ITag[] tags, ITagCbnt[] cbnts)
    {
        this._tags = tags;
        this._cbnts = cbnts;
    }

    /// <summary>
    /// 收集入口子树中的全部 Tag / Cbnt（不区分是否使能——<c>IsDirty()</c> 本来也不看使能）。<br/>
    /// 索引的正确性建立在 <see cref="ITagGrp.IsDirty()"/> 与 <c>WriteAsync</c> 的一致性契约上：
    /// "返回 false 时写不得有任何动作；只要存在待写节点就必须返回 true"。
    /// 注意它<b>不是</b>"父脏必有脏子"——组合自身的脏也在收录范围内，因此不会漏。
    /// </summary>
    public static DirtyNodeIndex Build(ITagGrp entry)
    {
        var tags = new List<ITag>();
        var cbnts = new List<ITagCbnt>();
        Collect(entry, tags, cbnts);
        return new DirtyNodeIndex(tags.ToArray(), cbnts.ToArray());
    }

    /// <summary>
    /// 是否有任一节点已脏。分两个数组扫描是为了避免对 <see cref="TagUnion"/> 做类型分发
    /// （实测 10k 节点：直接读 <c>ITag.IsDirty</c> 约 2.6 ns/点，经 <c>TagUnion</c> 约 5.2 ns/点）。
    /// </summary>
    public bool AnyDirty()
    {
        foreach (var tag in this._tags)
        {
            if (tag.IsDirty)
            {
                return true;
            }
        }

        foreach (var cbnt in this._cbnts)
        {
            if (cbnt.IsDirty)
            {
                return true;
            }
        }

        return false;
    }

    private static void Collect(ITagGrp grp, List<ITag> tags, List<ITagCbnt> cbnts)
    {
        foreach (var child in grp.Children.Values)
        {
            switch (child)
            {
                case TagUnion.TagUnit unit:
                    tags.Add(unit.Value);
                    break;
                case TagUnion.TagCbnt cbnt:
                    cbnts.Add(cbnt.Value);
                    break;
                case TagUnion.TagGrp nested:
                    Collect(nested.Value, tags, cbnts);
                    break;
            }
        }
    }
}
