namespace StdUnit.Tags.SimpleFiles;

internal class FloatDirectTag : SimpleFilesDirectTagBase<float>
{

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="thisChannel">自身通道</param>
    /// <param name="container">容器</param>
    public FloatDirectTag(TagDescriptor descriptor, SimpleFilesTagChannel? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
    }

    protected override float ParseValue(string text)
    {
        if (!float.TryParse(text, out var value))
        {
            throw new InvalidDataException($"无法将文件内容转换为float值：{text}");
        }
        return value;
    }
}
