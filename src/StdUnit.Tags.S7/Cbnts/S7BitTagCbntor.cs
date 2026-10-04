namespace StdUnit.Tags.S7;

/// <summary>
/// S7 位组合子：缓存字节的第 NthBit 位（0~7）。<br/>
/// 注意：8~15 的位地址在 factory 中会规整化到 0~7 并导致 cacheOffset 偏移 1（跨字节位）。
/// </summary>
public class S7BitTagCbntor : S7TagCbntorBase
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <param name="tagCbnt">S7 组合（byte 缓存）</param>
    /// <param name="tagOffset"></param>
    /// <param name="cacheOffset"></param>
    /// <param name="nthBit">第 Nth 位，0~7</param>
    internal S7BitTagCbntor(TagDescriptor tagDescriptor, TagCbnt<byte> tagCbnt, int tagOffset, int cacheOffset, byte nthBit)
        : base(tagDescriptor, tagCbnt, tagOffset, cacheOffset)
    {
        this.NthBit = nthBit;
    }

    /// <summary>
    /// 第 Nth 位，0~7
    /// </summary>
    public byte NthBit { get; }

    /// <summary>
    /// 测点值
    /// </summary>
    public override object? Value
    {
        get
        {
            var flags = this.Cache.Span[this.CacheOffset];
            return (flags & (1 << this.NthBit)) != 0;
        }
        set
        {
            if (value is not bool b)
            {
                throw new Exception($"不应该为Bit类型的测点赋值一个类型为{value?.GetType().Name}值");
            }
            var oldFlags = this.Cache.Span[this.CacheOffset];
            var newFlags = b
                ? oldFlags | 1 << this.NthBit
                : oldFlags & ~(1 << this.NthBit);
            this.Cache.Span[this.CacheOffset] = (byte)newFlags;
            this.Timestamp = DateTime.Now;
            this.MarkDirty();
        }
    }
}
