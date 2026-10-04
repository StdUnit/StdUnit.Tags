using StdUnit.Tags;
using StdUnit.Tags.S7;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.Core.Schema;

/// <summary>
/// 测试 <see cref="TagsProjectSchema"/>（嵌入程序集的 XSD）与可选的加载期校验功能
/// </summary>
public class TagsProjectSchemaFeatureTests
{
    private const string NamespacedXml = """
        <Project xmlns:s7="tags:s7">
            <Channel name="S7-1" driver="S7">
                <s7:IpAddr>localhost</s7:IpAddr>
                <s7:Rack>0</s7:Rack>
                <s7:Slot>1</s7:Slot>
            </Channel>
            <TagGrp name="g1" isEntry="true" isEnabled="true" channel="S7-1" scanInterval="10">
                <Tag name="bit" address="DB200.0.0" type="BIT"/>
            </TagGrp>
        </Project>
        """;

    private const string LegacyPrefixlessXml = """
        <Project>
            <Channel name="S7-1" driver="S7">
                <IpAddr>localhost</IpAddr>
                <Rack>0</Rack>
                <Slot>1</Slot>
            </Channel>
            <TagGrp name="g1" isEntry="true" isEnabled="true" channel="S7-1" scanInterval="10">
                <Tag name="bit" address="DB200.0.0" type="BIT"/>
            </TagGrp>
        </Project>
        """;


    /// <summary>
    /// 命名空间格式的 XML 通过校验。
    /// </summary>
    [Fact]
    public void Validate_NamespacedXml_ReturnsNoErrors()
    {
        var errors = TagsProjectSchema.Validate(XElement.Parse(NamespacedXml));
        Assert.True(errors.Count == 0, $"校验失败: {string.Join(" | ", errors)}");
    }

    /// <summary>
    /// 无前缀的老格式 XML 校验报错（但运行期仍可用——见可选开关测试）。
    /// </summary>
    [Fact]
    public void Validate_LegacyPrefixlessXml_ReturnsErrors()
    {
        var errors = TagsProjectSchema.Validate(XElement.Parse(LegacyPrefixlessXml));
        Assert.NotEmpty(errors);
    }

    /// <summary>
    /// Validate 不修改传入的 XElement（无副作用）。
    /// </summary>
    [Fact]
    public void Validate_DoesNotMutateInput()
    {
        var xml = XElement.Parse(NamespacedXml);
        var before = xml.ToString();

        _ = TagsProjectSchema.Validate(xml);

        Assert.Equal(before, xml.ToString());
    }

    /// <summary>
    /// ValidateAndThrow 在出错时抛 TagsProjectSchemaException，且带错误列表。
    /// </summary>
    [Fact]
    public void ValidateAndThrow_ThrowsWithErrors()
    {
        var ex = Assert.Throws<TagsProjectSchemaException>(
            () => TagsProjectSchema.ValidateAndThrow(XElement.Parse(LegacyPrefixlessXml)));
        Assert.NotEmpty(ex.Errors);
        Assert.Contains("Channel", ex.Message);
    }


    /// <summary>
    /// ExportTo 导出核心 + 驱动 schema，保持目录结构。
    /// </summary>
    [Fact]
    public void ExportTo_WritesSchemaFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tags-schema-export-" + Guid.NewGuid().ToString("N"));
        try
        {
            var files = TagsProjectSchema.ExportTo(dir);

            // Core 只内嵌核心 schema；驱动 schema 由各驱动项目（ITagsProjectSchemaProvider）提供
            Assert.Contains(Path.Combine(dir, "tagsproject.xsd"), files);
            Assert.True(File.Exists(Path.Combine(dir, "tagsproject.xsd")));
            Assert.True(new FileInfo(Path.Combine(dir, "tagsproject.xsd")).Length > 0);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }


    /// <summary>
    /// 默认不启用校验：老格式（无前缀）XML 照常构建项目。
    /// </summary>
    [Fact]
    public void MakeProject_WithoutValidation_AcceptsLegacyXml()
    {
        using var root = BuildServices(b => { });
        using var scope = root.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ITagsProjectFactory>();

        using var proj = factory.Create(string.Empty, XElement.Parse(LegacyPrefixlessXml));

        Assert.Single(proj.Channels);
    }

    /// <summary>
    /// 启用校验后，老格式（无前缀）XML 在加载期被拒绝。
    /// </summary>
    [Fact]
    public void MakeProject_WithValidationEnabled_RejectsLegacyXml()
    {
        using var root = BuildServices(b => b.EnableXmlSchemaValidation());
        using var scope = root.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ITagsProjectFactory>();

        Assert.Throws<TagsProjectSchemaException>(() =>
            factory.Create(string.Empty, XElement.Parse(LegacyPrefixlessXml)));
    }

    /// <summary>
    /// 启用校验后，命名空间格式的 XML 正常构建。
    /// </summary>
    [Fact]
    public void MakeProject_WithValidationEnabled_AcceptsNamespacedXml()
    {
        using var root = BuildServices(b => b.EnableXmlSchemaValidation());
        using var scope = root.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ITagsProjectFactory>();

        using var proj = factory.Create(string.Empty, XElement.Parse(NamespacedXml));

        Assert.Single(proj.Channels);
    }

    private static ServiceProvider BuildServices(Action<TagsProjectServiceBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddS7Support();
            configure(b);
        });
        return services.BuildServiceProvider();
    }
}
