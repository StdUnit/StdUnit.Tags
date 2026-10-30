namespace StdUnit.Tags.WebUi;

/// <summary>
/// <c>POST /stdunit.tags/value</c> 的请求体：把某个测点的值改成 <paramref name="Raw"/>。
/// </summary>
/// <param name="Path">测点路径（与 <see cref="TagNodeDto.Path"/> 一致）。</param>
/// <param name="Raw">
/// 值的文本形式；服务端按 <see cref="ITag.TagDescriptor"/> 的 <c>TagKind</c> 精确解析
/// （<b>不做跨类型转换</b>）。布尔类（<c>BIT</c>/<c>DI</c>/<c>DO</c>）接受 <c>true/false/1/0</c>。
/// </param>
public sealed record WriteTagValueRequest(string? Path, string? Raw);

/// <summary>
/// <c>POST /stdunit.tags/rescan</c> 的请求体：把某个测点标记为"未扫描"（<c>R1W</c> 测点会强制重读）。
/// </summary>
/// <param name="Path">测点路径（与 <see cref="TagNodeDto.Path"/> 一致）。</param>
public sealed record RescanTagRequest(string? Path);

/// <summary>
/// 命令执行结果。<paramref name="Error"/> 为 null 表示成功。
/// </summary>
/// <param name="Ok">是否成功。</param>
/// <param name="Error">失败原因（成功时为 null，序列化时省略）。</param>
public sealed record CommandResult(bool Ok, string? Error);
