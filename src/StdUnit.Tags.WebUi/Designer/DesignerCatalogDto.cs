namespace StdUnit.Tags.WebUi;

/// <summary>
/// 设计器元数据（"catalog"）：把<b>核心 schema + 所有已注册的驱动 schema</b> 内省成一份
/// JSON，供通道设计器 / 测点设计器渲染表单并导出 XML。<br/>
/// <br/>
/// 它<b>不</b>引入第二套事实源：结构与类型全部来自运行期真正用来校验的那套
/// <see cref="System.Xml.Schema.XmlSchemaSet"/>（核心 <see cref="TagsProjectSchema"/> 加上各驱动
/// 注册的 <see cref="ITagsProjectSchemaProvider"/>），文案/顺序/分组来自 XSD 里
/// <c>xs:annotation/xs:appinfo</c> 的 <c>&lt;ui&gt;</c> 与 <c>&lt;driver&gt;</c>（见各自的 .xsd 注释）。<br/>
/// <br/>
/// <b>它不负责校验导出结果</b>：导出后请直接走 <see cref="TagsProjectSchema.Validate(System.Xml.Linq.XElement)"/>——
/// catalog 只让表单"事前就长对"，最终裁判永远是 schema 本身。
/// </summary>
/// <param name="Version">catalog 自身的版本号（结构变更时递增，前端可据此降级）。</param>
/// <param name="Root">根元素 <c>&lt;Project&gt;</c> 的节点定义（<b>不在</b> <paramref name="Nodes"/> 内）。</param>
/// <param name="Nodes">除根以外的全部节点定义（<c>Channel</c> / <c>TagGrp</c> / <c>TagCbnt</c> / <c>Tag</c> / <c>Logicet</c>）。</param>
/// <param name="Drivers">各驱动通道的专属字段；按驱动名排序。</param>
/// <param name="TagTypes">
/// 内置测点类型（<see cref="BuiltinTagKinds"/>）。<b>只是提示</b>：schema 里 <c>type</c> 是开放字符串
/// （插件可注册自定义类型），设计器应允许自由输入。
/// </param>
public sealed record DesignerCatalogDto(
    int Version,
    DesignerNodeDto Root,
    IReadOnlyList<DesignerNodeDto> Nodes,
    IReadOnlyList<DesignerDriverDto> Drivers,
    IReadOnlyList<string> TagTypes);

/// <summary>
/// 一个 XML 节点（元素）的定义：属性 + 允许的子元素 + "还有 schema 看不到的扩展"的开关。<br/>
/// 对应 XSD 里的一个 <c>xs:complexType</c>（<c>Logicet</c> 这类纯文本元素例外，见 <see cref="TextContent"/>）。
/// </summary>
/// <param name="Name">元素名（本地名，核心 schema 无命名空间）。</param>
/// <param name="Label">显示名（来自 <c>xs:appinfo/ui@label</c>）。</param>
/// <param name="Attributes">该节点的属性（含默认值、枚举、范围）。</param>
/// <param name="Children">允许出现在其下的子元素名（核心 schema 内）。</param>
/// <param name="AllowsForeignElements">
/// 是否放行<b>其它命名空间</b>的子元素（<c>xs:any namespace="##other"</c>）。
/// 只有 <c>Channel</c> 会为 <c>true</c>：驱动专属字段就是在那里塞进去的。
/// </param>
/// <param name="AllowsCustomAttributes">
/// 是否有 <c>xs:anyAttribute</c>——意味着"schema 看不到的扩展属性"。<c>Tag</c> / <c>TagCbnt</c> /
/// <c>TagGrp</c> / <c>Channel</c> 都是 <c>true</c>（如 SimpleFiles 的 <c>autoCreateFile</c>、COM 的 <c>marker</c>）。
/// </param>
/// <param name="TextContent">纯文本元素的提示（<c>Logicet</c> 是 DLL 路径）；非纯文本元素为 <c>null</c>。</param>
public sealed record DesignerNodeDto(
    string Name,
    string? Label,
    IReadOnlyList<DesignerFieldDto> Attributes,
    IReadOnlyList<string> Children,
    bool AllowsForeignElements,
    bool AllowsCustomAttributes,
    string? TextContent);

