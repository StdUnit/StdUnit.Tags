namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// Modbus 字空间的 Int16 组合子：占用 1 个寄存器，<c>endian</c> 描述寄存器内两个字节的顺序
/// （BigEndian 直取；LittleEndian 交换两字节，与直接测点一致）。
/// </summary>
internal class ModbusRegisterInt16Cbntor : ModbusRegisterCbntorBase
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <param name="tagCbnt">Modbus 字空间组合（寄存器缓存）</param>
    /// <param name="tagOffset">字节偏移（必须为偶数）</param>
    /// <param name="isReadOnly">是否只读（输入寄存器）</param>
    internal ModbusRegisterInt16Cbntor(TagDescriptor tagDescriptor, TagCbnt<ushort> tagCbnt, int tagOffset, bool isReadOnly)
        : base(tagDescriptor, tagCbnt, tagOffset, isReadOnly)
    {
    }

    /// <summary>
    /// 测点值
    /// </summary>
    public override object? Value
    {
        get => (short)this.ApplyEndian(this.RegCache.Span[this.RegOffset]);
        set
        {
#pragma warning disable CS8605 // Unboxing a possibly null value.
            var data = (short)value;
#pragma warning restore CS8605 // Unboxing a possibly null value.
            this.EnsureWritable();
            this.RegCache.Span[this.RegOffset] = this.ApplyEndian((ushort)data);
            this.Timestamp = DateTime.UtcNow;
            this.MarkDirty();
        }
    }
}

/// <summary>
/// Modbus 字空间的 UInt16 组合子：占用 1 个寄存器，<c>endian</c> 语义同 <see cref="ModbusRegisterInt16Cbntor"/>。
/// </summary>
internal class ModbusRegisterUInt16Cbntor : ModbusRegisterCbntorBase
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <param name="tagCbnt">Modbus 字空间组合（寄存器缓存）</param>
    /// <param name="tagOffset">字节偏移（必须为偶数）</param>
    /// <param name="isReadOnly">是否只读（输入寄存器）</param>
    internal ModbusRegisterUInt16Cbntor(TagDescriptor tagDescriptor, TagCbnt<ushort> tagCbnt, int tagOffset, bool isReadOnly)
        : base(tagDescriptor, tagCbnt, tagOffset, isReadOnly)
    {
    }

    /// <summary>
    /// 测点值
    /// </summary>
    public override object? Value
    {
        get => this.ApplyEndian(this.RegCache.Span[this.RegOffset]);
        set
        {
#pragma warning disable CS8605 // Unboxing a possibly null value.
            var data = (ushort)value;
#pragma warning restore CS8605 // Unboxing a possibly null value.
            this.EnsureWritable();
            this.RegCache.Span[this.RegOffset] = this.ApplyEndian(data);
            this.Timestamp = DateTime.UtcNow;
            this.MarkDirty();
        }
    }
}
