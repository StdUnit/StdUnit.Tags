namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// Modbus 字空间的 Int32 组合子：占用 2 个寄存器。<br/>
/// EndianKind 描述寄存器顺序：BigEndian（Modbus 惯例默认）= 高寄存器在前；LittleEndian = 低寄存器在前。
/// </summary>
internal class ModbusRegisterInt32Cbntor : ModbusRegisterCbntorBase
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <param name="tagCbnt">Modbus 字空间组合（寄存器缓存）</param>
    /// <param name="tagOffset">字节偏移（必须为偶数）</param>
    /// <param name="isReadOnly">是否只读（输入寄存器）</param>
    internal ModbusRegisterInt32Cbntor(TagDescriptor tagDescriptor, TagCbnt<ushort> tagCbnt, int tagOffset, bool isReadOnly)
        : base(tagDescriptor, tagCbnt, tagOffset, isReadOnly)
    {
    }

    /// <summary>
    /// 测点值
    /// </summary>
    public override object? Value
    {
        get => (int)this.ReadBits();
        set
        {
#pragma warning disable CS8605 // Unboxing a possibly null value.
            this.WriteBits((uint)(int)value);
#pragma warning restore CS8605 // Unboxing a possibly null value.
        }
    }

    protected uint ReadBits()
    {
        var span = this.RegCache.Span.Slice(this.RegOffset, 2);
        return this.TagEndian() == EndianKinds.BigEndian
            ? (uint)((span[0] << 16) | span[1])
            : (uint)((span[1] << 16) | span[0]);
    }

    protected void WriteBits(uint bits)
    {
        this.EnsureWritable();
        var span = this.RegCache.Span.Slice(this.RegOffset, 2);
        if (this.TagEndian() == EndianKinds.BigEndian)
        {
            span[0] = (ushort)(bits >> 16);
            span[1] = (ushort)bits;
        }
        else
        {
            span[0] = (ushort)bits;
            span[1] = (ushort)(bits >> 16);
        }
        this.Timestamp = DateTime.Now;
        this.MarkDirty();
    }
}

/// <summary>
/// Modbus 字空间的 UInt32 组合子：占用 2 个寄存器。
/// </summary>
internal class ModbusRegisterUInt32Cbntor : ModbusRegisterInt32Cbntor
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <param name="tagCbnt">Modbus 字空间组合（寄存器缓存）</param>
    /// <param name="tagOffset">字节偏移（必须为偶数）</param>
    /// <param name="isReadOnly">是否只读（输入寄存器）</param>
    internal ModbusRegisterUInt32Cbntor(TagDescriptor tagDescriptor, TagCbnt<ushort> tagCbnt, int tagOffset, bool isReadOnly)
        : base(tagDescriptor, tagCbnt, tagOffset, isReadOnly)
    {
    }

    /// <summary>
    /// 测点值
    /// </summary>
    public override object? Value
    {
        get => this.ReadBits();
        set
        {
#pragma warning disable CS8605 // Unboxing a possibly null value.
            this.WriteBits((uint)value);
#pragma warning restore CS8605 // Unboxing a possibly null value.
        }
    }
}

/// <summary>
/// Modbus 字空间的 Float 组合子：占用 2 个寄存器（IEEE754 单精度，bit 组合）。
/// </summary>
internal class ModbusRegisterFloatCbntor : ModbusRegisterInt32Cbntor
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <param name="tagCbnt">Modbus 字空间组合（寄存器缓存）</param>
    /// <param name="tagOffset">字节偏移（必须为偶数）</param>
    /// <param name="isReadOnly">是否只读（输入寄存器）</param>
    internal ModbusRegisterFloatCbntor(TagDescriptor tagDescriptor, TagCbnt<ushort> tagCbnt, int tagOffset, bool isReadOnly)
        : base(tagDescriptor, tagCbnt, tagOffset, isReadOnly)
    {
    }

    /// <summary>
    /// 测点值
    /// </summary>
    public override object? Value
    {
        get => Compat.FloatBitsCompat.ToSingle(this.ReadBits());
        set
        {
#pragma warning disable CS8605 // Unboxing a possibly null value.
            var f = (float)value;
#pragma warning restore CS8605 // Unboxing a possibly null value.
            this.WriteBits(Compat.FloatBitsCompat.ToBits(f));
        }
    }
}
