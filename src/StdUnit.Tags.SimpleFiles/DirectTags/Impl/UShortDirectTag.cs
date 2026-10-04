namespace StdUnit.Tags.SimpleFiles;

internal class UShortDirectTag : SimpleFilesDirectTagBase<ushort>
{

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="thisChannel">自身通道</param>
    /// <param name="container">容器</param>
    public UShortDirectTag(TagDescriptor descriptor, SimpleFilesTagChannel? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
    }

    protected override ushort ParseValue(string text)
    {
        if (!ushort.TryParse(text, out var value))
        {
            throw new InvalidDataException($"无法将文件内容转换为ushort值：{text}");
        }
        return value;
    }
}
