using StdUnit.Tags.ModbusTcp;

namespace StdUnit.Tags.ZLan;

/// <summary>
/// ZLan 测点工厂：把 DI/DO 直连测点绑定到所在位空间组合（<see cref="ZLanDICbntBuilder"/> / <see cref="ZLanDOCbntBuilder"/>）的槽位上，
/// 并把针脚名（如 <c>DI3</c>）归一化为 Modbus 地址串。
/// </summary>
public class ZLanTagFactory : TagCbntorFactoryBase
{
    private readonly ZLanCbntBuilderBase _cbntBuilder;

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="builder">测点组合构建器（提供从站号与组合实例）</param>
    public ZLanTagFactory(ZLanCbntBuilderBase builder) : base(builder)
    {
        this._cbntBuilder = builder;
    }

    /// <summary>
    /// 创建 DI 直连测点：<see cref="TagDescriptor.TagSize"/> 为 0 时补 1，针脚名解析为相对 <see cref="DIPinAddr.DI1"/> 的偏移。
    /// </summary>
    /// <param name="tagDescriptor">测点描述符</param>
    /// <returns>DI 测点</returns>
    public virtual DITagCbntor CreateDITag(TagDescriptor tagDescriptor)
    {
        // normalize the tagsize
        if (tagDescriptor.TagSize == 0)
        {
            tagDescriptor.TagSize = 1;
        }
        var tagAddr = PinAddrUtils.ParseDI(tagDescriptor.RawAddress);
        tagDescriptor.NormalizedAddress = tagAddr.ToModbusTcpAddr(this._cbntBuilder.Slave);
        var startAddr = DIPinAddr.DI1;
        var offset = (int)tagAddr - (int)startAddr;
        return new DITagCbntor(tagDescriptor, this._cbntBuilder.TypedCbnt, offset);
    }


    /// <summary>
    /// 创建 DO 直连测点：<see cref="TagDescriptor.TagSize"/> 为 0 时补 1，针脚名解析为相对 <see cref="DOPinAddr.DO1"/> 的偏移。
    /// </summary>
    /// <param name="tagDescriptor">测点描述符</param>
    /// <returns>DO 测点</returns>
    public virtual DOTagCbntor CreateDOTag(TagDescriptor tagDescriptor)
    {
        // normalize the tagsize
        if (tagDescriptor.TagSize == 0)
        {
            tagDescriptor.TagSize = 1;
        }
        var tagAddr = PinAddrUtils.ParseDO(tagDescriptor.RawAddress);
        tagDescriptor.NormalizedAddress = tagAddr.ToModbusTcpAddr(this._cbntBuilder.Slave);
        var startAddr = DOPinAddr.DO1;
        var offset = (int)tagAddr - (int)startAddr;
        return new DOTagCbntor(tagDescriptor, this._cbntBuilder.TypedCbnt, offset);
    }

    /// <summary>
    /// 按测点种类分发：DI → <see cref="ZLanTagFactory.CreateDITag"/>，DO → <see cref="ZLanTagFactory.CreateDOTag"/>。
    /// </summary>
    /// <param name="descriptor">测点描述符</param>
    /// <returns>测点</returns>
    /// <exception cref="TagsProjectConfigurationException">测点种类不是 DI / DO</exception>
    public override ITagCbntor CreateTag(TagDescriptor descriptor)
    {
        var tag = descriptor.TagKind switch
        {
            BuiltinTagKinds.DI => this.CreateDITag(descriptor) as ITagCbntor,
            BuiltinTagKinds.DO => this.CreateDOTag(descriptor) as ITagCbntor,
            _ => throw new TagsProjectConfigurationException(
                $"ZLan 驱动未预料到的测点种类={descriptor.TagKind}（只支持 DI/DO）",
                $"Tag({descriptor.TagName})")
        };
        return tag;
    }
}