/// <summary>
/// 一个"字段"：既表示属性（<see cref="DesignerNodeDto.Attributes"/>），也表示元素（驱动字段、嵌套组）。
/// </summary>
/// <param name="Name">属性名或元素名。</param>
/// <param name="Type">
/// 基础类型：<c>System.Xml.XmlTypeCode</c> 的名字（<c>String</c> / <c>Boolean</c> / <c>Int16</c> /
/// <c>UnsignedShort</c> / <c>PositiveInteger</c> …）；<see cref="DesignerFieldKind.Group"/> 时为 <c>null</c>。
/// </param>
/// <param name="Kind">
/// 前端该渲染成什么：<c>string</c> / <c>bool</c> / <c>number</c> / <c>enum</c> / <c>group</c>。
/// </param>
/// <param name="Required">属性是 <c>use="required"</c>；元素是 <c>minOccurs&gt;0</c>。</param>
/// <param name="Default">schema 声明的默认值（<c>default="…"</c>）；没有则为 <c>null</c>。</param>
/// <param name="EnumValues">枚举候选值（<see cref="DesignerFieldKind.Enum"/> 时非空）。</param>
/// <param name="Min">下界（<c>minInclusive</c>；<c>positiveInteger</c> 视为 1，<c>nonNegativeInteger</c> 视为 0）。</param>
/// <param name="Max">上界（<c>maxInclusive</c> / <c>maxExclusive</c>）。</param>
/// <param name="MinLength">最小长度（<c>xs:minLength</c>），用于提示"非空"。</param>
/// <param name="Label">显示名（<c>ui@label</c>）。</param>
/// <param name="Order">排序提示（<c>ui@order</c>）；<c>null</c> 表示按 schema 声明顺序。</param>
/// <param name="Group">分组（<c>ui@group</c>），如"连接"/"安全"。</param>
/// <param name="Placeholder">输入框占位符（<c>ui@placeholder</c>）。</param>
/// <param name="Help">帮助文本（<c>ui@help</c> / <c>ui@description</c>）。</param>
/// <param name="Children">嵌套子元素（<see cref="DesignerFieldKind.Group"/> 时非空，如 OpcUa 的 <c>ServerOpt</c>）。</param>
/// <param name="AppliesToTypes">
/// 仅对驱动声明的 <c>Tag</c> 属性有意义：只在测点 <c>type</c> 命中这些值时才显示（如 S7 的 <c>maxlen</c> 只对 <c>STR</c>）。
/// <c>null</c>/空表示不限。它只是<b>UI 提示</b>，不参与校验。
/// </param>
/// <param name="AppliesToDrivers">
/// 仅对该属性有意义：只有节点所在通道的驱动命中这些值时才显示（如 <c>TagCbnt</c> 的 <c>area</c>/<c>slave</c>
/// 只对 ModbusTcp / HjzkIoBox / ZLanTcp）。<c>null</c>/空表示不限。同样只是<b>UI 提示</b>：
/// 已经写了值的属性即使不适用也会显示，避免"看不见却被导出"。
/// </param>
public sealed record DesignerFieldDto(
    string Name,
    string? Type,
    string Kind,
    bool Required,
    string? Default,
    IReadOnlyList<string>? EnumValues,
    decimal? Min,
    decimal? Max,
    int? MinLength,
    string? Label,
    int? Order,
    string? Group,
    string? Placeholder,
    string? Help,
    IReadOnlyList<DesignerFieldDto>? Children,
    IReadOnlyList<string>? AppliesToTypes = null,
    IReadOnlyList<string>? AppliesToDrivers = null);

/// <summary>
/// 一个驱动通道的定义：<c>&lt;Channel driver="Name"&gt;</c> 下允许的专属子元素。<br/>
/// 驱动名 ↔ 驱动 schema 的绑定<b>不</b>由 XSD 推导（XSD 1.0 无法按属性值切换内容模型，
/// 且 <c>HjzkIoBox</c>/<c>tags:hjzk</c>、<c>ZLanTcp</c>/<c>tags:zlan</c> 这类名字也对不上），
/// 而是由驱动 schema 自己在 <c>xs:annotation/xs:appinfo/driver</c> 里声明。
/// </summary>
/// <param name="Name">
/// <c>driver</c> 属性的取值（与 <c>XxxNames.DriverName</c> 一致，如 <c>S7</c>）；取自
/// <c>&lt;driver name="…"/&gt;</c>。
/// </param>
/// <param name="DisplayName">显示名（<c>&lt;driver displayName="…"/&gt;</c>），缺省同 <paramref name="Name"/>。</param>
/// <param name="Namespace">驱动字段所属的 XML 命名空间（<c>tags:s7</c>），导出时据此声明 <c>xmlns</c>。</param>
/// <param name="Prefix">导出时建议的前缀（<c>&lt;driver prefix="s7"/&gt;</c>，缺省取命名空间后缀）。</param>
/// <param name="Fields">该驱动的专属子元素；按 <c>order</c> 再按声明顺序。</param>
/// <param name="TagAttributes">
/// 该驱动<b>额外</b>认的 <c>Tag</c> 属性（如 S7 的 <c>maxlen</c>、SimpleFiles 的 <c>autoCreateFile</c>）。
/// 它们不在核心 schema 里——XSD 1.0 不允许跨命名空间给既有类型追加属性，所以由驱动在 xsd 的
/// <c>xs:appinfo</c> 里<b>声明</b>（<c>&lt;tagAttribute&gt;</c>）。运行期这些属性落在
/// <c>TagDescriptor.Extras</c>，<b>不会</b>被 schema 校验拒绝。
/// </param>
/// <param name="CbntAttributes">该驱动额外认的 <c>TagCbnt</c> 属性（<c>&lt;cbntAttribute&gt;</c>），语义同上。</param>
public sealed record DesignerDriverDto(
    string Name,
    string? DisplayName,
    string Namespace,
    string Prefix,
    IReadOnlyList<DesignerFieldDto> Fields,
    IReadOnlyList<DesignerFieldDto> TagAttributes,
    IReadOnlyList<DesignerFieldDto> CbntAttributes);

/// <summary>
/// <see cref="DesignerFieldDto.Kind"/> 的取值（前端渲染依据）。
/// </summary>
public static class DesignerFieldKind
{
    /// <summary>自由文本。</summary>
    public const string String = "string";

    /// <summary>布尔（<c>xs:boolean</c>）：<c>true</c> / <c>false</c>。</summary>
    public const string Bool = "bool";

    /// <summary>数字（整数或浮点）。</summary>
    public const string Number = "number";

    /// <summary>枚举：从 <see cref="DesignerFieldDto.EnumValues"/> 里选。</summary>
    public const string Enum = "enum";

    /// <summary>有子字段的复合元素（如 OpcUa 的 <c>ServerOpt</c>）。</summary>
    public const string Group = "group";
}
