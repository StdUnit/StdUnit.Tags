namespace StdUnit.Tags.S7;

/// <summary>
/// 表示基于连续字节的直接S7测点。<br/>
/// 这里许多方法效率并不高，但是暂时通道底层尚未提供更高效的接口，所以先这样实现
/// ——S7支持连续字节组合操作，这里存在的意义只是为了个别测点直接嵌入TagGrp，一般不会成为性能来源<br/>
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
    /// </summary>
    public abstract int BufferSize { get; }

    protected abstract T ConvertFromBytes(Span<byte> bytes);
    protected abstract void FillBytes(Span<byte> bytes, T value);

    /// <inheritdoc/>
    public override async Task ReadAsync(CancellationToken ct)
    {
        var addr = this.NormalizedAddress();
        var bytes = await this._bubbleChannel.ReadAsync(addr, BufferSize, ct);
        this._value = this.ConvertFromBytes(bytes.AsSpan());
        this.Timestamp = DateTime.UtcNow;
        this.NotifyTagRead(this.Value);
    }


    /// <inheritdoc/>
    public override async Task WriteAsync(CancellationToken ct)
    {
        var addr = this.NormalizedAddress();
        var bytes = new byte[BufferSize];
        var value = this.Value ?? throw new InvalidOperationException($"Tag({this.TagName()}) 在写入前 Value 不能为空");
        this.FillBytes(bytes, value);
        await this._bubbleChannel.WriteAsync(addr, bytes, ct);
        this.NotifyTagWritten(this.Value);
        this.IsDirty = false;
    }
}