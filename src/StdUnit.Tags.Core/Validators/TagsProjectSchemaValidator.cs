using System.Xml.Linq;

namespace StdUnit.Tags;

/// <summary>
/// <see cref="ITagsProjectValidator"/> 的一个实现：用核心 schema
/// （<see cref="TagsProjectSchema"/>）加上所有注册的 <see cref="ITagsProjectSchemaProvider"/>
/// 扩展 schema 一起校验项目根元素（XSD 校验）。
/// </summary>
public class TagsProjectSchemaValidator : ITagsProjectValidator
{
    private readonly IEnumerable<ITagsProjectSchemaProvider>? _providers;

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="providers">额外的 schema 提供者（第三方驱动扩展），可为 null/空</param>
    public TagsProjectSchemaValidator(IEnumerable<ITagsProjectSchemaProvider>? providers = null)
    {
        this._providers = providers;
    }

    /// <inheritdoc/>
    public void Validate(XElement root)
    {
        // 核心 schema + 第三方扩展 schema 合并校验
        var schemaContents = new List<(string LogicalName, string Content)>();
        schemaContents.AddRange(TagsProjectSchema.GetSchemaContents());

        if (this._providers != null)
        {
            foreach (var provider in this._providers)
            {
                schemaContents.AddRange(provider.GetSchemaContents());
            }
        }

        TagsProjectSchema.ValidateAndThrow(root, schemaContents);
    }
}
