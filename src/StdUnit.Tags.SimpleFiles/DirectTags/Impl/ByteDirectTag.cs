
namespace StdUnit.Tags.SimpleFiles;



internal class ByteDirectTag : SimpleFilesDirectTagBase<byte>
{

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="thisChannel">自身通道</param>
    /// <param name="container">容器</param>
    public ByteDirectTag(TagDescriptor descriptor, SimpleFilesTagChannel? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
    }

    protected override byte ParseValue(string text)
    {
        if (!byte.TryParse(text, out var value))
        {
            throw new InvalidDataException($"无法将文件内容转换为字节值：{text}");
        }
        return value;
    }
}



