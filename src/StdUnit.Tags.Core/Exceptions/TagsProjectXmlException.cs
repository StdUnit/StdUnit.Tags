namespace StdUnit.Tags;

/// <summary>
/// 项目 XML 的属性/元素值不合法时抛出：缺少必填属性、枚举值未知、数值无法解析等。<br/>
/// <br/>
/// 与 <see cref="TagsProjectSchemaException"/>（XSD 校验，默认关闭）的区别是：本类型在<b>解析阶段</b>
/// 抛出，无论是否开启 XSD 校验都会触发，是"XML 写错了"的基本反馈。
/// </summary>
public class TagsProjectXmlException : TagsProjectLoadException
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="message">错误消息</param>
    /// <param name="location">出错位置描述，可为 null</param>
    /// <param name="innerException">内部异常，可为 null</param>
    public TagsProjectXmlException(string message, string? location = null, Exception? innerException = null)
        : base(message, location, innerException)
    {
    }
}
