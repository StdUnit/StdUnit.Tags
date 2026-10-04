namespace StdUnit.Tags;

/// <summary>
/// 向 <see cref="ITagsProjectValidator"/> 提供额外 schema（第三方驱动/扩展用）。<br/>
/// 核心 schema 由库自带（<see cref="TagsProjectSchema"/>），无需注册；
/// <code>
/// services.AddSingleton&lt;ITagsProjectSchemaProvider&gt;(sp =&gt;
///     new MySchemaProvider(MyDriverSchemas.GetSchemaContents()));
/// </code>
/// 注册后，启用 <c>EnableXmlSchemaValidation()</c> 时，校验器会把提供者给出的 schema
/// 与核心 schema 一起编译进 <see cref="System.Xml.Schema.XmlSchemaSet"/> 校验。
/// </summary>
public interface ITagsProjectSchemaProvider
{
    /// <summary>
    /// 全部 schema 内容（逻辑名 → 文本）。
    /// </summary>
    IEnumerable<(string LogicalName, string Content)> GetSchemaContents();
}
