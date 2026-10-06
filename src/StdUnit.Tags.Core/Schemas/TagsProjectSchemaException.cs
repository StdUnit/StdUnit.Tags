namespace StdUnit.Tags;

/// <summary>
/// 测点项目 XML 未通过 XSD 校验时抛出（见 <see cref="TagsProjectSchema"/>）。<br/>
/// 是 <see cref="TagsProjectValidationException"/> 的特化：<see cref="TagsProjectValidationException.Errors"/>
/// 的聚合语义完全一致，只是消息前缀改为 XSD 语义，便于把"结构不合法"与其它校验错误区分开。
/// </summary>
public class TagsProjectSchemaException : TagsProjectValidationException
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="errors">校验错误消息列表</param>
    public TagsProjectSchemaException(IReadOnlyList<string> errors)
        : base(errors, "测点项目 XML 未通过 schema 校验")
    {
    }
}
