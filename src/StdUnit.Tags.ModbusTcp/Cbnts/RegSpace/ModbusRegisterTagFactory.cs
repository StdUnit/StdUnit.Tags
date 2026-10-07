namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// Modbus 字空间（保持寄存器/输入寄存器）测点工厂。<br/>
/// 只能创建寄存器解读类测点（BIT/BYTE/INT16/UINT16/INT32/UINT32/FLOAT/INT64/UINT64），
/// 位空间（线圈/离散输入）请使用 <see cref="ModbusBitTagFactory"/>。<br/>
/// 输入寄存器区域自动标记为只读（<see cref="ModbusRegisterCbntorBase.IsReadOnly"/>）。
/// </summary>
internal class ModbusRegisterTagFactory : TagCbntorFactoryBase
{
    /// <summary>
    /// c'tor（强类型绑定）
    /// </summary>
    internal ModbusRegisterTagFactory(TagCbntBuilderBase builder, ModbusRegisterTagCbnt cbnt) : base(builder)
    {
        this._cbnt = cbnt;
    }

    private readonly ModbusRegisterTagCbnt _cbnt;

    /// <summary>
    /// 所属组合的强类型引用（寄存器缓存）。
    /// </summary>
    internal ModbusRegisterTagCbnt TypedCbnt => this._cbnt;

    /// <summary>
    /// 获取测点的起始寄存器索引（相对组合起始地址）
    /// </summary>
    protected int GetRegOffset(TagDescriptor tagDescriptor)
    {
        var tagAddr = ModBusTcpAddressParser.Parse(tagDescriptor.RawAddress);
        var groupAddr = ModBusTcpAddressParser.Parse(TagCbnt.StartAddress);
        return tagAddr.StartPoint - groupAddr.StartPoint;
    }

    /// <inheritdoc/>
    public override ITagCbntor CreateTag(TagDescriptor descriptor)
    {
        // normalize the tagsize
        if (descriptor.TagSize == 0)
        {
            descriptor.TagSize = descriptor.TagKind switch
            {
                BuiltinTagKinds.BIT or BuiltinTagKinds.BYTE or BuiltinTagKinds.INT16 or BuiltinTagKinds.UINT16 => 2,
                BuiltinTagKinds.INT32 or BuiltinTagKinds.UINT32 or BuiltinTagKinds.FLOAT => 4,
                BuiltinTagKinds.INT64 or BuiltinTagKinds.UINT64 => 8,
                _ => throw new TagsProjectConfigurationException(
                    $"ModbusTcp 寄存器空间未预料到的测点种类={descriptor.TagKind}",
                    $"Tag({descriptor.TagName})"),
            };
        }

        // 只有一对字节的种类写 interpret 没有意义（interpret 描述的是多寄存器之间怎么摆）
        ModbusInterpret.RejectForSingleUnit(descriptor, $"Tag({descriptor.TagName})");

        var tagAddr = ModBusTcpAddressParser.Parse(descriptor.RawAddress);
        if (tagAddr.Area != RegisterKinds.HoldingRegisters && tagAddr.Area != RegisterKinds.InputRegisters)
        {
            throw new TagsProjectConfigurationException(
                $"地址区域{tagAddr.Area}不可作为寄存器测点（请在位空间组合中使用）",
                $"Tag({descriptor.TagName})");
        }
        var isReadOnly = tagAddr.Area == RegisterKinds.InputRegisters;
        var regOffset = GetRegOffset(descriptor);
        var tagOffset = regOffset * 2;

        ModbusRegisterCbntorBase tag = descriptor.TagKind switch
        {
            BuiltinTagKinds.BIT => new ModbusRegisterBitCbntor(descriptor, TypedCbnt, tagOffset, isReadOnly, tagAddr.NthBit),
            BuiltinTagKinds.BYTE => new ModbusRegisterByteCbntor(descriptor, TypedCbnt, tagOffset, isReadOnly),
            BuiltinTagKinds.INT16 => new ModbusRegisterInt16Cbntor(descriptor, TypedCbnt, tagOffset, isReadOnly),
            BuiltinTagKinds.UINT16 => new ModbusRegisterUInt16Cbntor(descriptor, TypedCbnt, tagOffset, isReadOnly),
            BuiltinTagKinds.INT32 => new ModbusRegisterInt32Cbntor(descriptor, TypedCbnt, tagOffset, isReadOnly),
            BuiltinTagKinds.UINT32 => new ModbusRegisterUInt32Cbntor(descriptor, TypedCbnt, tagOffset, isReadOnly),
            BuiltinTagKinds.FLOAT => new ModbusRegisterFloatCbntor(descriptor, TypedCbnt, tagOffset, isReadOnly),
            BuiltinTagKinds.INT64 => new ModbusRegisterInt64Cbntor(descriptor, TypedCbnt, tagOffset, isReadOnly),
            BuiltinTagKinds.UINT64 => new ModbusRegisterUInt64Cbntor(descriptor, TypedCbnt, tagOffset, isReadOnly),
            _ => throw new TagsProjectConfigurationException(
                $"ModbusTcp 寄存器空间未预料到的测点种类={descriptor.TagKind}",
                $"Tag({descriptor.TagName})"),
        };
        return tag;
    }
}
