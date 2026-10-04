
namespace StdUnit.Tags.SimpleFiles;


internal class BitDirectTag : SimpleFilesDirectTagBase<bool>
{

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="thisChannel">自身通道</param>
    /// <param name="container">容器</param>
    public BitDirectTag(TagDescriptor descriptor, SimpleFilesTagChannel? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
    }

    protected override bool ParseValue(string text)
    {
        if (!bool.TryParse(text, out var value))
        {
            throw new InvalidDataException($"无法将文件内容转换为布尔值：{text}");
        }
        return value;
    }
}



