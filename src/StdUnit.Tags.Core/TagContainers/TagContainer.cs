namespace StdUnit.Tags;


/// <summary>
/// Tag容器： <see cref="ITagCbnt"/> | <see cref="ITagGrp"/>
/// </summary>
public abstract record TagContainer
{
    /// <summary>
    /// 私有构造函数，禁止外部扩展。<br/>
    /// </summary>
    private TagContainer() { }

    private sealed record TagCbnt(ITagCbnt Value) : TagContainer();

    private sealed record TagGrp(ITagGrp Value) : TagContainer();

    /// <summary>
    /// 工厂函数
    /// </summary>
    /// <param name="cbnt"></param>
    /// <returns></returns>
    public static TagContainer From(ITagCbnt cbnt) => new TagCbnt(cbnt);

    /// <summary>
    /// 工厂函数
    /// </summary>
    /// <param name="grp"></param>
    /// <returns></returns>
    public static TagContainer From(ITagGrp grp) => new TagGrp(grp);

    /// <summary>
    /// 是否是 <see cref="ITagGrp"/>？<br/>
    /// </summary>
    public bool IsTagGrp => this is TagGrp;
    /// <summary>
    /// 是否是 <see cref="ITagCbnt"/>？<br/>
    /// </summary>
    public bool IsTagCbnt => this is TagCbnt;

    /// <summary>
    /// 根据当前实例的实际类型，调用不同的处理函数，映射出不同的结果<br/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="handleTagCbnt"></param>
    /// <param name="handleTagGrp"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public T Map<T>(Func<ITagCbnt, T> handleTagCbnt, Func<ITagGrp, T> handleTagGrp) => this switch
    {
        TagCbnt(ITagCbnt Value) => handleTagCbnt(Value),
        TagGrp(ITagGrp Value) => handleTagGrp(Value),
        _ => throw new NotImplementedException($"未预料到的{nameof(TagContainer)}子类型: {this.GetType()}")
    };
}
