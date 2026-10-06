namespace StdUnit.Tags.S7;

/// <summary>
/// S7 字节组合子：直接读缓存字节。
/// </summary>
public class S7ByteTagCbntor : S7TagCbntorBase
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <param name="tagCbnt">S7 组合（byte 缓存）</param>
    /// <param name="cacheOffset"></param>
    internal S7ByteTagCbntor(TagDescriptor tagDescriptor, TagCbnt<byte> tagCbnt, int cacheOffset)
        : base(tagDescriptor, tagCbnt, cacheOffset, cacheOffset)
    {
    }

    /// <summary>
    /// 测点值
    /// </summary>
    public override object? Value
    {
        get => this.Cache.Span[this.CacheOffset];
        set
        {
#pragma warning disable CS8605 // Unboxing a possibly null value.
            var b = (byte)value;
#pragma warning restore CS8605 // Unboxing a possibly null value.
            this.Cache.Span[this.CacheOffset] = b;
            this.Timestamp = DateTime.UtcNow;
            this.MarkDirty();
        }
    }
}
