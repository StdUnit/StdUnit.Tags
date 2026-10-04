namespace StdUnit.Tags.S7;

/// <summary>
/// S7 字符串测点缓存器
/// </summary>
public class S7StrTagCbntor : S7TagCbntorBase
{
    /// <summary>
    /// 字符串最大长度，ReadOnly
    /// </summary>
    public byte Maxlen { get; }

    /// <summary>
    /// 字符串有效长度，ReadOnly。
    /// </summary>
    public byte Strlen
    {
        get
        {
            var span = this.Cache.Span.Slice(CacheOffset);
            var total = span[0];
            var size = span[1];
            return size;
        }
    }

    internal S7StrTagCbntor(TagDescriptor tagDescriptor, TagCbnt<byte> tagCbnt, int cacheOffset, byte maxLen)
        : base(tagDescriptor, tagCbnt, cacheOffset, cacheOffset)
    {
        this.Maxlen = maxLen;
    }

    /// <summary>
    /// 测点值
    /// </summary>
    public override object? Value
    {
        get
        {
            var span = this.Cache.Span.Slice(CacheOffset);
            var size = span[1];
            var str = Compat.AsciiTextCompat.GetString(span.Slice(2, size));
            return str;
        }
        set
        {
            var str = value is null ? string.Empty : value is string s ? s : throw new InvalidCastException();
            var tagsize = this.TagSize();
            var span = this.Cache.Span.Slice(CacheOffset, tagsize);
            if (str.Length > this.Maxlen)
            {
                throw new ArgumentException($"字符串长度超过限制，最大{this.Maxlen}，实际{str.Length}");
            }

            var read = Compat.AsciiTextCompat.GetBytes(str, span.Slice(2));
            span[0] = this.Maxlen;
            span[1] = (byte)read;

            Timestamp = DateTime.UtcNow;
            MarkDirty();
        }
    }

}