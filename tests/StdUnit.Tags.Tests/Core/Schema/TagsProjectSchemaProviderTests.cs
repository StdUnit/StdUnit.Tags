using StdUnit.Tags;
using StdUnit.Tags.S7;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.Core.Schema;

/// <summary>
/// 测试第三方驱动扩展点：<see cref="ITagsProjectSchemaProvider"/> 可以把自定义 schema
/// 合并进 <see cref="ITagsProjectValidator"/>
/// </summary>
public class TagsProjectSchemaProviderTests
{
    private const string CustomDriverSchema = """
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
                   xmlns:my="tags:mydriver"
                   targetNamespace="tags:mydriver"
                   elementFormDefault="qualified">
            <xs:element name="MyOpt" type="xs:int"/>
        </xs:schema>
        """;

    private sealed class FakeSchemaProvider : ITagsProjectSchemaProvider
    {
        private readonly (string LogicalName, string Content)[] _schemas;

        public FakeSchemaProvider(params (string LogicalName, string Content)[] schemas)
            => this._schemas = schemas;
        public IEnumerable<(string LogicalName, string Content)> GetSchemaContents()
            => this._schemas;
    }

    /// <summary>
    /// 把 provider 的 schema 合并进核心 schema 后，
    /// 自定义元素按 provider schema 严格校验（值类型生效）。
    /// </summary>
    [Fact]
    public void Validate_WithProviderSchema_CustomElementValueChecked()
    {
        var combined = new List<(string, string)>(TagsProjectSchema.GetSchemaContents())
        {
            ("drivers/mydriver.xsd", CustomDriverSchema),
        };

        // 合法值通过
        var ok = XElement.Parse("""
            <Project xmlns:my="tags:mydriver">
                <Channel name="c1" driver="MyDriver">
                    <my:MyOpt>123</my:MyOpt>
                </Channel>
            </Project>
            """);
        var errorsOk = TagsProjectSchema.Validate(ok, combined);
        Assert.True(errorsOk.Count == 0, $"应通过校验: {string.Join(" | ", errorsOk)}");

        // 非法值
        var bad = XElement.Parse("""
            <Project xmlns:my="tags:mydriver">
                <Channel name="c1" driver="MyDriver">
                    <my:MyOpt>not-an-int</my:MyOpt>
                </Channel>
            </Project>
            """);
        var errorsBad = TagsProjectSchema.Validate(bad, combined);
        Assert.NotEmpty(errorsBad);
    }

    /// <summary>
    /// 端到端：EnableXmlSchemaValidation + 注册 provider 后，XML 正常构建
    /// （provider 的 schema 与核心 schema 合并校验，不影响运行期通道/测点构建）
    /// 。</summary>
    [Fact]
    public void MakeProject_WithValidationAndProvider_AcceptsCustomDriverXml()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.EnableXmlSchemaValidation();
            b.AddS7Support();
            b.Services.AddSingleton<ITagsProjectSchemaProvider>(new FakeSchemaProvider(
                ("drivers/mydriver.xsd", CustomDriverSchema)));
        });
        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ITagsProjectFactory>();

        var xml = XElement.Parse("""
            <Project xmlns:s7="tags:s7">
                <Channel name="S7-1" driver="S7">
                    <s7:IpAddr>localhost</s7:IpAddr>
                </Channel>
            </Project>
            """);

        using var proj = factory.Create(string.Empty, xml);
        Assert.NotNull(proj);
    }
}
