using StdUnit.Tags.ComScanner.Channels;


namespace StdUnit.Tags.ComScanner.Tags;

/// <summary>
/// 表示一个有值为T类型的COM只读测点。
/// 目前的设计 <br/>
/// </summary>
/// <typeparam name="T"></typeparam>
public class ComReadOnlyTag<T> : Tag<T, ComChannelBase<T>>
{
    /// <summary>
    /// c'tor
    /// </summary>
    public ComReadOnlyTag(TagDescriptor descriptor, ComChannelBase<T>? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
    }


    /// <summary>
    /// 通常来说，我们通过测点的getter来获取数据；
    /// 但是setter不会写入到底层，这里setter存在只是为了满足Tag基类的抽象要求。<br/>
    /// </summary>
    public override T? Value
    {
        get => _value;
        set
        {
            this._value = value;
        }
    }

    /// <inheritdoc/>
    public override async Task ReadAsync(CancellationToken ct)
    {
        await this._bubbleChannel.EnsureConnectedAsync(force: false, ct);

        if (!this._bubbleChannel.TryDequeueInput(out var str))
        {
            return;
        }

        this._value = str;
        this.Timestamp = DateTime.Now;
        this.NotifyTagRead(str);
    }

    /// <summary>
    /// 对于只读测点来说，WriteAsync通常没有任何意义，所以这里的WriteAsync不应该做任何事情。<br/>
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    public override Task WriteAsync(CancellationToken ct)
    {
        this.IsDirty = false;
        this.NotifyTagWritten(this._value);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 当前的通道对象
    /// </summary>
    public ComChannelBase<T> ComChannel => this._bubbleChannel;
}
