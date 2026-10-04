using StdUnit.Tags.ComScanner.Channels;


namespace StdUnit.Tags.ComScanner.Tags;

/// <summary>
/// 表示一个有值为T类型的COM只写测点。
/// 目前的设计 <br/>
/// </summary>
/// <typeparam name="T"></typeparam>
public class ComWriteOnlyTag<T> : Tag<T, ComChannelBase<T>>
{
    private readonly Func<T, byte[]> _converter;


    /// <summary>
    /// c'tor
    /// </summary>
    public ComWriteOnlyTag(TagDescriptor descriptor, ComChannelBase<T>? thisChannel, TagContainer container, Func<T, byte[]> converter)
        : base(descriptor, thisChannel, container)
    {
        _converter = converter;
    }


    /// <inheritdoc/>
    public override T? Value
    {
        get => _value;

        set
        {
            this._value = value;
            this.IsDirty = true;
            this.Timestamp = DateTime.Now;
        }
    }


    /// <summary>
    /// WriteOnly型测点的读操作通常没有任何意义，所以这里的ReadAsync不应该做任何事情。<br/>
    /// 也没有调用 NotifyTagRead 的必要，因为测点的值是外部写入的，而不是通过读取串口数据包获得的。<br/>
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    public override Task ReadAsync(CancellationToken ct) => Task.CompletedTask;


    /// <inheritdoc/>
    public override async Task WriteAsync(CancellationToken ct)
    {
        var value = this._value;
        if (value is null)
        {
            return;
        }

        var bytes = this._converter(value);
        await this._bubbleChannel.WriteAsync(bytes, 0, bytes.Length);
        this.IsDirty = false;
        this.NotifyTagWritten(value);
    }

    /// <summary>
    /// 当前的通道对象
    /// </summary>
    public ComChannelBase<T> ComChannel => this._bubbleChannel;
}
