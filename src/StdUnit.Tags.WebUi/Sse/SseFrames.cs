using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace StdUnit.Tags.WebUi.Internal;

/// <summary>
/// SSE 帧渲染。<b>序列化只在这里发生一次</b>，之后所有订阅者共用同一份字节。<br/>
/// 帧就是以 UTF-8 字节直接表示的，没有包装类型：渲染结果是
/// <c>event: &lt;name&gt;\ndata: &lt;json&gt;\n\n</c>。
/// </summary>
internal static class SseFrames
{
    private static readonly byte[] EventPrefix = "event: "u8.ToArray();

    /// <summary>
    /// 每个 <c>data:</c> 行的前缀，<b>带前导换行</b>：既负责终结前一行（首行 data 时终结 <c>event</c> 行，
    /// 续行时终结上一行 data），也是续行本身的前缀。两处字节完全相同，所以共用一份。
    /// </summary>
    private static readonly byte[] DataLinePrefix = "\ndata: "u8.ToArray();

    private static readonly byte[] FrameSuffix = "\n\n"u8.ToArray();

    /// <summary>告诉 EventSource 重连退避。</summary>
    public static ReadOnlyMemory<byte> Retry { get; } = "retry: 3000\n\n"u8.ToArray();

    /// <summary>心跳（注释行，客户端忽略）。</summary>
    public static ReadOnlyMemory<byte> Ping { get; } = ": ping\n\n"u8.ToArray();

    public static ReadOnlyMemory<byte> State(StateDto state) =>
        Encode("state", JsonSerializer.SerializeToUtf8Bytes(state, TagsStreamJsonContext.Default.StateDto));

    public static ReadOnlyMemory<byte> Values(EntryValuesDto values) =>
        Encode("values", JsonSerializer.SerializeToUtf8Bytes(values, TagsStreamJsonContext.Default.EntryValuesDto));

    /// <summary>
    /// 渲染一帧。<paramref name="json"/> <b>必须是单行</b> UTF-8 JSON，本方法不按行拆分。<br/>
    /// JSON 规范要求字符串里的控制字符（含 <c>\r</c> / <c>\n</c>）一律转义成
    /// <c>\r</c> / <c>\n</c> 字面量，而这里用的是不带缩进的 <c>SerializeToUtf8Bytes</c>，
    /// 所以 0x0A / 0x0D 不可能出现在序列化结果里。<br/>
    /// </summary>
    private static ReadOnlyMemory<byte> Encode(string eventName, byte[] json)
    {
        using var buffer = new MemoryStream(json.Length + 64);

        buffer.Write(EventPrefix);
        buffer.Write(Encoding.UTF8.GetBytes(eventName));
        buffer.Write(DataLinePrefix);
        buffer.Write(json);
        buffer.Write(FrameSuffix);
        return buffer.ToArray();
    }
}
