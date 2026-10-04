namespace StdUnit.Tags;

/// <summary>
/// TagUnit 遍历器，只对 TagUnit 进行遍历，忽略 TagGrp 和 TagCbnt
/// </summary>
public class TagTraverser : TraversingVisitorBase
{
    private readonly Action<ITag> _action;

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="action"></param>
    public TagTraverser(Action<ITag> action)
    {
        this._action = action;
    }

    /// <summary>
    /// do nothing, ignore TagGrp
    /// </summary>
    /// <param name="node"></param>
    protected override void Process(ITagGrp node) { }

    /// <summary>
    /// do nothing, ignore TagCbnt
    /// </summary>
    /// <param name="node"></param>
    protected override void Process(ITagCbnt node) { }

    /// <summary>
    /// invoke the action on TagUnit
    /// </summary>
    /// <param name="node"></param>
    protected override void Process(ITag node)
    {
        this._action.Invoke(node);
    }
}