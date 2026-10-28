using System.Text.Json.Serialization;

namespace StdUnit.Tags.WebStream;

/// <summary>
/// 测点树的一个节点（<b>只有结构，没有值</b>）。<br/>
/// 值走 <c>/stdunit.tags/stream</c> 的 <c>values</c> 事件，本结构在一个 <see cref="TreeDto.Epoch"/> 内是静态的。<br/>
/// <br/>
/// 这是一个<b>封闭的多态类型</b>：线上的判别字段是 <c>kind</c>（<c>grp</c> / <c>cbnt</c> / <c>tag</c>），
/// 每种节点只带自己那几个字段，而不是把所有字段拍扁成一大堆可空列。
/// </summary>
/// <param name="Path">
/// 相对项目根组的路径，<c>/</c> 分隔（根为空串），语法与 <see cref="ITagGrp.Descendant(string)"/> 的 <c>path</c> 一致。
/// </param>
/// <param name="Parent">父节点路径；根为 <c>null</c>。</param>
/// <param name="Depth">根为 0。</param>
/// <param name="Name">节点名称。</param>
/// <param name="ChannelName">解析到的通道名（可能来自上层继承）。</param>
/// <param name="AccessMode">冒泡后的有效访问模式（<c>RW</c> / <c>RO</c> / <c>R1W</c> / <c>WO</c>）。</param>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(TagGrpNodeDto), "grp")]
[JsonDerivedType(typeof(TagCbntNodeDto), "cbnt")]
[JsonDerivedType(typeof(TagLeafNodeDto), "tag")]
public abstract record TagNodeDto(
    string Path,
    // 根的 Parent 是 null：**必须写出来**（其余可空字段省略），否则前端分不清"根"与"字段缺失"。
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Parent,
    int Depth,
    string Name,
    string? ChannelName,
    string? AccessMode);

/// <summary>
/// 测点组节点（<see cref="ITagGrp"/>）。比基类多了入口标识、使能与扫描间隔。
/// </summary>
/// <param name="Path">见 <see cref="TagNodeDto"/>。</param>
/// <param name="Parent">见 <see cref="TagNodeDto"/>。</param>
/// <param name="Depth">见 <see cref="TagNodeDto"/>。</param>
/// <param name="Name">见 <see cref="TagNodeDto"/>。</param>
/// <param name="ChannelName">见 <see cref="TagNodeDto"/>。</param>
/// <param name="AccessMode">见 <see cref="TagNodeDto"/>。</param>
/// <param name="EntryKey">
/// 入口测点组非 null：把它作为 <c>?entries=</c> 与 <c>values.entry</c> 的键（等于 <c>entry.TagName()</c>）。
/// </param>
/// <param name="IsEntry">配置上是否为入口（嵌套入口不生效，只有最外层才有 <paramref name="EntryKey"/>）。</param>
/// <param name="IsEnabled">自动轮询是否使能（运行时值）。</param>
/// <param name="ScanIntervalMs">冒泡后的有效扫描间隔（毫秒）；null 表示未配置。</param>
public sealed record TagGrpNodeDto(
    string Path,
    string? Parent,
    int Depth,
    string Name,
    string? ChannelName,
    string? AccessMode,
    string? EntryKey,
    bool IsEntry,
    bool IsEnabled,
    int? ScanIntervalMs)
    : TagNodeDto(Path, Parent, Depth, Name, ChannelName, AccessMode);

/// <summary>
/// 组合测点节点（<see cref="ITagCbnt"/>）。其子节点只可能是 <see cref="TagLeafNodeDto"/>。
/// </summary>
/// <param name="Path">见 <see cref="TagNodeDto"/>。</param>
/// <param name="Parent">见 <see cref="TagNodeDto"/>。</param>
/// <param name="Depth">见 <see cref="TagNodeDto"/>。</param>
/// <param name="Name">见 <see cref="TagNodeDto"/>。</param>
/// <param name="ChannelName">见 <see cref="TagNodeDto"/>。</param>
/// <param name="AccessMode">见 <see cref="TagNodeDto"/>。</param>
/// <param name="IsEnabled">自动轮询是否使能（运行时值）。</param>
/// <param name="StartAddress">起始地址。</param>
public sealed record TagCbntNodeDto(
    string Path,
    string? Parent,
    int Depth,
    string Name,
    string? ChannelName,
    string? AccessMode,
    bool IsEnabled,
    string? StartAddress)
    : TagNodeDto(Path, Parent, Depth, Name, ChannelName, AccessMode);

/// <summary>
/// 叶测点节点（<see cref="ITag"/>，含 <see cref="ITagCbntor"/>）。
/// </summary>
/// <param name="Path">见 <see cref="TagNodeDto"/>。</param>
/// <param name="Parent">见 <see cref="TagNodeDto"/>。</param>
/// <param name="Depth">见 <see cref="TagNodeDto"/>。</param>
/// <param name="Name">见 <see cref="TagNodeDto"/>。</param>
/// <param name="ChannelName">见 <see cref="TagNodeDto"/>。</param>
/// <param name="AccessMode">见 <see cref="TagNodeDto"/>。</param>
/// <param name="TagKind"><see cref="BuiltinTagKinds"/> 中的类型名。</param>
/// <param name="Address">规范化后的地址。</param>
/// <param name="TagSize">占用字节数。</param>
/// <param name="IsReadOnly">是否只读。</param>
public sealed record TagLeafNodeDto(
    string Path,
    string? Parent,
    int Depth,
    string Name,
    string? ChannelName,
    string? AccessMode,
    string? TagKind,
    string? Address,
    int? TagSize,
    bool IsReadOnly)
    : TagNodeDto(Path, Parent, Depth, Name, ChannelName, AccessMode);

/// <summary>
/// 测点树结构（扁平节点列表，前端按 <see cref="TagNodeDto.Parent"/> 自行组树）。
/// </summary>
/// <param name="Epoch">代际号：每次项目启动递增。客户端发现 epoch 变化应整树替换。</param>
/// <param name="IsStarted">取树时项目是否在运行；false 时 <paramref name="Nodes"/> 为空。</param>
/// <param name="RootName">根节点名称（<see cref="TagNodeDto.Path"/> 为空串的那个节点）。</param>
/// <param name="Nodes">
/// 扁平节点列表（根在最前，其余按树的深度优先顺序）。<br/>
/// 元素是 <see cref="TagGrpNodeDto"/> / <see cref="TagCbntNodeDto"/> / <see cref="TagLeafNodeDto"/> 之一，
/// 线上对应 <c>kind</c> = <c>grp</c> / <c>cbnt</c> / <c>tag</c>。
/// </param>
public sealed record TreeDto(
    long Epoch,
    bool IsStarted,
    string RootName,
    IReadOnlyList<TagNodeDto> Nodes);
