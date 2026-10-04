namespace StdUnit.Tags.ModbusTcp;


/// <summary>
/// ModbusTcp 位空间（线圈 0x / 离散输入 1x）测点工厂。<br/>
/// 只能创建位空间测点（DI/DO）；字空间（寄存器 3x/4x）的 BIT/BYTE/INT16/.../FLOAT 请使用 <see cref="ModbusRegisterTagFactory"/>。
/// </summary>
public class ModbusBitTagFactory : TagCbntorFactoryBase
{
    /// <summary>
    /// c'tor（强类型绑定）
    /// </summary>
    internal ModbusBitTagFactory(TagCbntBuilderBase builder, ModbusBitTagCbnt cbnt) : base(builder)
    {
        this._cbnt = cbnt;
    }

    private readonly ModbusBitTagCbnt _cbnt;

    /// <summary>
    /// 所属组合的强类型引用（bool 缓存）。
    /// </summary>
    internal ModbusBitTagCbnt TypedCbnt => this._cbnt;

    #region
    /// <summary>
    /// 创建DI测点
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public virtual DITagCbntor CreateDITag(TagDescriptor tagDescriptor)
    {
        // normalize the tagsize
        if (tagDescriptor.TagSize == 0)
        {
            tagDescriptor.TagSize = 1;
        }

        var tagAddr = ModBusTcpAddressParser.Parse(tagDescriptor.RawAddress);
        var groupAddr = ModBusTcpAddressParser.Parse(TagCbnt.StartAddress);

        if (tagAddr.Area != RegisterKinds.InputContacts)
        {
            throw new Exception($"地址区域{tagAddr.Area}不可作为DI测点");
        }
        var offset = tagAddr.StartPoint - groupAddr.StartPoint;
        return new DITagCbntor(tagDescriptor, TypedCbnt, offset);
    }

    /// <summary>
    /// 创建DO测点
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public virtual DOTagCbntor CreateDOTag(TagDescriptor tagDescriptor)
    {
        // normalize the tagsize
        if (tagDescriptor.TagSize == 0)
        {
            tagDescriptor.TagSize = 1;
        }

        var tagAddr = ModBusTcpAddressParser.Parse(tagDescriptor.NormalizedAddress);
        var groupAddr = ModBusTcpAddressParser.Parse(TagCbnt.StartAddress);

        if (tagAddr.Area != RegisterKinds.OutputCoils)
        {
            throw new Exception($"地址区域{tagAddr.Area}不可作为DO测点");
        }
        var offset = tagAddr.StartPoint - groupAddr.StartPoint;
        return new DOTagCbntor(tagDescriptor, TypedCbnt, offset);
    }
    #endregion

    /// <inheritdoc/>
    public override ITagCbntor CreateTag(TagDescriptor descriptor)
    {
        var tag = descriptor.TagKind switch
        {
            // 1000x 离散输入
            BuiltinTagKinds.DI => CreateDITag(descriptor) as ITagCbntor,
            // 0000x 线圈输出
            BuiltinTagKinds.DO => CreateDOTag(descriptor) as ITagCbntor,
            _ => throw new Exception($"位空间组合不支持测点种类={descriptor.TagKind}（BIT/BYTE/INT16/.../FLOAT 属于寄存器空间，请使用 {nameof(ModbusRegisterTagFactory)}）")
        };
        return tag;
    }

}
