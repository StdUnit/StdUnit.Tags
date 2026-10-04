namespace StdUnit.Tags.SimpleFiles;

internal class ShortDirectTag : SimpleFilesDirectTagBase<short>
{

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="thisChannel">自身通道</param>
    /// <param name="container">容器</param>
    public ShortDirectTag(TagDescriptor descriptor, SimpleFilesTagChannel? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
    }

    protected override short ParseValue(string text)
    {
        if (!short.TryParse(text, out var value))
        {
            throw new InvalidDataException($"无法将文件内容转换为short值：{text}");
        }
        return value;
    }
}
