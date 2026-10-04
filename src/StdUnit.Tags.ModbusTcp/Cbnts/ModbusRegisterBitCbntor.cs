namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// Modbus 字空间（寄存器）的位组合子：占用 1 个寄存器，第 NthBit 位（0~15）。<br/>
/// 位在寄存器内的位置由 Modbus 标准定义（bit0 = LSB），与设备字节序无关。
/// </summary>
internal class ModbusRegisterBitCbntor : ModbusRegisterCbntorBase
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <param name="tagCbnt">Modbus 字空间组合（寄存器缓存）</param>
    /// <param name="tagOffset">字节偏移（必须为偶数）</param>
    /// <param name="isReadOnly">是否只读（输入寄存器）</param>
    /// <param name="nthBit">第 Nth 位，0~15</param>
    internal ModbusRegisterBitCbntor(TagDescriptor tagDescriptor, TagCbnt<ushort> tagCbnt, int tagOffset, bool isReadOnly, byte nthBit)
        : base(tagDescriptor, tagCbnt, tagOffset, isReadOnly)
    {
        if (nthBit > 15)
        {
            throw new ArgumentException($"寄存器位地址超出范围(0~15)，当前 nthBit={nthBit}");
        }
        this.NthBit = nthBit;
    }

    /// <summary>
    /// 第 Nth 位，0~15
    /// </summary>
    public byte NthBit { get; }

    /// <summary>
    /// 测点值
    /// </summary>
    public override object? Value
    {
        get
        {
            var reg = this.RegCache.Span[this.RegOffset];
            var hasFlag = (reg >> this.NthBit) & 1;
            return hasFlag != 0;
        }
        set
        {
            if (value is not bool b)
            {
                throw new Exception($"不应该为Bit类型的测点赋值一个类型为{value?.GetType().Name}值");
            }
            this.EnsureWritable();
            var reg = this.RegCache.Span[this.RegOffset];
            var newReg = b
                ? (ushort)(reg | (1 << this.NthBit))
                : (ushort)(reg & ~(1 << this.NthBit));
            this.RegCache.Span[this.RegOffset] = newReg;
            this.Timestamp = DateTime.Now;
            this.MarkDirty();
        }
    }
}
