using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using StdUnit.Tags.SimpleFiles.Compat;

namespace StdUnit.Tags.SimpleFiles;

/// <summary>
/// Json 类型的直接标签。<br/>
/// <br/>
/// <typeparamref name="T"/> 的序列化元数据有两条来源：
/// <list type="bullet">
/// <item><b>源生成元数据</b>（AOT / 裁剪下唯一可用）：由注册时登记的 <c>JsonSerializerContext</c> 解析而来，
/// 见 <c>TagsProject_Extensions.AddJsonSerializerContext</c> 与 <c>SimpleFilesDirectTagBuilder.WithJsonTagFactory</c>；</item>
/// <item><b>反射</b>（<c>typeInfo</c> 为 null 时）：仅在 <c>JsonSerializer.IsReflectionEnabledByDefault</c>
/// 为 true（普通 JIT 宿主）时可用；NativeAOT / 裁剪宿主下 STJ 会关掉反射，故这里直接以
/// <see cref="TagsProjectNotSupportedException"/> 快速失败，而不是等第一次读写时才抛 STJ 的
/// "Reflection-based serialization has been disabled"。</item>
/// </list>
/// </summary>
/// <typeparam name="T">测点值类型。JSON 测点通常是一个 POCO，也可以是基元类型。</typeparam>
public class JsonDirectTag<T> : SimpleFilesDirectTagBase<T>
{
    private readonly JsonTypeInfo? _jsonTypeInfo;

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="thisChannel"></param>
    /// <param name="container"></param>
    /// <param name="jsonTypeInfo">
    /// <typeparamref name="T"/> 的序列化元数据（取自 <c>JsonSerializerContext</c>）。<br/>
    /// 为 null 时退化为反射序列化，仅普通 JIT 宿主可用。
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="jsonTypeInfo"/> 与 <typeparamref name="T"/> 不匹配</exception>
    /// <exception cref="TagsProjectNotSupportedException">未提供元数据，而当前宿主禁用了反射序列化（NativeAOT / 裁剪）时</exception>
    public JsonDirectTag(TagDescriptor descriptor, SimpleFilesTagChannel? thisChannel, TagContainer container, JsonTypeInfo? jsonTypeInfo = null)
     : base(descriptor, thisChannel, container)
    {
        if (jsonTypeInfo is not null && jsonTypeInfo.Type != typeof(T))
        {
            throw new ArgumentException(
                $"{nameof(jsonTypeInfo)} 是 {jsonTypeInfo.Type.FullName} 的元数据，与测点值类型 {typeof(T).FullName} 不匹配",
                nameof(jsonTypeInfo));
        }

        if (jsonTypeInfo is null)
        {
            // 没有元数据就只能走反射：宿主禁用反射（NativeAOT / 裁剪）时在此快速失败
            JsonReflectionCompat.EnsureReflectionSupported(
                typeof(T), $"Tag({this.TagName()})", JsonReflectionCompat.IsSupported);
        }

        this._jsonTypeInfo = jsonTypeInfo;
    }

    /// <summary>
    /// 解析 JSON 字符串为对象
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    /// <remarks>
    /// 反射分支的告警在此处定点抑制：反射的**可用性**已由 ctor 经
    /// <see cref="JsonReflectionCompat.EnsureReflectionSupported"/> 校验过，
    /// 这里只是把库内部的传导噪声去掉。
    /// </remarks>
#if NET5_0_OR_GREATER
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = JsonReflectionCompat.MetadataHint)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = JsonReflectionCompat.MetadataHint)]
#endif
    protected override T? ParseValue(string text)
    {
        if (this._jsonTypeInfo is null)
        {
            return JsonSerializer.Deserialize<T>(text);
        }

        return (T?)JsonSerializer.Deserialize(text, this._jsonTypeInfo);
    }

    /// <summary>
    /// 格式化对象为JSON字符串
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <inheritdoc cref="ParseValue(string)" path="/remarks"/>
#if NET5_0_OR_GREATER
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = JsonReflectionCompat.MetadataHint)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = JsonReflectionCompat.MetadataHint)]
#endif
    protected override string FormatValue(T? value)
    {
        if (this._jsonTypeInfo is null)
        {
            return JsonSerializer.Serialize(value);
        }

        return JsonSerializer.Serialize((object?)value, this._jsonTypeInfo);
    }
}
