namespace StdUnit.Tags;

/// <summary>
/// <see cref="TagUnion"/> 的遍历器<br/>
/// </summary>
public class TagUnionTraverser : ITagUnionVisitor
{
    private Action<ITagGrp>? _procGrp;
    private Action<ITagCbnt>? _procCbnt;
    private Action<ITag>? _procUnit;

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="procGrp">处理  <see cref="ITagGrp"/> 节点本身，不处理子节点</param>
    /// <param name="procCbnt">处理  <see cref="ITagCbnt"/> 节点本身，不处理子节点</param>
    /// <param name="procTag">处理  <see cref="ITag"/> 节点本身</param>
    public TagUnionTraverser(Action<ITagGrp>? procGrp, Action<ITagCbnt>? procCbnt, Action<ITag>? procTag)
    {
        this._procGrp = procGrp;
        this._procCbnt = procCbnt;
        this._procUnit = procTag;
    }

    /// <inheritdoc/>
    public void Visit(TagUnion.TagGrp grp)
    {
        this._procGrp?.Invoke(grp.Value);
        foreach (var kvp in grp.Value.Children)
        {
            var child = kvp.Value;
            child.Accept(this);
        }
    }

    /// <inheritdoc/>
    public void Visit(TagUnion.TagCbnt cbnt)
    {
        this._procCbnt?.Invoke(cbnt.Value);

        foreach (var kvp in cbnt.Value.Children)
        {
            var key = kvp.Key;
            var child = cbnt[key];
            child.Accept(this);
        }
    }

    /// <inheritdoc/>
    public void Visit(TagUnion.TagUnit tag)
    {
        this._procUnit?.Invoke(tag.Value);
    }

}