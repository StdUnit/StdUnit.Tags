namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// Modbus 字空间（寄存器）的字节组合子：占用 1 个寄存器，取其中高字节或低字节。<br/>
/// <c>BigEndian</c>（标准设备：寄存器高字节在前）→ 取高字节；<c>LittleEndian</c>（描述符默认值）→ 取低字节。
/// 语义与 <see cref="ByteDirectTag"/> 一致，详见项目根目录的 Notes.md。
/// </summary>
internal class ModbusRegisterByteCbntor : ModbusRegisterCbntorBase
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <param name="tagCbnt">Modbus 字空间组合（寄存器缓存）</param>
    /// <param name="tagOffset">字节偏移（必须为偶数）</param>
    /// <param name="isReadOnly">是否只读（输入寄存器）</param>
    internal ModbusRegisterByteCbntor(TagDescriptor tagDescriptor, TagCbnt<ushort> tagCbnt, int tagOffset, bool isReadOnly)
        : base(tagDescriptor, tagCbnt, tagOffset, isReadOnly)
    {
    }

    /// <summary>
    /// 测点值
    /// </summary>
    public override object? Value
    {
        get
        {
            var reg = this.RegCache.Span[this.RegOffset];
            return (byte)(this.TagEndian() == EndianKinds.BigEndian ? (reg >> 8) : reg);
        }
        set
        {
#pragma warning disable CS8605 // Unboxing a possibly null value.
            var b = (byte)value;
#pragma warning restore CS8605 // Unboxing a possibly null value.
            this.EnsureWritable();
            var reg = this.RegCache.Span[this.RegOffset];
            var newReg = this.TagEndian() == EndianKinds.BigEndian
                ? (ushort)((reg & 0x00FF) | (b << 8))
                : (ushort)((reg & 0xFF00) | b);
            this.RegCache.Span[this.RegOffset] = newReg;
            this.Timestamp = DateTime.UtcNow;
            this.MarkDirty();
        }
    }
}
