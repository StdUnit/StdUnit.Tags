using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace StdUnit.Tags.WebUi.Internal;

/// <summary>
/// 把核心 schema + 所有已注册的驱动 schema 内省成 <see cref="DesignerCatalogDto"/>。<br/>
/// <br/>
/// <b>两条数据通道</b>，职责不重叠：<br/>
/// 1. <see cref="XmlSchemaSet"/>（编译后的）——结构、类型、必填、默认值、枚举、数值范围、<c>xs:any</c> 放行；<br/>
/// 2. <see cref="XDocument"/>——<c>xs:annotation/xs:appinfo</c> 里的 <c>&lt;driver&gt;</c>（驱动名绑定）与
///    <c>&lt;ui&gt;</c>（文案/顺序/分组）。注释是文档级事实，直接读 XML 比在编译产物上找 Annotation 更可靠。<br/>
/// <br/>
/// 结果在首次访问时构建并缓存（schema 集在一次运行内不变）。schema 本身有错时会让
/// <see cref="XmlSchemaSet.Compile"/> 抛异常——这与 <c>EnableXmlSchemaValidation()</c> 的行为一致，
/// 属于配置错误而不是请求错误。
/// </summary>
internal sealed class DesignerCatalog
{
    /// <summary>catalog 结构版本（对外契约）。</summary>
    public const int CurrentVersion = 1;

    /// <summary>文档根元素名（核心 schema 的约定，运行期不校验根元素名）。</summary>
    public const string RootElementName = "Project";

    private static readonly XNamespace Xs = XmlSchema.Namespace;

    private readonly Lazy<DesignerCatalogDto> _catalog;

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="providers">额外的 schema 提供者（各驱动包注册的，可为 null/空）</param>
    public DesignerCatalog(IEnumerable<ITagsProjectSchemaProvider>? providers = null)
    {
        this._catalog = new Lazy<DesignerCatalogDto>(() => Build(providers));
    }

    /// <summary>内省结果（首次访问时构建）。</summary>
    public DesignerCatalogDto Catalog => this._catalog.Value;

    private static DesignerCatalogDto Build(IEnumerable<ITagsProjectSchemaProvider>? providers)
    {
        var sources = new List<(string LogicalName, string Content)>();
        sources.AddRange(TagsProjectSchema.GetSchemaContents());
        if (providers is not null)
        {
            foreach (var provider in providers)
            {
                sources.AddRange(provider.GetSchemaContents());
            }
        }

        var annotations = new SchemaAnnotations(sources);
        var set = Compile(sources);

        var root = set.GlobalElements[new XmlQualifiedName(RootElementName)] as XmlSchemaElement
            ?? throw new InvalidOperationException(
                $"核心 schema 里没有 <{RootElementName}> 的全局元素声明，无法构建设计器元数据。");

        var collector = new NodeCollector(annotations);
        collector.Visit(root);

        return new DesignerCatalogDto(
            CurrentVersion,
            collector.Nodes[0],
            collector.Nodes.Skip(1).ToArray(),
            CollectDrivers(set, annotations),
            BuiltinTagTypes());
    }

    private static XmlSchemaSet Compile(IEnumerable<(string LogicalName, string Content)> sources)
    {
        var set = new XmlSchemaSet();
        foreach (var (logicalName, content) in sources)
        {
            using var text = new StringReader(content);
            using var reader = XmlReader.Create(text);
            try
            {
                set.Add(XmlSchema.Read(reader, null)!);
            }
            catch (XmlSchemaException ex)
            {
                throw new InvalidOperationException($"无法解析 schema \"{logicalName}\"：{ex.Message}", ex);
            }
        }

        set.Compile();
        return set;
    }

