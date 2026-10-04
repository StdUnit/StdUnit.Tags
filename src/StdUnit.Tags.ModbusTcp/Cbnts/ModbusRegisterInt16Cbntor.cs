namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// Modbus 字空间的 Int16 组合子：占用 1 个寄存器，缓存元素即寄存器数值（NModbus 已按线序解析），直接取值、无字节序分支。
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
        get => (short)this.RegCache.Span[this.RegOffset];
        set
        {
#pragma warning disable CS8605 // Unboxing a possibly null value.
            var data = (short)value;
#pragma warning restore CS8605 // Unboxing a possibly null value.
            this.EnsureWritable();
            this.RegCache.Span[this.RegOffset] = (ushort)data;
            this.Timestamp = DateTime.Now;
            this.MarkDirty();
        }
    }
}

/// <summary>
/// Modbus 字空间的 UInt16 组合子：占用 1 个寄存器，直接取值。
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
        get => this.RegCache.Span[this.RegOffset];
        set
        {
#pragma warning disable CS8605 // Unboxing a possibly null value.
            var data = (ushort)value;
#pragma warning restore CS8605 // Unboxing a possibly null value.
            this.EnsureWritable();
            this.RegCache.Span[this.RegOffset] = data;
            this.Timestamp = DateTime.Now;
            this.MarkDirty();
        }
    }
}
