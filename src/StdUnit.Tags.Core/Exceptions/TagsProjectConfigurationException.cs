namespace StdUnit.Tags;

/// <summary>
/// 项目配置在<b>语义</b>上不自洽时抛出：<c>channel</c> 指向不存在的通道、<c>driver</c> 未注册、
/// 同一父节点下测点重名、测点种类超出驱动支持范围、通道类型与测点要求不匹配等。<br/>
/// <br/>
/// 这些错误 XML 语法与 XSD 校验都发现不了（XSD 只能保证结构合法），只有在构建测点树时才能发现；
/// 统一成本类型后，用户不必再面对 <c>NullReferenceException</c> 或字典的
/// "An item with the same key has already been added"。<br/>
/// 与 <see cref="TagsProjectValidationException"/> 的分工：前者是"构建过程中立刻发现"，后者是
/// "构建之前由校验器批量扫描后一次性报告"。
/// </summary>
public class TagsProjectConfigurationException : TagsProjectLoadException
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="message">错误消息</param>
    /// <param name="location">出错位置描述，可为 null</param>
    /// <param name="innerException">内部异常，可为 null</param>
    public TagsProjectConfigurationException(string message, string? location = null, Exception? innerException = null)
        : base(message, location, innerException)
    {
    }
}
