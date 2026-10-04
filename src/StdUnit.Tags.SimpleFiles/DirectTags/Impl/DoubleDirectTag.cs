namespace StdUnit.Tags.SimpleFiles;

internal class DoubleDirectTag : SimpleFilesDirectTagBase<double>
{

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="thisChannel">自身通道</param>
    /// <param name="container">容器</param>
    public DoubleDirectTag(TagDescriptor descriptor, SimpleFilesTagChannel? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
    }

    protected override double ParseValue(string text)
    {
        if (!double.TryParse(text, out var value))
        {
            throw new InvalidDataException($"无法将文件内容转换为double值：{text}");
        }
        return value;
    }
}
