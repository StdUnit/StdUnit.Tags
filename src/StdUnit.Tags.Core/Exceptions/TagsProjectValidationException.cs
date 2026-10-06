namespace StdUnit.Tags;

/// <summary>
/// 加载期项目校验（<see cref="ITagsProjectValidator"/>）未通过时抛出，携带全部错误明细。<br/>
/// <br/>
/// 校验器会一次性扫描整个项目并汇总所有错误（而不是发现第一个就停止），因此本异常总是聚合形态：
/// 用 <see cref="Errors"/> 逐条取出，或直接读 <see cref="Exception.Message"/>（已包含全部条目）。<br/>
/// XSD 校验使用其派生类型 <see cref="TagsProjectSchemaException"/>。
/// </summary>
public class TagsProjectValidationException : TagsProjectLoadException
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="errors">校验错误消息列表（非空）</param>
    public TagsProjectValidationException(IReadOnlyList<string> errors)
        : this(errors, "测点项目校验未通过")
    {
    }

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="errors">校验错误消息列表（非空）</param>
    /// <param name="messagePrefix">消息前缀，用于区分校验类别（如 XSD 校验）</param>
    protected TagsProjectValidationException(IReadOnlyList<string> errors, string messagePrefix)
        : base($"{messagePrefix}，共 {errors.Count} 处错误：{Environment.NewLine}{string.Join(Environment.NewLine, errors.Select(e => "  - " + e))}")
    {
        this.Errors = errors;
    }

    /// <summary>
    /// 校验错误消息列表。
    /// </summary>
    public IReadOnlyList<string> Errors { get; }
}
