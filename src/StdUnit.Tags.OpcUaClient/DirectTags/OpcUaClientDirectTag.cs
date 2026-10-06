
using Opc.Ua;

namespace StdUnit.Tags.OpcUaClient.DirectTags;

internal abstract class OpcUaClientDirectTag<TValue> : Tag<TValue, OpcUaClientTagChannel>
{
    public OpcUaClientDirectTag(TagDescriptor descriptor, OpcUaClientTagChannel? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
        var addrstr = this.NormalizedAddress();
    }

    protected abstract TValue ConvertFromDataValue(DataValue datavalue);

    public override async Task ReadAsync(CancellationToken ct)
    {
        var addrstr = this.NormalizedAddress();
        // 通道保证"返回即非坏值"（坏点会让这次读取直接失败，异常向上传播、不改缓存也不发通知）
        var datavale = await this._bubbleChannel.ReadValueAsync(addrstr, ct);
        var value = this.ConvertFromDataValue(datavale);
        this._value = value;
        this.Timestamp = DateTime.Now;
        this.NotifyTagRead(value);
    }

    public override async Task WriteAsync(CancellationToken ct)
    {
        var addr = this.NormalizedAddress();
        var datavalue = new DataValue { Value = this._value };
        await this._bubbleChannel.WriteValueAsync(addr, datavalue, ct);
        this.IsDirty = false;
        this.NotifyTagWritten(this._value);
    }
}


internal class OpcUaClientDirectTag : OpcUaClientDirectTag<object>
{
    public OpcUaClientDirectTag(TagDescriptor descriptor, OpcUaClientTagChannel? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
    }
    protected override object ConvertFromDataValue(DataValue datavalue)
    {
        return datavalue.Value;
    }
}