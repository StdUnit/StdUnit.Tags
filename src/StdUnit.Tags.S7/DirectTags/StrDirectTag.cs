namespace StdUnit.Tags.S7;

internal class StrDirectTag : ContinuousBytesBasedDirectTag<string>
{
    /// <summary>
    /// 字符串最大长度，ReadOnly
    /// </summary>
    public byte Maxlen { get; }

    public StrDirectTag(TagDescriptor descriptor, S7TagChannel? thisChannel, TagContainer parent, byte maxLen)
        : base(descriptor, thisChannel, parent)
    {
        this.Maxlen = maxLen;
    }

    public override int BufferSize => Maxlen + 2;

    protected override string ConvertFromBytes(Span<byte> bytes)
    {
        var size = bytes[1];
        var str = Compat.AsciiTextCompat.GetString(bytes.Slice(2, size));
        return str;
    }
    protected override void FillBytes(Span<byte> bytes, string value)
    {
        var len = value.Length;
        if (len > Maxlen)
        {
            throw new ArgumentException($"String exceeds the maximum length(MaxLen={Maxlen}, attempts={value})");
        }
        var read = Compat.AsciiTextCompat.GetBytes(value, bytes.Slice(2));
        bytes[0] = this.Maxlen;
        bytes[1] = (byte)read;
    }
}



