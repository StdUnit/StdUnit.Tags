namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// ModbusTcp Cbnt 构建器
/// </summary>
public class ModbusBitTagCbntBuilder : TagCbntBuilderBase
{
    /// <summary>
    /// c'tor<br/>
    /// 需要额外使用 <c>WithCbntDescriptor()</c> 设置实际描述符。
    /// </summary>
    public ModbusBitTagCbntBuilder()
        : this(new ModbusBitTagCbnt(new TagCbntDescriptor { Name = "unkown_modbustcp_cbnt_name", StartAddress = "unknown_modbustcp_cbnt_start_address" }))
    {
    }

    private readonly ModbusBitTagCbnt _cbnt;

    internal ModbusBitTagCbntBuilder(ModbusBitTagCbnt cbnt) : base(cbnt)
    {
        this._cbnt = cbnt;
    }

    /// <summary>
    /// 所属组合的强类型引用。
    /// </summary>
    internal ModbusBitTagCbnt TypedCbnt => this._cbnt;


    /// <summary>
    /// 从站站号。<br/>
    /// 写了 <c>TagCbnt</c> 的 <c>slave</c> 属性时，本构建器会把它合成进组合的起始地址（如 <c>address="10001" slave="2"</c> 等同于 <c>address="2~10001"</c>）。
    /// </summary>
    public virtual byte Slave { get; protected set; } = 1;

    /// <summary>
    /// 区域。<br/>
    /// 本构建器不消费该属性（Modbus 的区域由起始地址决定），它供子类使用——例如 Hjzk / ZLan 用它选择 DI / DO 构建器。
    /// </summary>
    public virtual string? Area { get; protected set; }

    /// <summary>
    /// 当前组合是否<b>位</b>空间（0x/1x，非寄存器空间）——由起始地址解析。<br/>
    /// 本构建器只服务位空间；寄存器空间（3x/4x）请使用 <see cref="ModbusRegisterTagCbntBuilder"/>。
    /// </summary>
    internal bool IsNotRegisterArea()
    {
        var addr = ModBusTcpAddressParser.Parse(this.TagCbnt.StartAddress);
        return addr.Area != RegisterKinds.HoldingRegisters && addr.Area != RegisterKinds.InputRegisters;
    }


    /// <inheritdoc/>
    public override TagCbntBuilderBase WithCbntDescriptor(TagCbntDescriptor descriptor)
    {
        var hasSlave = ModbusCbntSlaveAddress.TryGetSlave(descriptor, out var slave);
        if (hasSlave)
        {
            this.Slave = slave;
        }
        if (descriptor.Extras.TryGetValue("area", out var areaAttr))
        {
            this.Area = areaAttr.Value;
        }
        base.WithCbntDescriptor(descriptor);
        if (hasSlave)
        {
            this.TagCbnt.StartAddress = ModbusCbntSlaveAddress.WithSlave(this.TagCbnt.StartAddress, slave);
        }
        return this;
    }

    /// <inheritdoc/>
    protected override ITagCbntor Fallback(TagDescriptor descriptor, ITagChannel channel)
    {
        var tagFactory = this.MakeModbusBitTagFactory();
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