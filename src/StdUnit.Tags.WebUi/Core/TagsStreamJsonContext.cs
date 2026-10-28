using System.Text.Json.Serialization;

namespace StdUnit.Tags.WebUi;

/// <summary>
/// 线协议的 JSON 元数据（源生成）。<br/>
/// 所有写出的 JSON 都必须走这里的 <c>JsonTypeInfo&lt;T&gt;</c> 重载，
/// 否则 NativeAOT / 裁剪宿主下会因为缺少运行时类型元数据而失败。
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(TreeDto))]
[JsonSerializable(typeof(StateDto))]
[JsonSerializable(typeof(EntryValuesDto))]
[JsonSerializable(typeof(WriteTagValueRequest))]
[JsonSerializable(typeof(RescanTagRequest))]
[JsonSerializable(typeof(CommandResult))]
internal sealed partial class TagsStreamJsonContext : JsonSerializerContext
{
}