    /// <summary>
    /// 内置测点类型。schema 里 <c>type</c> 故意是开放字符串（插件可注册自定义类型），
    /// 所以这只是给设计器的候选提示，不是闭集。
    /// </summary>
    private static IReadOnlyList<string> BuiltinTagTypes() => new[]
    {
        BuiltinTagKinds.BIT,
        BuiltinTagKinds.BYTE,
        BuiltinTagKinds.INT16,
        BuiltinTagKinds.UINT16,
        BuiltinTagKinds.INT32,
        BuiltinTagKinds.UINT32,
        BuiltinTagKinds.INT64,
        BuiltinTagKinds.UINT64,
        BuiltinTagKinds.FLOAT,
        BuiltinTagKinds.DOUBLE,
        BuiltinTagKinds.STR,
        BuiltinTagKinds.DI,
        BuiltinTagKinds.DO,
    };

    private static IReadOnlyList<DesignerDriverDto> CollectDrivers(XmlSchemaSet set, SchemaAnnotations annotations)
    {
        var drivers = new List<DesignerDriverDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (XmlSchema schema in set.Schemas())
        {
            var ns = schema.TargetNamespace;
            // 核心 schema 没有 targetNamespace，也因此不可能有驱动声明。
            if (string.IsNullOrEmpty(ns))
            {
                continue;
            }

            var driver = annotations.GetDriver(ns);
            if (driver is null || !seen.Add(driver.Name))
            {
                continue;
            }

            var index = 0;
            var fields = new List<(int Index, DesignerFieldDto Field)>();
            foreach (XmlSchemaElement element in schema.Elements.Values)
            {
                // 通道里的驱动字段是经核心 schema 的 xs:any minOccurs="0" 插进来的，
                // 所以"是否必填"与元素自己的 minOccurs（全局声明默认 1）无关：一律可选。
                fields.Add((index++, DescribeElement(element, ns, annotations, required: false)));
            }

            drivers.Add(new DesignerDriverDto(
                driver.Name,
                driver.DisplayName ?? driver.Name,
                ns,
                driver.Prefix ?? DerivePrefix(ns),
                // OrderBy 是稳定排序：未声明 order 的字段保持 schema 里的声明顺序。
                fields.OrderBy(f => f.Field.Order ?? int.MaxValue).Select(f => f.Field).ToArray(),
                driver.TagAttributes,
                driver.CbntAttributes));
        }

        return drivers.OrderBy(d => d.Name, StringComparer.Ordinal).ToArray();
    }

    /// <summary>命名空间 <c>tags:s7</c> → 前缀 <c>s7</c>（去掉非 NCName 字符）。</summary>
    private static string DerivePrefix(string ns)
    {
        var suffix = ns[(ns.LastIndexOf(':') + 1)..];
        var prefix = new string(suffix.Where(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '.').ToArray());
        if (prefix.Length == 0)
        {
            return "drv";
        }

        // NCName 不能以数字/'-'/'.' 开头。
        return char.IsLetter(prefix[0]) || prefix[0] == '_' ? prefix : "d" + prefix;
    }

    private static DesignerFieldDto DescribeElement(
        XmlSchemaElement element,
        string ns,
        SchemaAnnotations annotations,
        bool required)
    {
        var name = element.QualifiedName.Name;
        var ui = annotations.ElementUi(ns, name);
        var type = element.ElementSchemaType;

        if (type is XmlSchemaComplexType complex)
        {
            var (children, _) = WalkParticle(complex.ContentTypeParticle);
            return new DesignerFieldDto(
                Name: name,
                Type: null,
                Kind: DesignerFieldKind.Group,
                Required: required,
                Default: null,
                EnumValues: null,
                Min: null,
                Max: null,
                MinLength: null,
                Label: ui?.Label,
                Order: ui?.Order,
                Group: ui?.Group,
                Placeholder: ui?.Placeholder,
                Help: ui?.Help,
                Children: children.Select(c => DescribeElement(c, ns, annotations, required: c.MinOccurs > 0))
                                  .OrderBy(f => f.Order ?? int.MaxValue)
                                  .ToArray());
        }

        var shape = DescribeSimpleType(type as XmlSchemaSimpleType);
        return new DesignerFieldDto(
            Name: name,
            Type: shape.Type,
            Kind: shape.Kind,
            Required: required,
            Default: null,
            EnumValues: shape.Enums,
            Min: shape.Min,
            Max: shape.Max,
            MinLength: shape.MinLength,
            Label: ui?.Label,
            Order: ui?.Order,
            Group: ui?.Group,
            Placeholder: ui?.Placeholder,
            Help: ui?.Help,
            Children: null);
    }

