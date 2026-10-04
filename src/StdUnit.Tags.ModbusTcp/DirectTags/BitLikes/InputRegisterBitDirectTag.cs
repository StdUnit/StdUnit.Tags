namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// Modbus的 InputRegister 的 bit 位，地址范围300000~399999
/// </summary>
internal class InputRegisterBitDirectTag : Tag<bool, ModbusTcpChannel>
{
    public InputRegisterBitDirectTag(TagDescriptor descriptor, ModbusTcpChannel? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
        var addr = this.GetAddress();
        this.NthBit = addr.NthBit;
    }
    public override ITagChannel? Channel { get; set; }

    /// <summary>
    /// 第Nth位比特: 取值范围 0~15。
    /// </summary>
    public byte NthBit { get; }

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

    public override async Task ReadAsync(CancellationToken ct)
    {
        var regs = await this._bubbleChannel.ReadRegistersAsync(this.NormalizedAddress(), 1, ct);
        this._value = ((regs[0] >> this.NthBit) & 1) != 0;
        this.Timestamp = DateTime.Now;
        this.NotifyTagRead(this._value);
    }
    public override Task WriteAsync(CancellationToken ct) =>
        throw new NotSupportedException($"输入寄存器点不可写入({this.TagName()}");
}
