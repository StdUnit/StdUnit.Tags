using System;
using System.Text.Json;

namespace StdUnit.Tags.SimpleFiles.Compat;

/// <summary>
/// "宿主是否允许反射序列化"这一宿主能力的判定与兜底，供 SimpleFiles 的 JSON 测点使用
/// （调用点：<see cref="SimpleFilesDirectTagBuilder"/> 解析元数据、<see cref="JsonDirectTag{T}"/> 直接构造）。<br/>
/// <br/>
/// 集中到一处的理由：缺少源生成元数据时的失败语义与提示文案只有一份；宿主能力的读取只有一处。
/// 真实 AOT 宿主下的行为由 <c>samples/AotSmokeTest.SimpleFiles</c> 端到端验证。
/// </summary>
internal static class JsonReflectionCompat
{
    /// <summary>
    /// 当前宿主是否允许反射序列化。<br/>
    /// <c>PublishAot=true</c> / 裁剪宿主下为 false——那时 SDK 会把 feature switch
    /// <c>System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault</c> 写进 runtimeconfig.json。<br/>
    /// 注意该开关被 STJ **首次访问即固化**，进程内事后改 AppContext 是无效的，
    /// 所以"false 分支"在 JIT 测试里只能靠给 <see cref="EnsureReflectionSupported"/> 传参来覆盖。
    /// </summary>
    internal static bool IsSupported => JsonSerializer.IsReflectionEnabledByDefault;

    /// <summary>
    /// 缺少源生成元数据时的提示文本。<br/>
    /// 同时用作 trim / AOT 告警的 <c>Justification</c>，所以必须是编译期常量。
    /// </summary>
    internal const string MetadataHint =
        "请在注册时用 AddJsonSerializerContext(...) 登记生成该类型的 JsonSerializerContext，"
        + "JSON 测点才能在不允许反射序列化的宿主（NativeAOT / 裁剪）上工作。";

    /// <summary>
    /// 校验"没有源生成元数据、只能退化到反射序列化"这一路径的宿主能力，不允许就快速失败。
    /// </summary>
    /// <param name="type">测点值类型</param>
    /// <param name="location">定位上下文，如 <c>Tag(json-v)</c></param>
    /// <param name="reflectionSupported">
    /// 宿主能力，由调用方传入 <see cref="IsSupported"/>。<br/>
    /// 做成参数而不是内部直接读：这样"允许/不允许"两条分支在 JIT 测试里都能覆盖，
    /// 而真实取值下 false 分支只有 AOT / 裁剪宿主才走得到。
    /// </param>
    /// <exception cref="TagsProjectNotSupportedException">宿主禁用了反射序列化时</exception>
    internal static void EnsureReflectionSupported(Type type, string location, bool reflectionSupported)
    {
        if (reflectionSupported)
        {
            return;
        }

        throw new TagsProjectNotSupportedException(
            $"当前宿主（NativeAOT / 裁剪）禁用了反射序列化，测点值类型 {type.FullName} 无法退化到反射序列化：{MetadataHint}",
            location);
    }
}
