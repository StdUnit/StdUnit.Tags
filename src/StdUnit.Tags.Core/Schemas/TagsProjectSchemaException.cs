namespace StdUnit.Tags;

/// <summary>
/// 测点项目 XML 未通过 XSD 校验时抛出（见 <see cref="TagsProjectSchema"/>）。
/// </summary>
public class TagsProjectSchemaException : Exception
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="errors">校验错误消息列表</param>
    public TagsProjectSchemaException(IReadOnlyList<string> errors)
        : base($"测点项目 XML 未通过 schema 校验，共 {errors.Count} 处错误：{Environment.NewLine}{string.Join(Environment.NewLine, errors.Select(e => "  - " + e))}")
    {
        this.Errors = errors;
    }

    /// <summary>
    /// 校验错误消息列表。
    /// </summary>
    public IReadOnlyList<string> Errors { get; }
}
