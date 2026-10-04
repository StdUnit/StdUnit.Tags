namespace StdUnit.Tags;

/// <summary>
/// <see cref="TagUnion"/> 的访问者
/// </summary>
public interface ITagUnionVisitor
{
    /// <summary>
    /// 访问 <see cref="ITagGrp"/>节点
    /// </summary>
    void Visit(TagUnion.TagGrp grp);

    /// <summary>
    /// 访问 <see cref="ITagGrp"/>节点
    /// </summary>
    void Visit(TagUnion.TagCbnt cbnt);

    /// <summary>
    /// 访问 <see cref="ITag"/>节点
    /// </summary>
    void Visit(TagUnion.TagUnit tag);
}
