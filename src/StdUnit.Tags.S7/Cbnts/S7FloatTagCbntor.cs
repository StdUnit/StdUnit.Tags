using System.Buffers.Binary;

namespace StdUnit.Tags.S7;
/// <summary>
/// S7 Float 组合子：缓存 = PLC 内存原始字节（IEEE754），按 EndianKind 直读。
/// </summary>
public class S7FloatTagCbntor : S7TagCbntorBase
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <param name="tagCbnt">S7 组合（byte 缓存）</param>
    /// <param name="cacheOffset"></param>
    internal S7FloatTagCbntor(TagDescriptor tagDescriptor, TagCbnt<byte> tagCbnt, int cacheOffset)
        : base(tagDescriptor, tagCbnt, cacheOffset, cacheOffset)
    {
    }

    /// <summary>
    /// 测点值
    /// </summary>
    public override object? Value
    {
        get
        {
            var span = this.Cache.Span.Slice(this.CacheOffset, 4);
            return Compat.FloatBitsCompat.Read(span, this.TagEndian() == EndianKinds.BigEndian);
        }
        set
        {
#pragma warning disable CS8605 // Unboxing a possibly null value.
            var data = (float)value;
#pragma warning restore CS8605 // Unboxing a possibly null value.
            var dst = this.Cache.Span.Slice(this.CacheOffset, 4);
            Compat.FloatBitsCompat.Write(dst, data, this.TagEndian() == EndianKinds.BigEndian);
            this.Timestamp = DateTime.UtcNow;
            this.MarkDirty();
        }
    }
}
