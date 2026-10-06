using StdUnit.Tags.ModbusTcp;

namespace StdUnit.Tags.Hjzk;

/// <summary>
/// Hjzk 测点工厂
/// </summary>
public class HjzkTagFactory : TagCbntorFactoryBase
{
    private readonly HjzkCbntBuilderBase _cbntBuilder;


    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="builder"></param>
    public HjzkTagFactory(HjzkCbntBuilderBase builder) : base(builder)
    {
        this._cbntBuilder = builder;
    }

    /// <summary>
    /// 创建 DI 测点
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <returns></returns>
    /// <exception cref="TagsProjectAddressException">地址不是合法的 Hjzk DI 引脚地址</exception>
    public virtual DITagCbntor CreateDITag(TagDescriptor tagDescriptor)
    {
        // normalize the tagsize
        if (tagDescriptor.TagSize == 0)
        {
            tagDescriptor.TagSize = 1;
        }
        var tagAddr = PinAddrUtils.TryParseDI(tagDescriptor.RawAddress, out var addr) ?
            addr :
            throw new TagsProjectAddressException(
                $"Hjzk DI 地址非法：{tagDescriptor.RawAddress}（期望形如 DI1/DI2 ... 的引脚地址）",
                $"Tag({tagDescriptor.TagName})");
        tagDescriptor.NormalizedAddress = addr.ToModbusTcpAddr(this._cbntBuilder.Slave);

        var startAddr = DIPinAddr.DI1;
        var offset = (int)tagAddr - (int)startAddr;
        return new DITagCbntor(tagDescriptor, this._cbntBuilder.TypedCbnt, offset);
    }


    /// <summary>
    /// 创建 DO 测点
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <returns></returns>
    /// <exception cref="TagsProjectAddressException">地址不是合法的 Hjzk DO 引脚地址</exception>
    public virtual DOTagCbntor CreateDOTag(TagDescriptor tagDescriptor)
    {
        // normalize the tagsize
        if (tagDescriptor.TagSize == 0)
        {
            tagDescriptor.TagSize = 1;
        }
        var tagAddr = PinAddrUtils.TryParseDO(tagDescriptor.RawAddress, out var addr) ?
            addr :
            throw new TagsProjectAddressException(
                $"Hjzk DO 地址非法：{tagDescriptor.RawAddress}（期望形如 DO1/DO2 ... 的引脚地址）",
                $"Tag({tagDescriptor.TagName})");
        tagDescriptor.NormalizedAddress = addr.ToModbusTcpAddr(this._cbntBuilder.Slave);

        var startAddr = DOPinAddr.DO1;
        var offset = (int)tagAddr - (int)startAddr;
        return new DOTagCbntor(tagDescriptor, this._cbntBuilder.TypedCbnt, offset);
    }

    /// <summary>
    /// 创建测点
    /// </summary>
    /// <param name="descriptor"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public override ITagCbntor CreateTag(TagDescriptor descriptor)
    {
        var tag = descriptor.TagKind switch
        {
            BuiltinTagKinds.DI => this.CreateDITag(descriptor) as ITagCbntor,
            BuiltinTagKinds.DO => this.CreateDOTag(descriptor) as ITagCbntor,
            _ => throw new TagsProjectConfigurationException(
                $"Hjzk 驱动未预料到的测点种类={descriptor.TagKind}（只支持 DI/DO）",
                $"Tag({descriptor.TagName})")
        };
        return tag;
    }

}
