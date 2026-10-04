using StdUnit.Tags.Generated;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace StdUnit.Tags;

/// <summary>
/// TagsProject XML Schema（XSD）的访问入口，提供校验和导出功能
/// </summary>
public static class TagsProjectSchema
{
    /// <summary>
    /// 核心 schema 的逻辑名。
    /// </summary>
    public const string CoreSchemaResource = "tagsproject.xsd";

    /// <summary>
    /// 核心 schema 内容（逻辑名 → 文本）。逻辑名如 <c>tagsproject.xsd</c>。
    /// 由源生成器从 Schemas/tagsproject.xsd 编译为常量（见 <see cref="Generated.SchemaContent_tagsproject"/>）。
    /// </summary>
    public static IEnumerable<(string LogicalName, string Content)> GetSchemaContents() =>
        SchemaContent_tagsproject.GetSchemaContents();

    /// <summary>
    /// 用 XSD 校验测点项目根元素 <paramref name="root"/>。
    /// 不做任何副作用——在副本上校验，不会修改传入的 <paramref name="root"/>。
    /// </summary>
    /// <param name="root">项目根元素（<c>&lt;Project&gt;</c>）</param>
    /// <returns>校验错误消息列表；为空表示通过。</returns>
    public static IReadOnlyList<string> Validate(XElement root) =>
        Validate(root, GetSchemaContents());

    /// <summary>
    /// 用指定 schema 集合校验测点项目根元素 <paramref name="root"/>。
    /// 不修改传入的 <paramref name="root"/>（在副本上校验）。
    /// </summary>
    /// <param name="root">项目根元素（<c>&lt;Project&gt;</c>）</param>
    /// <param name="schemaContents">schema 集合（逻辑名 → 文本），如核心 + 第三方扩展</param>
    /// <returns>校验错误消息列表；为空表示通过。</returns>
    public static IReadOnlyList<string> Validate(XElement root, IEnumerable<(string LogicalName, string Content)> schemaContents)
    {
        var errors = new List<string>();

        var set = new XmlSchemaSet();
        set.ValidationEventHandler += (_, e) =>
        {
            if (e.Severity == XmlSeverityType.Error)
            {
                errors.Add(e.Message);
            }
        };
        foreach (var (_, content) in schemaContents)
        {
            using (var txtReader = new StringReader(content))
            using (var xmlReader = XmlReader.Create(txtReader))
            {
                set.Add(XmlSchema.Read(xmlReader, null)!);
            }
        }
        set.Compile();

        // 在副本上校验；去掉 xsi:noNamespaceSchemaLocation（相对路径在运行期无意义，
        // 且此时 schema 已由代码注入）。
        var copy = new XElement(root);
        copy.SetAttributeValue(
            XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance") + "noNamespaceSchemaLocation", null);

        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = set,
        };
        settings.ValidationEventHandler += (_, e) =>
        {
            if (e.Severity == XmlSeverityType.Error)
            {
                errors.Add(e.Message);
            }
        };

        using var inner = copy.CreateReader();
        using var reader2 = XmlReader.Create(inner, settings);
        while (reader2.Read()) { }

        return errors;
    }

    /// <summary>
    /// 校验项目根元素；有错误时抛出 <see cref="TagsProjectSchemaException"/>。
    /// </summary>
    /// <param name="root">项目根元素（<c>&lt;Project&gt;</c>）</param>
    /// <exception cref="TagsProjectSchemaException">校验不通过</exception>
    public static void ValidateAndThrow(XElement root) =>
        ValidateAndThrow(root, GetSchemaContents());

    /// <summary>
    /// 用指定 schema 集合校验项目根元素；有错误时抛出 <see cref="TagsProjectSchemaException"/>。
    /// </summary>
    /// <param name="root">项目根元素（<c>&lt;Project&gt;</c>）</param>
    /// <param name="schemaContents">schema 集合（逻辑名 → 文本）</param>
    /// <exception cref="TagsProjectSchemaException">校验不通过</exception>
    public static void ValidateAndThrow(XElement root, IEnumerable<(string LogicalName, string Content)> schemaContents)
    {
        var errors = Validate(root, schemaContents);
        if (errors.Count > 0)
        {
            throw new TagsProjectSchemaException(errors);
        }
    }

    /// <summary>
    /// 把 XSD 导出到指定目录（保持目录结构），供编辑器智能提示等用途：
    ///   <c>{directory}/tagsproject.xsd</c>
    ///   <c>{directory}/drivers/*.xsd</c>
    /// 导出后，项目 XML 可用
    /// <c>xsi:noNamespaceSchemaLocation=".../tagsproject.xsd"</c> 引用核心 schema。
    /// <para>
    /// <b>internal（不对外暴露）</b>：NuGet + MSBuild/SDK 消费方无需导出——包的 buildTransitive
    /// targets 已自动把 schema 以链接项注入项目树。本方法仅作为内部备用（非 NuGet 部署、
    /// 运行时自省导出、外部工具需要物理文件等场景）；若将来确有对外需求，再提升为 public 即可
    /// （API 面刻意保持最小）。
    /// </para>
    /// </summary>
    /// <param name="directory">目标目录，不存在则创建</param>
    /// <returns>导出的文件路径列表</returns>
    internal static IReadOnlyList<string> ExportTo(string directory)
    {
        Directory.CreateDirectory(directory);
        var exported = new List<string>();

        foreach (var (logicalName, content) in GetSchemaContents())
        {
            var relative = logicalName.Replace('/', Path.DirectorySeparatorChar);
            var target = Path.Combine(directory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            exported.Add(target);
        }
        return exported;
    }
}
