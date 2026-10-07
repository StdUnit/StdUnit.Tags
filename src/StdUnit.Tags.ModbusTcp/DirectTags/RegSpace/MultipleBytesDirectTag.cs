namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// 多寄存器 DirectTag 基类：读写基于 <see cref="IModbusRegisterChannel"/>（寄存器数组，NModbus 已按协议解析成数值）。
/// 字节序解读完全在测点层（<see cref="GetValueFromRegisters"/> / <see cref="FillRegisters"/>），通道层不做任何字节序调整。<br/>
/// <br/>
/// <b>主机端序不参与</b>：<c>ushort[]</c> 里是 NModbus 已还原好的数值，端序已由协议在寄存器内部固定，
/// 所以排布由两个属性分工表达：<c>endian</c> = 每个 16 位单元内部两个字节的顺序（与 S7 同名同义），
/// 32/64 位里单元之间的顺序由 <c>interpret</c> 表达（见 <see cref="ModbusValueInterpreter{T}"/>）。
/// 完整推导与实证见项目根目录的 Notes.md。
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
    /// <paramref name="registers"/> = 设备寄存器值（NModbus 按协议解析成数值，标准设备 = 物理值）。<br/>
    /// 排布（每单元内部字节序 + 单元之间的顺序）在此处按 <c>endian</c> + <c>interpret</c> 解读。
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
