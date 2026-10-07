namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// Modbus 字空间（保持寄存器/输入寄存器）Cbnt 构建器。<br/>
/// 与 <see cref="ModbusBitTagCbntBuilder"/>（位空间）配对：本构建器只服务于寄存器空间（3x/4x），
/// 缓存为寄存器数组（<see cref="ModbusRegisterTagCbnt"/>，T=ushort）。
/// </summary>
public class ModbusRegisterTagCbntBuilder : TagCbntBuilderBase
{
    /// <summary>
    /// c'tor<br/>
    /// 需要额外使用 <c>WithCbntDescriptor()</c> 设置实际描述符。
    /// </summary>
    public ModbusRegisterTagCbntBuilder()
        : this(new ModbusRegisterTagCbnt(new TagCbntDescriptor { Name = "unkown_modbustcp_register_cbnt_name", StartAddress = "unknown_modbustcp_register_cbnt_start_address" }))
    {
    }

    private readonly ModbusRegisterTagCbnt _cbnt;

    internal ModbusRegisterTagCbntBuilder(ModbusRegisterTagCbnt cbnt) : base(cbnt)
    {
        this._cbnt = cbnt;
    }

    /// <summary>
    /// 所属组合的强类型引用。
    /// </summary>
    internal ModbusRegisterTagCbnt TypedCbnt => this._cbnt;

    /// <summary>
    /// 从站站号。<br/>
    /// 写了 <c>TagCbnt</c> 的 <c>slave</c> 属性时，本构建器会把它合成进组合的起始地址（如 <c>address="40001" slave="2"</c> 等同于 <c>address="2~40001"</c>）。
    /// </summary>
    public virtual byte Slave { get; protected set; } = 1;


    /// <inheritdoc/>
    public override TagCbntBuilderBase WithCbntDescriptor(TagCbntDescriptor descriptor)
    {
        var hasSlave = ModbusCbntSlaveAddress.TryGetSlave(descriptor, out var slave);
        if (hasSlave)
        {
            this.Slave = slave;
        }
        base.WithCbntDescriptor(descriptor);
        if (hasSlave)
        {
            this.TagCbnt.StartAddress = ModbusCbntSlaveAddress.WithSlave(this.TagCbnt.StartAddress, slave);
        }
        return this;
    }

    /// <summary>
    /// 当前组合是否寄存器空间（3x/4x）——由起始地址解析。<br/>
    /// 本构建器只服务寄存器空间，位空间（0x/1x）请使用 <see cref="ModbusBitTagCbntBuilder"/>。
    /// </summary>
    internal bool IsRegisterArea()
    {
        var addr = ModBusTcpAddressParser.Parse(this.TagCbnt.StartAddress);
        return addr.Area == RegisterKinds.HoldingRegisters || addr.Area == RegisterKinds.InputRegisters;
    }

    /// <inheritdoc/>
    protected override ITagCbntor Fallback(TagDescriptor descriptor, ITagChannel channel)
    {
        var tagFactory = new ModbusRegisterTagFactory(this, this.TypedCbnt);
        return tagFactory.CreateTag(descriptor);
    }

    /// <inheritdoc/>
    protected override TagCbntBuilderBase AutoLayout()
    {
        var cacheSize = 0;
        foreach (var kvp in this.TagCbnt.Children)
        {
            var tag = kvp.Value;
            var occupied = tag.TagOffset + tag.TagDescriptor.TagSize;
            if (occupied > cacheSize)
            {
                cacheSize = occupied;
            }
        }
        this._cbnt.ResizeCache(cacheSize);
        return this;
    }
}