    private static DesignerFieldDto DescribeAttribute(
        XmlSchemaAttribute attribute,
        string ownerTypeName,
        string ns,
        SchemaAnnotations annotations)
    {
        var name = attribute.QualifiedName.Name;
        var ui = annotations.AttributeUi(ns, ownerTypeName, name);
        var shape = DescribeSimpleType(attribute.AttributeSchemaType);

        return new DesignerFieldDto(
            Name: name,
            Type: shape.Type,
            Kind: shape.Kind,
            Required: attribute.Use == XmlSchemaUse.Required,
            Default: attribute.Use == XmlSchemaUse.Required ? null : attribute.DefaultValue,
            EnumValues: shape.Enums,
            Min: shape.Min,
            Max: shape.Max,
            MinLength: shape.MinLength,
            Label: ui?.Label,
            Order: ui?.Order,
            Group: ui?.Group,
            Placeholder: ui?.Placeholder,
            Help: ui?.Help,
            Children: null,
            AppliesToDrivers: ui?.AppliesToDrivers);
    }

    /// <summary>
    /// 简单类型的"形状"：基础类型名 + 渲染类别 + 枚举 + 数值范围 + 最小长度。
    /// </summary>
    private static SimpleShape DescribeSimpleType(XmlSchemaSimpleType? type)
    {
        var enums = new List<string>();
        decimal? min = null;
        decimal? max = null;
        int? minLength = null;

        if (type?.Content is XmlSchemaSimpleTypeRestriction restriction)
        {
            foreach (XmlSchemaFacet facet in restriction.Facets)
            {
                switch (facet)
                {
                    case XmlSchemaEnumerationFacet e:
                        if (!string.IsNullOrEmpty(e.Value))
                        {
                            enums.Add(e.Value);
                        }
                        break;
                    case XmlSchemaMinInclusiveFacet minInclusive:
                        min = ParseDecimal(minInclusive.Value) ?? min;
                        break;
                    case XmlSchemaMaxInclusiveFacet maxInclusive:
                        max = ParseDecimal(maxInclusive.Value) ?? max;
                        break;
                    case XmlSchemaMaxExclusiveFacet maxExclusive:
                        max = ParseDecimal(maxExclusive.Value) ?? max;
                        break;
                    case XmlSchemaMinLengthFacet minLen:
                        minLength = int.TryParse(minLen.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                            ? n
                            : minLength;
                        break;
                }
            }
        }

        var code = type?.Datatype?.TypeCode ?? XmlTypeCode.String;

        // positiveInteger / nonNegativeInteger 的边界来自类型本身，不是显式 facet。
        min ??= code switch
        {
            XmlTypeCode.PositiveInteger => 1,
            XmlTypeCode.NonNegativeInteger => 0,
            _ => null,
        };

        var kind = enums.Count > 0
            ? DesignerFieldKind.Enum
            : code == XmlTypeCode.Boolean
                ? DesignerFieldKind.Bool
                : IsNumeric(code)
                    ? DesignerFieldKind.Number
                    : DesignerFieldKind.String;

        return new SimpleShape(code.ToString(), kind, enums.Count == 0 ? null : enums, min, max, minLength);
    }

    private static bool IsNumeric(XmlTypeCode code) => code is
        XmlTypeCode.Decimal or XmlTypeCode.Double or XmlTypeCode.Float or
        XmlTypeCode.Integer or XmlTypeCode.NonPositiveInteger or XmlTypeCode.NegativeInteger or
        XmlTypeCode.NonNegativeInteger or XmlTypeCode.PositiveInteger or
        XmlTypeCode.Long or XmlTypeCode.Int or XmlTypeCode.Short or XmlTypeCode.Byte or
        XmlTypeCode.UnsignedLong or XmlTypeCode.UnsignedInt or XmlTypeCode.UnsignedShort or XmlTypeCode.UnsignedByte;

    private static decimal? ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : null;

    /// <summary>
    /// 收集一个内容模型里的元素声明，并判断是否放行"其它命名空间"的元素（<c>xs:any namespace="##other"</c>）。
    /// </summary>
    private static (List<XmlSchemaElement> Elements, bool AllowsForeignElements) WalkParticle(XmlSchemaParticle? particle)
    {
        var elements = new List<XmlSchemaElement>();
        var allowsForeign = false;
        Walk(particle);
        return (elements, allowsForeign);

        void Walk(XmlSchemaParticle? p)
        {
            switch (p)
            {
                case XmlSchemaElement element:
                    elements.Add(element);
                    break;
                case XmlSchemaGroupBase group:
                    foreach (var item in group.Items)
                    {
                        Walk(item as XmlSchemaParticle);
                    }
                    break;
                case XmlSchemaAny any:
                    // Namespace 形如 "##other" / "##any" / "tags:s7"。
                    allowsForeign |= (any.Namespace ?? string.Empty).Contains("##other", StringComparison.Ordinal)
                        || (any.Namespace ?? string.Empty).Contains("##any", StringComparison.Ordinal);
                    break;
            }
        }
    }

    /// <summary>
    /// 从根元素出发按"元素 → 类型 → 子元素"递归，收集全部节点定义（同名只收一次：
    /// <c>TagGrp</c> 自嵌套就是同一个类型）。
    /// </summary>
    private sealed class NodeCollector
    {
        private readonly SchemaAnnotations _annotations;
        private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

        public NodeCollector(SchemaAnnotations annotations)
        {
            this._annotations = annotations;
        }

        /// <summary>按发现顺序排列：第 0 个是根元素。</summary>
        public List<DesignerNodeDto> Nodes { get; } = new();

        public void Visit(XmlSchemaElement element)
        {
            var name = element.QualifiedName.Name;
            if (string.IsNullOrEmpty(name) || !this._seen.Add(name))
            {
                return;
            }

            var type = element.ElementSchemaType;
            if (type is XmlSchemaComplexType complex)
            {
                var typeName = complex.QualifiedName.Name;
                var ui = this._annotations.TypeUi(typeName) ?? this._annotations.ElementUi(string.Empty, name);
                var (children, allowsForeign) = WalkParticle(complex.ContentTypeParticle);

                this.Nodes.Add(new DesignerNodeDto(
                    name,
                    ui?.Label,
                    DescribeAttributes(complex, typeName),
                    children.Select(c => c.QualifiedName.Name).ToArray(),
                    allowsForeign,
                    complex.AttributeWildcard is not null,
                    TextContent: null));

                foreach (var child in children)
                {
                    this.Visit(child);
                }

                return;
            }

            // 纯文本元素（<Logicet>）：没有属性、没有子元素，值就是元素文本。
            var simpleUi = this._annotations.ElementUi(string.Empty, name);
            this.Nodes.Add(new DesignerNodeDto(
                name,
                simpleUi?.Label,
                Array.Empty<DesignerFieldDto>(),
                Array.Empty<string>(),
                AllowsForeignElements: false,
                AllowsCustomAttributes: false,
                TextContent: simpleUi?.Help ?? simpleUi?.Placeholder ?? "文本"));
        }

        private DesignerFieldDto[] DescribeAttributes(XmlSchemaComplexType complex, string typeName)
        {
            var index = 0;
            var result = new List<(int Index, DesignerFieldDto Field)>();
            foreach (XmlSchemaAttribute attribute in complex.AttributeUses.Values)
            {
                if (attribute.Use == XmlSchemaUse.Prohibited)
                {
                    continue;
                }

                result.Add((index, DescribeAttribute(attribute, typeName, string.Empty, this._annotations)));
                index++;
            }

            // 显式 order 优先；同一档内保持声明顺序（OrderBy 稳定）。
            return result.OrderBy(r => r.Field.Order ?? int.MaxValue)
                         .ThenBy(r => r.Index)
                         .Select(r => r.Field)
                         .ToArray();
        }
    }

    /// <summary>
    /// 一次简单类型的"形状"：基础类型名 + 渲染类别 + 枚举 + 数值范围 + 最小长度。
    /// 属性自己的 <c>default</c> 不在这里（由 <see cref="DescribeAttribute"/> 直接读 <see cref="XmlSchemaAttribute.DefaultValue"/>）。
    /// </summary>
    private sealed record SimpleShape(
        string? Type,
        string Kind,
        IReadOnlyList<string>? Enums,
        decimal? Min,
        decimal? Max,
        int? MinLength);

    /// <summary>
    /// <c>xs:annotation/xs:appinfo</c> 里的设计器提示，按"文档中的位置"索引。<br/>
    /// 内容形状（appinfo 里的元素不带命名空间；多个参数与 <c>xs:schema</c> 上有没有默认 <c>xmlns</c> 无关，按本地名匹配）：<br/>
    /// <code>
    /// &lt;xs:schema …&gt;
    ///   &lt;xs:annotation&gt;&lt;xs:appinfo&gt;
    ///     &lt;driver name="S7" displayName="Siemens S7" prefix="s7"/&gt;
    ///     &lt;!-- 驱动额外认的测点/组合属性：XSD 1.0 表达不了，只能在这里"声明" --&gt;
    ///     &lt;tagAttribute name="maxlen" kind="number" min="1" max="255" label="字符串最大长度"
    ///                   appliesToTypes="STR" required="true"/&gt;
    ///     &lt;cbntAttribute name="…" …/&gt;
    ///   &lt;/xs:appinfo&gt;&lt;/xs:annotation&gt;
    ///   …
    ///   &lt;xs:complexType name="TagGrpType"&gt;
    ///     &lt;xs:annotation&gt;&lt;xs:appinfo&gt;&lt;ui label="测点组" order="2"/&gt;&lt;/xs:appinfo&gt;&lt;/xs:annotation&gt;
    ///     &lt;xs:attribute name="isEntry"&gt;
    ///       &lt;xs:annotation&gt;&lt;xs:appinfo&gt;&lt;ui label="作为入口" order="1" group="基础"/&gt;&lt;/xs:appinfo&gt;&lt;/xs:annotation&gt;
    ///     &lt;/xs:attribute&gt;
    ///   &lt;/xs:complexType&gt;
    /// &lt;/xs:schema&gt;
    /// </code>
    /// XSD 会忽略这些内容（<c>appinfo</c> 可以放任意 XML），所以对校验无影响。
    /// </summary>
    private sealed class SchemaAnnotations
    {
        private readonly Dictionary<string, DriverAnnotation> _drivers = new(StringComparer.Ordinal);
        private readonly Dictionary<(string Ns, string Name), UiInfo> _types = new();
        private readonly Dictionary<(string Ns, string Name), UiInfo> _elements = new();
        private readonly Dictionary<(string Ns, string Owner, string Name), UiInfo> _attributes = new();

        public SchemaAnnotations(IEnumerable<(string LogicalName, string Content)> sources)
        {
            foreach (var (_, content) in sources)
            {
                this.Add(content);
            }
        }

        public DriverAnnotation? GetDriver(string ns) => this._drivers.GetValueOrDefault(ns);

        public UiInfo? TypeUi(string typeName) => this._types.GetValueOrDefault((string.Empty, typeName));

        public UiInfo? ElementUi(string ns, string name) => this._elements.GetValueOrDefault((ns, name));

        public UiInfo? AttributeUi(string ns, string ownerTypeName, string name) =>
            this._attributes.GetValueOrDefault((ns, ownerTypeName, name));

        private void Add(string content)
        {
            XDocument document;
            try
            {
                document = XDocument.Parse(content);
            }
            catch (XmlException)
            {
                // schema 本身有问题时 Compile 会给出正式的错误，这里只负责别让元数据先行崩掉。
                return;
            }

            var root = document.Root;
            if (root is null)
            {
                return;
            }

            var ns = (string?)root.Attribute("targetNamespace") ?? string.Empty;

            var driver = ParseDriver(root);
            if (driver is not null)
            {
                this._drivers[ns] = driver;
            }

            foreach (var element in root.Descendants(Xs + "element"))
            {
                var name = (string?)element.Attribute("name");
                var ui = ParseUi(element);
                if (name is not null && ui is not null)
                {
                    this._elements[(ns, name)] = ui;
                }
            }

            foreach (var complexType in root.Descendants(Xs + "complexType"))
            {
                var typeName = (string?)complexType.Attribute("name");
                if (typeName is null)
                {
                    continue;
                }

                var ui = ParseUi(complexType);
                if (ui is not null)
                {
                    this._types[(ns, typeName)] = ui;
                }

                foreach (var attribute in complexType.Elements(Xs + "attribute"))
                {
                    var name = (string?)attribute.Attribute("name");
                    var attributeUi = ParseUi(attribute);
                    if (name is not null && attributeUi is not null)
                    {
                        this._attributes[(ns, typeName, name)] = attributeUi;
                    }
                }
            }
        }

        private static DriverAnnotation? ParseDriver(XElement schema)
        {
            var driver = AppinfoChild(schema, "driver");
            var name = (string?)driver?.Attribute("name");
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return new DriverAnnotation(
                name!,
                (string?)driver!.Attribute("displayName") ?? (string?)driver.Attribute("label"),
                (string?)driver.Attribute("prefix"),
                ParseAttributeDeclarations(schema, "tagAttribute"),
                ParseAttributeDeclarations(schema, "cbntAttribute"));
        }

        /// <summary>
        /// 解析 <c>&lt;tagAttribute&gt;</c> / <c>&lt;cbntAttribute&gt;</c> 声明（驱动额外认的属性）。
        /// 这些属性运行期落在 <c>TagDescriptor.Extras</c> / <c>TagCbntDescriptor.Extras</c>，
        /// schema 只能靠 <c>xs:anyAttribute</c> 放行，所以"有哪些、什么类型、什么文案"只能在这里声明。
        /// </summary>
        private static IReadOnlyList<DesignerFieldDto> ParseAttributeDeclarations(XElement schema, string localName)
        {
            var result = new List<(int Sort, DesignerFieldDto Field)>();
            foreach (var element in AppinfoChildren(schema, localName))
            {
                var field = ParseAttributeDeclaration(element);
                if (field is not null)
                {
                    // OrderBy 是稳定排序：没写 order 的保持 schema 里的声明顺序。
                    result.Add((field.Order ?? int.MaxValue, field));
                }
            }

            return result.OrderBy(r => r.Sort).Select(r => r.Field).ToArray();
        }

        private static DesignerFieldDto? ParseAttributeDeclaration(XElement element)
        {
            var name = (string?)element.Attribute("name");
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var type = (string?)element.Attribute("type");
            var enumValues = SplitList((string?)element.Attribute("enumValues"));
            var kind = (string?)element.Attribute("kind")
                ?? (enumValues is not null
                    ? DesignerFieldKind.Enum
                    : InferKind(type));

            return new DesignerFieldDto(
                Name: name!,
                Type: type,
                Kind: kind,
                Required: bool.TryParse((string?)element.Attribute("required"), out var required) && required,
                Default: (string?)element.Attribute("default"),
                EnumValues: enumValues,
                Min: ParseDecimal((string?)element.Attribute("min")),
                Max: ParseDecimal((string?)element.Attribute("max")),
                MinLength: int.TryParse((string?)element.Attribute("minLength"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var minLength)
                    ? minLength
                    : null,
                Label: (string?)element.Attribute("label"),
                Order: int.TryParse((string?)element.Attribute("order"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var order)
                    ? order
                    : null,
                Group: (string?)element.Attribute("group"),
                Placeholder: (string?)element.Attribute("placeholder"),
                Help: (string?)element.Attribute("help") ?? (string?)element.Attribute("description"),
                Children: null,
                AppliesToTypes: SplitList((string?)element.Attribute("appliesToTypes")));
        }

        /// <summary>逗号分隔的列表（去空白、去空项）；空则返回 <c>null</c>。</summary>
        private static IReadOnlyList<string>? SplitList(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var items = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return items.Length == 0 ? null : items;
        }

        /// <summary>没写 <c>kind</c> 时按 <c>type</c> 猜一个渲染类别（纯提示，猜错最多是控件类型不理想）。</summary>
        private static string InferKind(string? type)
        {
            var text = type ?? string.Empty;
            if (text.Contains("boolean", StringComparison.OrdinalIgnoreCase))
            {
                return DesignerFieldKind.Bool;
            }

            if (text.Contains("int", StringComparison.OrdinalIgnoreCase)
                || text.Contains("short", StringComparison.OrdinalIgnoreCase)
                || text.Contains("byte", StringComparison.OrdinalIgnoreCase)
                || text.Contains("decimal", StringComparison.OrdinalIgnoreCase)
                || text.Contains("double", StringComparison.OrdinalIgnoreCase)
                || text.Contains("float", StringComparison.OrdinalIgnoreCase)
                || text.Contains("number", StringComparison.OrdinalIgnoreCase))
            {
                return DesignerFieldKind.Number;
            }

            return DesignerFieldKind.String;
        }

        private static UiInfo? ParseUi(XElement owner)
        {
            var ui = AppinfoChild(owner, "ui");
            if (ui is null)
            {
                return null;
            }

            return new UiInfo(
                (string?)ui.Attribute("label"),
                int.TryParse((string?)ui.Attribute("order"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var order)
                    ? order
                    : null,
                (string?)ui.Attribute("group"),
                (string?)ui.Attribute("placeholder"),
                (string?)ui.Attribute("help") ?? (string?)ui.Attribute("description"),
                SplitList((string?)ui.Attribute("appliesToDrivers")));
        }

        /// <summary>
        /// 取 <c>xs:annotation/xs:appinfo</c> 下按<b>本地名</b>匹配的提示元素。<br/>
        /// 不能按"无命名空间"去找：<c>appinfo</c> 里的内容会继承 <c>xs:schema</c> 上的默认 <c>xmlns</c>，
        /// 而多个驱动 schema（modbustcp / zlan / hjzk / com / opcuaclient）都声明了
        /// <c>xmlns="tags:{driver}"</c>，于是 <c>&lt;driver&gt;</c>/<c>&lt;ui&gt;</c> 落进了目标命名空间。
        /// </summary>
        private static XElement? AppinfoChild(XElement owner, string localName) =>
            AppinfoChildren(owner, localName).FirstOrDefault();

        /// <summary>同上，取<b>全部</b>同名提示元素（如多个 <c>&lt;tagAttribute&gt;</c>）。</summary>
        private static IEnumerable<XElement> AppinfoChildren(XElement owner, string localName) =>
            owner.Element(Xs + "annotation")?
                 .Element(Xs + "appinfo")?
                 .Elements()
                 .Where(e => e.Name.LocalName == localName)
            ?? Enumerable.Empty<XElement>();
    }

    private sealed record DriverAnnotation(
        string Name,
        string? DisplayName,
        string? Prefix,
        IReadOnlyList<DesignerFieldDto> TagAttributes,
        IReadOnlyList<DesignerFieldDto> CbntAttributes);

    private sealed record UiInfo(
        string? Label,
        int? Order,
        string? Group,
        string? Placeholder,
        string? Help,
        IReadOnlyList<string>? AppliesToDrivers = null);
}
