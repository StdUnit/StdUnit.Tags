using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using StdUnit.Tags.WebUi;
using StdUnit.Tags.WebUi.Internal;
using Xunit;

namespace StdUnit.Tags.Tests.NetCoreOnly.WebUi;

/// <summary>
/// <see cref="DesignerCatalog"/>：把核心 schema + 驱动 schema 内省成设计器元数据。<br/>
/// 验证的是"XSD 里写了什么 → catalog 里就该是什么"：结构/类型/默认值/枚举/范围来自编译后的
/// <c>XmlSchemaSet</c>，驱动名绑定与文案来自 <c>xs:appinfo</c>。
/// </summary>
public class DesignerCatalogTests
{
    private sealed class FakeSchemaProvider : ITagsProjectSchemaProvider
    {
        private readonly (string LogicalName, string Content)[] _schemas;

        public FakeSchemaProvider(params (string LogicalName, string Content)[] schemas) => this._schemas = schemas;

        public IEnumerable<(string LogicalName, string Content)> GetSchemaContents() => this._schemas;
    }

    private static DesignerCatalogDto Build(params (string LogicalName, string Content)[] schemas)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (schemas.Length > 0)
        {
            services.AddSingleton<ITagsProjectSchemaProvider>(new FakeSchemaProvider(schemas));
        }

