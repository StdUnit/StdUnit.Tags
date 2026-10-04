using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Xunit;

namespace StdUnit.Tags.Tests.Core.Schema;

/// <summary>
/// 校验 <see cref="Schemas/tagsproject.xsd"/>（XSD 1.0 命名空间模块化方案）：<br/>
/// 1) 所有 schema 文件能被 .NET XmlSchemaSet 编译；<br/>
/// 2) 示例项目 XML 通过校验；<br/>
/// 3) 命名空间约束的行为（带前缀通过、无前缀拒绝）。<br/>
///
/// 文件来自各项目 <c>Schemas/</c> 与 <c>samples/</c>，
/// 由 csproj Link 复制到测试输出目录<br/>
/// </summary>
public class TagsProjectSchemaTests
{
    #region compile
    /// <summary>测试输出目录下的 Schema 根（相对 AppContext.BaseDirectory）。</summary>
    private const string SchemaRoot = "Schemas";

    private static string SchemaPath(string relative) =>
        Path.Combine(AppContext.BaseDirectory, SchemaRoot, relative);

    /// <summary>
    /// 编译 core + 驱动 schema。
    /// </summary>
    private static (XmlSchemaSet Set, List<string> Errors) CompileSchemas()
    {
        var set = new XmlSchemaSet();
        var errors = new List<string>();
        set.ValidationEventHandler += (_, e) =>
        {
            if (e.Severity == XmlSeverityType.Error)
            {
                errors.Add(e.Message);
            }
        };

        AddSchemaFile(set, SchemaPath("tagsproject.xsd"));
        foreach (var file in Directory.GetFiles(SchemaPath("drivers"), "*.xsd").OrderBy(f => f, StringComparer.Ordinal))
        {
            AddSchemaFile(set, file);
        }
        set.Compile();
        return (set, errors);
    }

    private static void AddSchemaFile(XmlSchemaSet set, string path)
    {
        using var reader = XmlReader.Create(path);
        set.Add(XmlSchema.Read(reader, null)!);
    }

    /// <summary>
    /// 校验文档。移除 <c>xsi:noNamespaceSchemaLocation</c>（相对路径在测试输出目录下失效），
    /// 使用预编译的 <see cref="XmlSchemaSet"/> 校验。
    /// </summary>
    private static List<string> Validate(XmlSchemaSet set, XDocument doc)
    {
        doc.Root!.SetAttributeValue(
            XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance") + "noNamespaceSchemaLocation", null);

        var errors = new List<string>();
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

        using var inner = doc.CreateReader();
        using var reader = XmlReader.Create(inner, settings);
        while (reader.Read()) { }
        return errors;
    }

    private static List<string> Validate(XmlSchemaSet set, string xml) =>
        Validate(set, XDocument.Parse(xml));
    #endregion

    /// <summary>
    /// 所有 schema（core + 7 个驱动）都能被 .NET XmlSchemaSet 编译。
    /// </summary>
    [Fact]
    public void AllSchemas_CompileWithDotNet()
    {
        var (_, errors) = CompileSchemas();
        Assert.True(errors.Count == 0, $"schema 编译错误: {string.Join(" | ", errors)}");
    }

    /// <summary>
    /// 仓库内示例项目 XML 均通过 schema 校验。
    /// </summary>
    [Fact]
    public void SampleIndexXmls_ValidateAgainstSchema()
    {
        var (set, compileErrors) = CompileSchemas();
        Assert.True(compileErrors.Count == 0, $"schema 编译错误: {string.Join(" | ", compileErrors)}");

        var samples = new[]
        {
            @"Samples\Web\index.xml",
            @"Samples\Web\index2.xml",
            @"Samples\Web\index-plugin.xml",
            @"Samples\WpfDemo\index.xml",
            @"Samples\WpfDemo\index-2.xml",
            @"Samples\NixMonitor\index.xml",
        };
        foreach (var rel in samples)
        {
            var path = Path.Combine(AppContext.BaseDirectory, rel);
            Assert.True(File.Exists(path), $"示例文件未复制到输出目录: {path}");
            var errors = Validate(set, XDocument.Load(path));
            Assert.True(errors.Count == 0, $"{rel} 校验失败: {string.Join(" | ", errors)}");
        }
    }

    /// <summary>
    /// 遗留的老格式会被 schema 拒绝（校验期行为）。
    /// </summary>
    [Fact]
    public void PrefixlessDriverElements_AreRejectedBySchema()
    {
        var (set, _) = CompileSchemas();
        var xml = """
                  <Project>
                    <Channel name="c1" driver="S7">
                      <IpAddr>localhost</IpAddr>
                    </Channel>
                  </Project>
                  """;
        var errors = Validate(set, xml);
        Assert.NotEmpty(errors);
    }

    /// <summary>带命名空间前缀的驱动专属子元素通过校验。</summary>
    [Fact]
    public void NamespacedDriverElements_AreAcceptedBySchema()
    {
        var (set, _) = CompileSchemas();
        var xml = """
                  <Project xmlns:s7="tags:s7">
                    <Channel name="c1" driver="S7">
                      <s7:IpAddr>localhost</s7:IpAddr>
                      <s7:Rack>0</s7:Rack>
                    </Channel>
                  </Project>
                  """;
        var errors = Validate(set, xml);
        Assert.True(errors.Count == 0, $"校验失败: {string.Join(" | ", errors)}");
    }

    /// <summary>非法值被 schema 拒绝（short 类型校验）。</summary>
    [Fact]
    public void InvalidDriverElementValue_IsRejectedBySchema()
    {
        var (set, _) = CompileSchemas();
        var xml = """
                  <Project xmlns:s7="tags:s7">
                    <Channel name="c1" driver="S7">
                      <s7:Rack>not-a-number</s7:Rack>
                    </Channel>
                  </Project>
                  """;
        var errors = Validate(set, xml);
        Assert.NotEmpty(errors);
    }
}
