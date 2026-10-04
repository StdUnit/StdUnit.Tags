namespace StdUnit.Tags;

/// <summary>
/// 遍历式访问者
/// </summary>
public abstract class TraversingVisitorBase : ITagUnionVisitor
{
    /// <inheritdoc/>
    public void Visit(TagUnion.TagGrp grp)
    {
        this.Process(grp.Value);
        foreach (var kvp in grp.Value.Children)
        {
            var child = kvp.Value;
            child.Accept(this);
        }
    }

    /// <inheritdoc/>
    public void Visit(TagUnion.TagCbnt cbnt)
    {
        this.Process(cbnt.Value);

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
        this.Process(tag.Value);
    }

    /// <summary>
    /// 处理  <see cref="ITagGrp"/> 节点本身，不处理子节点
    /// </summary>
    /// <param name="node"></param>
    protected abstract void Process(ITagGrp node);

    /// <summary>
    /// 处理  <see cref="ITagCbnt"/> 节点本身，不处理子节点
    /// </summary>
    /// <param name="node"></param>
    protected abstract void Process(ITagCbnt node);

    /// <summary>
    /// 处理  <see cref="ITagGrp"/> 节点本身
    /// </summary>
    /// <param name="node"></param>
    protected abstract void Process(ITag node);
}
