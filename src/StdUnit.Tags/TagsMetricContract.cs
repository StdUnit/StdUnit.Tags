namespace StdUnit.Tags;

/// <summary>
/// 本库运行期指标的**契约**：Meter 名、指标名、单位与 tag 键。<br/>
/// 使用方按这些字面量订阅与查询，因此它们<b>不会改名</b>——改名等于让对方的 dashboard 静默变空，
/// 把单位从 <c>s</c> 改成 <c>ms</c> 会让曲线错 1000 倍，而两边都不会有编译错误。只会新增，不会重命名。<br/>
/// 改动流程：先改 CHANGELOG（按破坏性变更写）+ README 的指标表，再改这里，最后同步
/// <c>MetricsContractTests</c> 里的"字面量钉子"——那条测试故意与字面量比对，就是为了拦住"顺手改名"。
/// </summary>
public static class TagsMetricContract
{
    /// <summary>Meter 名，使用方按它订阅：<c>AddMeter(TagsMetricContract.MeterName)</c>。</summary>
    /// <remarks>
    /// 固定为全家族共用的名字，不要按运行器/子系统拆 Meter：使用方订阅一次即可覆盖将来新增的所有指标，
    /// 而 <c>dotnet-counters --counters StdUnit.Tags</c> 是精确匹配，拆细之后就看不到了。
    /// </remarks>
    public const string MeterName = "StdUnit.Tags";

    /// <summary>指标名：入口完整走完的轮次数（读入 → 逻辑 → 刷写）。</summary>
    public const string Turns = "stdunit.tags.turns";

    /// <summary>指标单位：轮次。</summary>
    public const string TurnsUnit = "{turn}";

    /// <summary>指标名：每轮耗时（从 EnsureConnected 到刷写输出，不含轮间等待）。</summary>
    public const string TurnDuration = "stdunit.tags.turn.duration";

    /// <summary>指标单位：秒。不要改成 <c>ms</c>——OTel 约定与既有 dashboard 都按秒。</summary>
    public const string TurnDurationUnit = "s";

    /// <summary>指标名：入口轮次崩溃次数（停机引起的取消不计）。</summary>
    public const string Failures = "stdunit.tags.failures";

    /// <summary>指标单位：次（失败）。</summary>
    public const string FailuresUnit = "{failure}";

    /// <summary>指标名：入口崩溃后重新建连并成功走完建连阶段的次数。</summary>
    public const string Reconnects = "stdunit.tags.reconnects";

    /// <summary>指标单位：次（重连）。</summary>
    public const string ReconnectsUnit = "{reconnect}";

    /// <summary>唯一的 tag 键：入口的定位路径</summary>
    public const string EntryTag = "entry";
}