namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// 多寄存器 DirectTag 基类：读写基于 <see cref="IModbusRegisterChannel"/>（寄存器数组，NModbus 已按线序解析）。
/// 字节序解读完全在测点层（<see cref="GetValueFromRegisters"/> / <see cref="FillRegisters"/>），通道层不做任何字节序调整。
/// </summary>
internal abstract class MultipleBytesDirectTag<T> : Tag<T, ModbusTcpChannel>
    where T : unmanaged, IEquatable<T>
{
    public MultipleBytesDirectTag(TagDescriptor descriptor, ModbusTcpChannel? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
    }
    public override ITagChannel? Channel { get; set; }

    #region 地址
    private ModbusTcpAddress? _addr;

    protected ModbusTcpAddress GetAddress()
    {
        if (_addr.HasValue)
        {
            return _addr.Value;
        }

        var addressStr = this.NormalizedAddress();
        var addr = ModBusTcpAddressParser.Parse(addressStr);
        this._addr = addr;
        return addr;
    }
    #endregion

    /// <summary>
    /// 本测点占用寄存器数
    /// </summary>
    protected abstract int RegisterCount { get; }

    /// <summary>
    /// 从寄存器数组解读物理值。<br/>
    /// <paramref name="registers"/> = 设备寄存器值（NModbus 按线序解析，标准设备 = 物理值）。<br/>
    /// 字节序（含寄存器内部颠倒、寄存器间 word order）在此处按 EndianKind 解读。
    /// </summary>
    protected abstract T GetValueFromRegisters(ReadOnlySpan<ushort> registers);

    /// <summary>
    /// 把物理值写入寄存器数组。<paramref name="registers"/> 语义与 <see cref="GetValueFromRegisters"/> 对称。
    /// </summary>
    protected abstract void FillRegisters(T value, Span<ushort> registers);

    /// <summary>
    /// 寄存器内部字节交换（适用于设备"寄存器内部字节颠倒"的非标场景，仅 16 位以上需要）。
    /// </summary>
    protected static ushort SwapBytes(ushort reg) => (ushort)((reg >> 8) | (reg << 8));

    public override async Task ReadAsync(CancellationToken ct)
    {
        var regs = await this._bubbleChannel.ReadRegistersAsync(this.NormalizedAddress(), this.RegisterCount, ct);
        this._value = this.GetValueFromRegisters(regs);
        this.Timestamp = DateTime.UtcNow;
        this.NotifyTagRead(this._value);
    }

    public override async Task WriteAsync(CancellationToken ct)
    {
        var addr = this.GetAddress();
        if (addr.Area != RegisterKinds.HoldingRegisters)
        {
            throw new InvalidOperationException($"按字节写入，只支持 HoldingRegisters，当前测点({this.TagName()}), 地址={addr.Area}");
        }

        var value = this._value;
        var registers = new ushort[this.RegisterCount];
        this.FillRegisters(value, registers);

        await this._bubbleChannel.WriteRegistersAsync(this.NormalizedAddress(), registers, ct);
        this.IsDirty = false;
        this.NotifyTagWritten(this._value);
    }

}
