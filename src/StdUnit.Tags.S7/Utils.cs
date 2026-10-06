namespace StdUnit.Tags.S7;

internal static class S7Utils
{
    /// <summary>
    /// 规范化S7字符串型测点大小（最大值+2）
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <param name="maxlen"></param>
    /// <exception cref="TagsProjectXmlException">未配置 maxlen，或 maxlen 不是大于 0 的整数</exception>
    public static void NormalizeS7StrTagSize(TagDescriptor tagDescriptor, out byte maxlen)
    {
        var tagName = tagDescriptor.TagName;
        var location = $"Tag({tagName})";

        maxlen =
            !tagDescriptor.Extras.TryGetValue("maxlen", out var maxlenAttr) ? throw new TagsProjectXmlException($"字符串型测点必须指定字符串最大长度 maxlen", location) :
            !byte.TryParse(maxlenAttr.Value, out var prefer) ? throw new TagsProjectXmlException($"字符串型测点 maxlen 属性必须可解析成正整数，当前 maxlen={maxlenAttr.Value}", location) :
            prefer < 1 ? throw new TagsProjectXmlException($"字符串型测点 maxlen 属性必须大于0，当前 maxlen={prefer}", location) :
            prefer;

        // normalize the tagsize
        tagDescriptor.TagSize = 2 + prefer; // S7字符串的前2个字节是用来存储字符串的实际长度的，所以总长度=2+maxlen
    }
}
