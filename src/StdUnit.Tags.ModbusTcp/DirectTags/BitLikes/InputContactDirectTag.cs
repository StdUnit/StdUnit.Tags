namespace StdUnit.Tags.ModbusTcp;


/// <summary>
/// ModBus的 DI 点，地址范围10000~19999
/// </summary>
internal class InputContactDirectTag : Tag<bool, ModbusTcpChannel>
{
    public InputContactDirectTag(TagDescriptor descriptor, ModbusTcpChannel? thisChannel, TagContainer container)
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

    public override async Task ReadAsync(CancellationToken ct)
    {
        var bits = await this._bubbleChannel.ReadBitsAsync(this.NormalizedAddress(), 1, ct);
        this._value = bits[0];
        this.Timestamp = DateTime.UtcNow;
        this.NotifyTagRead(this._value);
    }


    public override Task WriteAsync(CancellationToken ct) =>
        throw new NotSupportedException($"DI点({this.TagName()}地址={this.RawAddress()})不可写入");


}
