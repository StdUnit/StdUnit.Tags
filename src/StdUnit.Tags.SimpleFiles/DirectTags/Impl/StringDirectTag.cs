namespace StdUnit.Tags.SimpleFiles;

internal class StringDirectTag : SimpleFilesDirectTagBase<string>
{

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="thisChannel">自身通道</param>
    /// <param name="container">容器</param>
    public StringDirectTag(TagDescriptor descriptor, SimpleFilesTagChannel? thisChannel, TagContainer container)
        : base(descriptor, thisChannel, container)
    {
    }

    protected override string? ParseValue(string text)
    {
        return text;
    }

    /// <summary>
    /// 字符串类型的默认内容是空字符串
    /// </summary>
    protected override Task CreateAndWriteDefaultAsync(string path, CancellationToken ct)
    {
        return Compat.FileAsyncCompat.WriteAllTextAsync(path, string.Empty, ct);
    }
}
