namespace Itminus.Tags;


/// <summary>
/// 组合测点工厂基类。用于在测定组合构建器中创建测点组合子
/// </summary>
public abstract class TagCbntorFactoryBase
{

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="cbntBuilder"></param>
    public TagCbntorFactoryBase(TagCbntBuilderBase cbntBuilder)
    {
        CbntBuilder = cbntBuilder;
    }

    /// <summary>
    /// 测点组合构建器
    /// </summary>
    public TagCbntBuilderBase CbntBuilder { get; }

    /// <summary>
    /// 测点组合
    /// </summary>
    public ITagCbnt TagCbnt => CbntBuilder.TagCbnt;


    /// <summary>
    /// 根据描述，创建Tag<br/>
    /// 返回类型是 <see cref="ITagCbntor"/> 而非 <see cref="ITag"/>：本工厂产出的必然是组合内的测点子项。<br/>
    /// 这也避免了派生类用协变返回类型重写——协变返回类型需要 .NET 5+ 运行时支持，net472 下会报 CS8830。
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <returns></returns>
    public abstract ITagCbntor CreateTag(TagDescriptor tagDescriptor);


}
