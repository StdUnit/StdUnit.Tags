namespace StdUnit.Tags.S7;

internal static class S7Utils
{
    /// <summary>
    /// 规范化S7字符串型测点大小（最大值+2）
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <param name="maxlen"></param>
    /// <exception cref="InvalidDataException"></exception>
    public static void NormalizeS7StrTagSize(TagDescriptor tagDescriptor, out byte maxlen)
    {
        var tagName = tagDescriptor.TagName;

        maxlen =
            !tagDescriptor.Extras.TryGetValue("maxlen", out var maxlenAttr) ? throw new InvalidDataException($"字符串型测点必须指定字符串最大长度 maxlen。测点={tagName}") :
            !byte.TryParse(maxlenAttr.Value, out var prefer) ? throw new InvalidDataException($"字符串型测点 maxlen 属性必须可解析成正整数，当前 maxlen={maxlenAttr.Value}, 测点={tagName}") :
            prefer < 1 ? throw new InvalidDataException($"字符串型测点 maxlen 属性必须大于0，当前 maxlen={prefer}, 测点={tagName}") :
            prefer;

        // normalize the tagsize
        tagDescriptor.TagSize = 2 + prefer; // S7字符串的前2个字节是用来存储字符串的实际长度的，所以总长度=2+maxlen
    }
}
