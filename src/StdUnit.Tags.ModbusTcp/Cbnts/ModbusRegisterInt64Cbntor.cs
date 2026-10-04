namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// Modbus 字空间的 Int64 组合子：占用 4 个寄存器。<br/>
/// EndianKind 描述寄存器顺序：BigEndian（Modbus 惯例默认）= 高寄存器在前；LittleEndian = 低寄存器在前。
/// </summary>
internal class ModbusRegisterInt64Cbntor : ModbusRegisterCbntorBase
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <param name="tagCbnt">Modbus 字空间组合（寄存器缓存）</param>
    /// <param name="tagOffset">字节偏移（必须为偶数）</param>
    /// <param name="isReadOnly">是否只读（输入寄存器）</param>
    internal ModbusRegisterInt64Cbntor(TagDescriptor tagDescriptor, TagCbnt<ushort> tagCbnt, int tagOffset, bool isReadOnly)
        : base(tagDescriptor, tagCbnt, tagOffset, isReadOnly)
    {
    }

    /// <summary>
    /// 测点值
    /// </summary>
    public override object? Value
    {
        get => (long)this.ReadBits();
        set
        {
#pragma warning disable CS8605 // Unboxing a possibly null value.
            this.WriteBits((ulong)(long)value);
#pragma warning restore CS8605 // Unboxing a possibly null value.
        }
    }

    protected ulong ReadBits()
    {
        var span = this.RegCache.Span.Slice(this.RegOffset, 4);
        return this.TagEndian() == EndianKinds.BigEndian
            ? ((ulong)span[0] << 48) | ((ulong)span[1] << 32) | ((ulong)span[2] << 16) | span[3]
            : ((ulong)span[3] << 48) | ((ulong)span[2] << 32) | ((ulong)span[1] << 16) | span[0];
    }

    protected void WriteBits(ulong bits)
    {
        this.EnsureWritable();
        var span = this.RegCache.Span.Slice(this.RegOffset, 4);
        if (this.TagEndian() == EndianKinds.BigEndian)
        {
            span[0] = (ushort)(bits >> 48);
            span[1] = (ushort)(bits >> 32);
            span[2] = (ushort)(bits >> 16);
            span[3] = (ushort)bits;
        }
        else
        {
            span[0] = (ushort)bits;
            span[1] = (ushort)(bits >> 16);
            span[2] = (ushort)(bits >> 32);
            span[3] = (ushort)(bits >> 48);
        }
        this.Timestamp = DateTime.Now;
        this.MarkDirty();
    }
}

/// <summary>
/// Modbus 字空间的 UInt64 组合子：占用 4 个寄存器。
/// </summary>
internal class ModbusRegisterUInt64Cbntor : ModbusRegisterInt64Cbntor
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <param name="tagCbnt">Modbus 字空间组合（寄存器缓存）</param>
    /// <param name="tagOffset">字节偏移（必须为偶数）</param>
    /// <param name="isReadOnly">是否只读（输入寄存器）</param>
    internal ModbusRegisterUInt64Cbntor(TagDescriptor tagDescriptor, TagCbnt<ushort> tagCbnt, int tagOffset, bool isReadOnly)
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
            this.WriteBits((ulong)value);
#pragma warning restore CS8605 // Unboxing a possibly null value.
        }
    }
}
