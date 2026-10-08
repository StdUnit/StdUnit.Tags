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
