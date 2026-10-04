using System.Text.Json;

namespace StdUnit.Tags.SimpleFiles;


/// <summary>
/// Json类型的直接标签
/// </summary>
/// <typeparam name="T"></typeparam>
public class JsonDirectTag<T> : SimpleFilesDirectTagBase<T>
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="thisChannel"></param>
    /// <param name="container"></param>
    public JsonDirectTag(TagDescriptor descriptor, SimpleFilesTagChannel? thisChannel, TagContainer container)
     : base(descriptor, thisChannel, container)
    {
    }

    /// <summary>
    /// 解析JSON字符串为对象
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    protected override T? ParseValue(string text)
    {
        return JsonSerializer.Deserialize<T>(text);
    }

    /// <summary>
    /// 格式化对象为JSON字符串
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    protected override string FormatValue(T? value)
    {
        return JsonSerializer.Serialize(value);
    }
}
