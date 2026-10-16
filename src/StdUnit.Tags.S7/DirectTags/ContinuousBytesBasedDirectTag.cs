namespace StdUnit.Tags.S7;

/// <summary>
/// 表示基于连续字节的直接S7测点。<br/>
/// 读写都直接在<b>栈缓冲区</b>上完成（S7 的连续字节读写接口本来就接受调用方提供的缓冲区），
/// 因此每次读/写都不再产生 byte[] 分配。<br/>
/// 这里存在的意义是为了个别测点直接嵌入 TagGrp（无需先建 TagCbnt），一般不会成为性能来源。<br/>
/// </summary>
/// <typeparam name="T"></typeparam>
internal abstract class ContinuousBytesBasedDirectTag<T> : Tag<T, S7TagChannel>
    where T : notnull, IEquatable<T>
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="thisChannel">对应于测点本身的通道</param>
    /// <param name="parent">父容器</param>
    public ContinuousBytesBasedDirectTag(TagDescriptor descriptor, S7TagChannel? thisChannel, TagContainer parent)
        : base(descriptor, thisChannel, parent)
    {
        this.Channel = thisChannel;
    }

    /// <inheritdoc/>
    public override ITagChannel? Channel { get; set; }

    /// <summary>
    /// 对应一个测点需要读写的字节数。<br/>
    /// <b>实现必须保证它是个小数值</b>——读写会直接 <c>stackalloc</c> 这么大的一块栈空间
    /// （现有实现最大的是 <see cref="StrDirectTag"/> 的 <c>Maxlen + 2</c>，不超过 257 字节）。
    /// </summary>
    public abstract int BufferSize { get; }

    protected abstract T ConvertFromBytes(Span<byte> bytes);
    protected abstract void FillBytes(Span<byte> bytes, T value);

    /// <inheritdoc/>
    public override Task ReadAsync(CancellationToken ct)
    {
        var addr = this.NormalizedAddress();
        Span<byte> buffer = stackalloc byte[BufferSize];
        this._bubbleChannel.Read(addr, buffer, ct);
        this._value = this.ConvertFromBytes(buffer);
        this.Timestamp = DateTime.UtcNow;
        this.NotifyTagRead(this.Value);
        return Task.CompletedTask;
    }


    /// <inheritdoc/>
    public override Task WriteAsync(CancellationToken ct)
    {
        var addr = this.NormalizedAddress();
        Span<byte> bytes = stackalloc byte[BufferSize];
        var value = this.Value ?? throw new InvalidOperationException($"Tag({this.TagName()}) 在写入前 Value 不能为空");
        this.FillBytes(bytes, value);
        this._bubbleChannel.Write(addr, bytes, ct);
        this.NotifyTagWritten(this.Value);
        this.IsDirty = false;
        return Task.CompletedTask;
    }
}