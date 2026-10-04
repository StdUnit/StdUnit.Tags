namespace StdUnit.Tags.ModbusTcp;

internal class HoldingRegisterBitDirectTag : Tag<bool, ModbusTcpChannel>
{
    public HoldingRegisterBitDirectTag(TagDescriptor descriptor, ModbusTcpChannel? thisChannel, TagContainer container)
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
    public override async Task WriteAsync(CancellationToken ct)
    {
        var regs = await this._bubbleChannel.ReadRegistersAsync(this.NormalizedAddress(), 1, ct);
        var oldReg = regs[0];
        var newReg = this._value == true
            ? oldReg | (1 << this.NthBit)
            : oldReg & ~(1 << this.NthBit);

        await this._bubbleChannel.WriteRegistersAsync(this.NormalizedAddress(), new[] { (ushort)newReg }, ct);
        this.IsDirty = false;
        this.NotifyTagWritten(this._value);
    }
}
