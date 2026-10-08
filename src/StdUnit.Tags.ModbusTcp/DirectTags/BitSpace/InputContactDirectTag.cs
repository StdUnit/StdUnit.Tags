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
