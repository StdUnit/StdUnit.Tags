namespace StdUnit.Tags.ModbusTcp;


/// <summary>
/// Modbus的DO点，地址范围00000~09999
/// </summary>
internal class OutputCoilDirectTag : Tag<bool, ModbusTcpChannel>
{

    /// <summary>
    /// c'tor
    /// </summary>
    public OutputCoilDirectTag(TagDescriptor descriptor, ModbusTcpChannel? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
    }

    /// <inheritdoc/>
    public override ITagChannel? Channel { get; set; }

    #region 地址
    private ModbusTcpAddress? _addr;

    /// <summary>
    /// 获取地址
    /// </summary>
    /// <returns></returns>
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


    /// <inheritdoc/>
    public override async Task ReadAsync(CancellationToken ct)
    {
        var bits = await this._bubbleChannel.ReadBitsAsync(this.NormalizedAddress(), 1, ct);
        this._value = bits[0];
        this.Timestamp = DateTime.UtcNow;
        this.NotifyTagRead(this._value);
    }

    /// <inheritdoc/>
    public override async Task WriteAsync(CancellationToken ct)
    {
        var flag = this._value;
        await this._bubbleChannel.WriteBitsAsync(this.NormalizedAddress(), new[] { flag }, ct);
        this.IsDirty = false;
        this.NotifyTagWritten(flag);
    }
}