        using var provider = services.BuildServiceProvider();
        return new DesignerCatalog(provider.GetServices<ITagsProjectSchemaProvider>()).Catalog;
    }

    /// <summary>一个最小但完整的驱动 schema：驱动声明 + 枚举 + 范围 + 嵌套组 + appinfo 文案。</summary>
    private const string FakeDriverSchema = """
        <?xml version="1.0" encoding="utf-8"?>
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
                   xmlns="tags:fake" targetNamespace="tags:fake" elementFormDefault="qualified">
          <xs:annotation><xs:appinfo>
            <driver name="Fake" displayName="假驱动" prefix="fk"/>
          </xs:appinfo></xs:annotation>

          <xs:simpleType name="ModeType">
            <xs:restriction base="xs:string">
              <xs:enumeration value="Fast"/>
              <xs:enumeration value="Slow"/>
            </xs:restriction>
          </xs:simpleType>

          <xs:simpleType name="PositiveLimit">
            <xs:restriction base="xs:unsignedShort">
              <xs:minInclusive value="2"/>
            </xs:restriction>
          </xs:simpleType>

          <xs:complexType name="NestedType">
            <xs:sequence>
              <xs:element name="Inner" type="xs:int" minOccurs="1">
                <xs:annotation><xs:appinfo><ui label="内层" order="1"/></xs:appinfo></xs:annotation>
              </xs:element>
            </xs:sequence>
          </xs:complexType>

          <xs:element name="Host" type="xs:string">
            <xs:annotation><xs:appinfo><ui label="主机" order="1" placeholder="localhost"/></xs:appinfo></xs:annotation>
          </xs:element>
          <xs:element name="Mode" type="ModeType">
            <xs:annotation><xs:appinfo><ui label="模式" order="2"/></xs:appinfo></xs:annotation>
          </xs:element>
          <xs:element name="Retries" type="PositiveLimit">
            <xs:annotation><xs:appinfo><ui label="重试次数" order="3"/></xs:appinfo></xs:annotation>
          </xs:element>
          <xs:element name="Nested" type="NestedType">
            <xs:annotation><xs:appinfo><ui label="嵌套" order="4"/></xs:appinfo></xs:annotation>
          </xs:element>
        </xs:schema>
        """;

    /// <summary>有 targetNamespace 但没声明 <c>&lt;driver&gt;</c> 的扩展 schema（不该被当成通道驱动）。</summary>
    private const string SchemaWithoutDriver = """
        <?xml version="1.0" encoding="utf-8"?>
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
                   xmlns="tags:other" targetNamespace="tags:other" elementFormDefault="qualified">
          <xs:element name="Whatever" type="xs:string"/>
        </xs:schema>
        """;

    private static DesignerNodeDto Node(DesignerCatalogDto catalog, string name) =>
        catalog.Nodes.Single(n => n.Name == name);

    private static DesignerFieldDto Field(IEnumerable<DesignerFieldDto> fields, string name) =>
        fields.Single(f => f.Name == name);

    [Fact]
    public void Catalog_DescribesTheCoreSchema()
    {
        var catalog = Build();

        Assert.Equal(DesignerCatalog.CurrentVersion, catalog.Version);
        Assert.Equal("Project", catalog.Root.Name);
        Assert.Equal(new[] { "Channel", "TagGrp", "Logicet" }, catalog.Root.Children);
        Assert.True(catalog.Root.AllowsCustomAttributes);
        Assert.False(catalog.Root.AllowsForeignElements);

        // 根在 Nodes 里不重复出现；TagGrp 自嵌套只收一次。
        Assert.DoesNotContain(catalog.Nodes, n => n.Name == "Project");
        Assert.Equal(
            new[] { "Channel", "TagGrp", "TagCbnt", "Tag", "Logicet" },
            catalog.Nodes.Select(n => n.Name).ToArray());
    }

    [Fact]
    public void Catalog_ExposesAttributesWithDefaultsEnumsAndRanges()
    {
        var catalog = Build();
        var tag = Node(catalog, "Tag");

        var name = Field(tag.Attributes, "name");
        Assert.True(name.Required);
        Assert.Equal(DesignerFieldKind.String, name.Kind);
        Assert.Equal(1, name.MinLength);          // NameType 的 xs:minLength
        Assert.Equal("名称", name.Label);

        var endian = Field(tag.Attributes, "endian");
        Assert.Equal(DesignerFieldKind.Enum, endian.Kind);
        Assert.Equal(new[] { "LittleEndian", "BigEndian" }, endian.EnumValues!);
        Assert.Equal("LittleEndian", endian.Default);

        var access = Field(tag.Attributes, "access");
        Assert.Equal(new[] { "RW", "RO", "R1W", "WO" }, access.EnumValues!);
        Assert.Null(access.Default);

        var tagSize = Field(tag.Attributes, "tagSize");
        Assert.Equal(DesignerFieldKind.Number, tagSize.Kind);
        Assert.Equal(1, tagSize.Min);             // xs:positiveInteger
    }

    [Fact]
    public void Catalog_RespectsUiOrder()
    {
        var catalog = Build();
        var grp = Node(catalog, "TagGrp");

        // 声明顺序是 name/channel/isEntry/isEnabled/scanInterval/access，
        // appinfo 的 order 必须压过它。
        Assert.Equal(
            new[] { "name", "isEntry", "isEnabled", "scanInterval", "channel", "access" },
            grp.Attributes.Select(a => a.Name).ToArray());
    }

    [Fact]
    public void Catalog_MarksOpenContentModels()
    {
        var catalog = Build();

        var channel = Node(catalog, "Channel");
        Assert.True(channel.AllowsForeignElements);       // xs:any namespace="##other"
        Assert.True(channel.AllowsCustomAttributes);
        Assert.Empty(channel.Children);                   // 驱动字段只能在 #other 命名空间

        var grp = Node(catalog, "TagGrp");
        Assert.Equal(new[] { "TagGrp", "TagCbnt", "Tag" }, grp.Children);
        Assert.False(grp.AllowsForeignElements);
        Assert.True(grp.AllowsCustomAttributes);          // 驱动/插件自定义属性

        Assert.Empty(Node(catalog, "Tag").Children);
        Assert.Equal("Tag", Assert.Single(Node(catalog, "TagCbnt").Children));
    }

    [Fact]
    public void Catalog_DescribesTextOnlyElements()
    {
        var catalog = Build();
        var logicet = Node(catalog, "Logicet");

        Assert.Empty(logicet.Attributes);
        Assert.Empty(logicet.Children);
        Assert.False(logicet.AllowsCustomAttributes);
        Assert.Equal("相对项目根目录或绝对路径", logicet.TextContent);
    }

    [Fact]
    public void Catalog_ExposesBuiltinTagTypesAsHints()
    {
        var catalog = Build();

        Assert.Contains(BuiltinTagKinds.INT16, catalog.TagTypes);
        Assert.Contains(BuiltinTagKinds.STR, catalog.TagTypes);
        // schema 里 type 是开放字符串：这里只是候选，不是闭集（故不含 Unknown）。
        Assert.DoesNotContain(BuiltinTagKinds.Unknown, catalog.TagTypes);
    }

    [Fact]
    public void Catalog_WithoutAnyProvider_HasNoDrivers()
    {
        var catalog = Build();

        Assert.Empty(catalog.Drivers);
    }

    [Fact]
    public void Catalog_BindsDriverFromSchemaAppInfo()
    {
        var catalog = Build(("fake.xsd", FakeDriverSchema));

        var driver = Assert.Single(catalog.Drivers);
        Assert.Equal("Fake", driver.Name);
        Assert.Equal("假驱动", driver.DisplayName);
        Assert.Equal("tags:fake", driver.Namespace);
        Assert.Equal("fk", driver.Prefix);
    }

    [Fact]
    public void Catalog_DriverFieldsCarryTypeEnumAndRange()
    {
        var catalog = Build(("fake.xsd", FakeDriverSchema));
        var fields = catalog.Drivers.Single().Fields;

        // appinfo 的 order 生效（Host=1 / Mode=2 / Retries=3 / Nested=4）。
        Assert.Equal(new[] { "Host", "Mode", "Retries", "Nested" }, fields.Select(f => f.Name).ToArray());

        var host = Field(fields, "Host");
        Assert.Equal(DesignerFieldKind.String, host.Kind);
        Assert.Equal("String", host.Type);
        Assert.Equal("主机", host.Label);
        Assert.Equal("localhost", host.Placeholder);
        Assert.False(host.Required);                     // 通道字段都可省略

        var mode = Field(fields, "Mode");
        Assert.Equal(DesignerFieldKind.Enum, mode.Kind);
        Assert.Equal(new[] { "Fast", "Slow" }, mode.EnumValues!);

        var retries = Field(fields, "Retries");
        Assert.Equal(DesignerFieldKind.Number, retries.Kind);
        Assert.Equal(2, retries.Min);                    // 命名简单类型上的 minInclusive
    }

    [Fact]
    public void Catalog_NestedDriverElementBecomesAGroup()
    {
        var catalog = Build(("fake.xsd", FakeDriverSchema));

        var nested = Field(catalog.Drivers.Single().Fields, "Nested");
        Assert.Equal(DesignerFieldKind.Group, nested.Kind);
        Assert.Null(nested.Type);

        var inner = Assert.Single(nested.Children!);
        Assert.Equal("Inner", inner.Name);
        Assert.True(inner.Required);                     // minOccurs="1"
        Assert.Equal("内层", inner.Label);
    }

    [Fact]
    public void Catalog_IgnoresSchemasWithoutDriverDeclaration()
    {
        var catalog = Build(
            ("fake.xsd", FakeDriverSchema),
            ("other.xsd", SchemaWithoutDriver));

        // 只有声明了 <driver> 的 schema 才算通道驱动；其它扩展 schema 仍可参与校验。
        Assert.Equal(new[] { "Fake" }, catalog.Drivers.Select(d => d.Name).ToArray());
    }

    [Fact]
    public void Catalog_DerivesPrefixFromNamespaceWhenNotDeclared()
    {
        var schema = FakeDriverSchema.Replace(" prefix=\"fk\"", string.Empty, StringComparison.Ordinal);

        var driver = Assert.Single(Build(("fake.xsd", schema)).Drivers);
        Assert.Equal("fake", driver.Prefix);             // tags:fake → fake
    }

    [Fact]
    public void Catalog_IsRegisteredByAddTagsStreamAndServed()
    {
        // 服务面上能直接解析（端点在 MapTagsStream 里用它）。
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b => { });
        services.AddTagsStream();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
        var catalog = provider.GetRequiredService<DesignerCatalog>().Catalog;

        Assert.Equal("Project", catalog.Root.Name);
    }
}
