namespace StdUnit.Tags;

/// <summary>
/// 代表一组测点群组。群组内的各个测点是松散的，可能共享通信通道，也可能不共享通信通道。<br/>
/// 由于这种性质，群组中的测点既不会被统一读，也不会被统一写，它们的读或写往往意味着多次IO交互。<br/>
/// </summary>
public class TagGrp : ITagGrp
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="channel"></param>
    public TagGrp(TagGrpDescriptor descriptor, ITagChannel? channel)
    {
        Descriptor = descriptor;
        IsEnabled = descriptor.IsEnabled;
        this.Channel = channel;
    }

    /// <inheritdoc/>
    public TagGrpDescriptor Descriptor { get; set; }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public ITagChannel? Channel { get; set; }

    /// <inheritdoc/>
    public ITagGrp? Parent { get; set; }

    #region 子节点

    /// <inheritdoc/>
    public IDictionary<string, TagUnion> Children { get; } = new Dictionary<string, TagUnion>();

    /// <inheritdoc/>
    public TagUnion this[string tagName] => Children.TryGetValue(tagName, out var tag) ?
        tag :
        throw new Exception($"TagGrp({this.TagName()}) has no child who's name={tagName}");

    /// <summary>
    /// 获取子节点，支持路径访问，例如：`"tagGrp1/tagGrp2/tagCbnt1"`<br/>
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public virtual TagUnion Descendant(string path)
    {
        var segments = path.Split('/');
        if (segments.Length == 0)
        {
            throw new Exception($"invalid tag path={path}");
        }
        TagUnion tagunion = this[segments[0]];
        for (int idx = 1; idx < segments.Length; idx++)
        {
            var segment = segments[idx];
            tagunion = tagunion[segment];
        }
        return tagunion;
    }


    /// <summary>
    /// 增加测点
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    public virtual ITagGrp AddTag(ITag tag)
    {
        if (tag.Parent is null)
        {
            tag.Parent = TagContainer.From(this);
        }
        else
        {
            var parent = tag.Parent.Map(
                cbnt => TagContainer.From(this),
                grp => grp.Equals(this) ? tag.Parent : TagContainer.From(this)
                );
            tag.Parent = parent;
        }
        this.Children.Add(tag.TagName(), new TagUnion.TagUnit(tag));
        return this;
    }

    /// <summary>
    /// 增加测点
    /// </summary>
    /// <param name="tagCbnt"></param>
    /// <returns></returns>
    public virtual ITagGrp AddTag(ITagCbnt tagCbnt)
    {
        if (tagCbnt.Parent is null)
        {
            tagCbnt.Parent = this;
        }
        else if (!tagCbnt.Parent.Equals(this))
        {
            tagCbnt.Parent = this;
        }

        this.Children.Add(tagCbnt.TagName(), new TagUnion.TagCbnt(tagCbnt));
        return this;
    }

    /// <summary>
    /// 增加测点
    /// </summary>
    /// <param name="tagGrp"></param>
    /// <returns></returns>
    public virtual ITagGrp AddTag(ITagGrp tagGrp)
    {
        tagGrp.Parent = this;
        this.Children.Add(tagGrp.TagName(), new TagUnion.TagGrp(tagGrp));
        return this;
    }
    #endregion

    /// <inheritdoc/>
    public bool IsEnabled { get; set; }

    /// <inheritdoc/>
    public async Task ReadAsync(CancellationToken ct)
    {
        foreach (var kvp in Children)
        {
            var tagunion = kvp.Value;
            await tagunion.ReadAsync(ct);
        }
    }


    /// <inheritdoc/>
    public async Task WriteAsync(CancellationToken ct)
    {
        foreach (var kvp in Children)
        {
            var tagunion = kvp.Value;
            await tagunion.WriteAsync(ct);
        }
    }

    /// <inheritdoc/>
    public bool IsDirty()
    {
        foreach (var kvp in Children)
        {
            var child = kvp.Value;
            if (child.IsDirty())
            {
                return true;
            }
        }
        return false;
    }
}
