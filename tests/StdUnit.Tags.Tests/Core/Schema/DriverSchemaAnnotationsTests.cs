using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.Core.Schema;

/// <summary>
/// 各驱动 schema 里的<b>设计器元数据</b>（<c>xs:annotation/xs:appinfo/driver</c>）与 XSD 本身的有效性。<br/>
/// 为什么需要它：驱动 xsd 只以 <c>AdditionalFiles</c> 参与编译（源生成器把它们变成字符串常量），
/// 构建<b>不会</b>校验 XSD 语法；一旦写坏，消费方一启用 <c>EnableXmlSchemaValidation()</c> 就炸。
/// 这里同时锁住两件事：<br/>
/// 1. 核心 + 每个驱动 schema 能编译，且能用它校验一份最小的项目 XML；<br/>
/// 2. <c>&lt;driver name&gt;</c> 必须与 <c>XxxNames.DriverName</c>（即 <c>driver="…"</c> 的取值）一致——
///    WebUi 的 <c>/catalog</c> 就是靠这条声明把驱动名绑到命名空间的。
/// </summary>
public class DriverSchemaAnnotationsTests
{
    public static IEnumerable<object[]> Drivers => new[]
    {
        new object[] { S7.S7Names.DriverName, "tags:s7", new S7.S7SchemaProvider() },
        new object[] { ModbusTcp.ModbusTcpNames.DriverName, "tags:modbustcp", new ModbusTcp.ModbusTcpSchemaProvider() },
        new object[] { ZLan.ZLanTcpNames.DriverName, "tags:zlan", new ZLan.ZLanTcpSchemaProvider() },
        new object[] { Hjzk.HjzkNames.DriverName, "tags:hjzk", new Hjzk.HjzkSchemaProvider() },
        new object[] { ComScanner.ComDriverNames.DriverName, "tags:com", new ComScanner.ComScannerSchemaProvider() },
        new object[] { OpcUaClient.OpcUaClientNames.DriverName, "tags:opcuaclient", new OpcUaClient.OpcUaClientSchemaProvider() },
        new object[] { SimpleFiles.SimpleFilesNames.DriverName, "tags:simplefiles", new SimpleFiles.SimpleFilesSchemaProvider() },
    };

    private static readonly XNamespace Xs = "http://www.w3.org/2001/XMLSchema";

    /// <summary>取 schema 里声明的驱动名（按本地名匹配：appinfo 内容可能继承默认 xmlns）。</summary>
    private static string? DeclaredDriverName(string schemaContent)
    {
        var root = XDocument.Parse(schemaContent).Root!;
        var driver = root.Element(Xs + "annotation")?
                         .Element(Xs + "appinfo")?
                         .Elements()
                         .FirstOrDefault(e => e.Name.LocalName == "driver");
        return (string?)driver?.Attribute("name");
    }

    [Theory]
    [MemberData(nameof(Drivers))]
    public void DriverSchema_DeclaresItsDriverNameAndNamespace(string driverName, string expectedNamespace, ITagsProjectSchemaProvider provider)
    {
        var schemas = provider.GetSchemaContents().ToArray();
        var (_, content) = Assert.Single(schemas);

        var root = XDocument.Parse(content).Root!;
        Assert.Equal(expectedNamespace, (string?)root.Attribute("targetNamespace"));
        Assert.Equal(driverName, DeclaredDriverName(content));
    }

    [Theory]
    [MemberData(nameof(Drivers))]
    public void DriverSchema_CompilesAndValidatesAMinimalProject(string driverName, string _, ITagsProjectSchemaProvider provider)
    {
        var contents = TagsProjectSchema.GetSchemaContents().Concat(provider.GetSchemaContents()).ToArray();

        var project = new XElement("Project",
            new XElement("Channel",
                new XAttribute("name", "c1"),
                new XAttribute("driver", driverName)),
            new XElement("TagGrp",
                new XAttribute("name", "g1"),
                new XAttribute("isEntry", "true"),
                new XAttribute("channel", "c1"),
                new XElement("Tag",
                    new XAttribute("name", "t1"),
                    new XAttribute("type", "INT16"),
                    new XAttribute("address", "0"))));

        var errors = TagsProjectSchema.Validate(project, contents);
        Assert.Empty(errors);
    }

    /// <summary>
    /// 驱动"额外认的测点属性"（<c>&lt;tagAttribute&gt;</c>）声明必须覆盖运行期真正从
    /// <c>TagDescriptor.Extras</c> 里读的那些键——这条断言就是为了防止声明与解析代码漂移。
    /// </summary>
    [Theory]
    [InlineData("S7", "maxlen")]
    [InlineData("SimpleFiles", "autoCreateFile")]
    [InlineData("ModbusTcp", "interpret")]
    public void DriverSchema_DeclaresItsExtraTagAttributes(string driverName, string expectedAttribute)
    {
        var provider = Drivers.Select(d => (ITagsProjectSchemaProvider)d[2]).Single(p =>
        {
            var content = p.GetSchemaContents().Single().Content;
            return DeclaredDriverName(content) == driverName;
        });

        var root = XDocument.Parse(provider.GetSchemaContents().Single().Content).Root!;
        var declared = root.Element(Xs + "annotation")?
                           .Element(Xs + "appinfo")?
                           .Elements()
                           .Where(e => e.Name.LocalName == "tagAttribute")
                           .Select(e => (string?)e.Attribute("name"))
                           .Where(n => n is not null)
                           .ToArray();

        Assert.Contains(expectedAttribute, declared!);
    }

    [Fact]
    public void AllDriverSchemas_CompileTogetherWithTheCoreSchema()
    {
        // 运行期就是这么做的（TagsProjectSchemaValidator：核心 + 所有 provider 合并成一个 XmlSchemaSet）。
        var contents = TagsProjectSchema.GetSchemaContents()
            .Concat(Drivers.Select(d => (ITagsProjectSchemaProvider)d[2]).SelectMany(p => p.GetSchemaContents()))
            .ToArray();

        var project = new XElement("Project",
            new XElement("Channel", new XAttribute("name", "c1"), new XAttribute("driver", "S7")),
            new XElement("TagGrp",
                new XAttribute("name", "g1"),
                new XAttribute("isEntry", "true"),
                new XAttribute("channel", "c1"),
                new XElement("Tag", new XAttribute("name", "t1"), new XAttribute("address", "DB1.0"))));

        var errors = TagsProjectSchema.Validate(project, contents);

        // 注意：驱动专属子元素在别的命名空间，这里不校验它们——只确认 8 个 schema 能共存。
        // "c1" 没有驱动专属字段会有别的错误吗？不会：Channel 的 xs:any 是 minOccurs=0。
        Assert.Empty(errors);
    }

    [Fact]
    public void CoreSchema_CarriesUiAnnotations()
    {
        var (_, content) = TagsProjectSchema.GetSchemaContents().Single();
        var root = XDocument.Parse(content).Root!;

        // 设计器文案挂在 xs:complexType 上（节点）与 xs:attribute 上（字段）。
        var tagType = root.Elements(Xs + "complexType").Single(e => (string?)e.Attribute("name") == "TagType");
        Assert.Equal("测点", (string?)tagType.Element(Xs + "annotation")?.Element(Xs + "appinfo")?.Element("ui")?.Attribute("label"));

        var isEntry = root.Descendants(Xs + "attribute").Single(e => (string?)e.Attribute("name") == "isEntry");
        Assert.Equal("作为入口", (string?)isEntry.Element(Xs + "annotation")?.Element(Xs + "appinfo")?.Element("ui")?.Attribute("label"));
    }
}
