using System.Diagnostics.Metrics;

namespace StdUnit.Tags;

/// <summary>
/// 指标的<b>实现</b>：创建 <see cref="Meter"/>、持有各 instrument、提供给运行器调用的记录方法。<br/>
/// 名称 / 单位 / tag 键的契约常量统一在公开的 <see cref="TagsMetricContract"/> 里，这里只引用、不重复定义。<br/>
/// 无监听者时 <c>Counter.Add</c> / <c>Histogram.Record</c> 是零分配、纳秒级，所以没有"是否启用"的开关。
/// </summary>
internal static class TagsMetric
{
    private static readonly Meter Meter = CreateMeter();

    /// <summary>
    /// 有意<b>不</b>传 version：AOT/裁剪下用反射读程序集自定义特性不可靠（元数据可能被裁掉，结果静默退化成 0.0.0）。<br/>
    /// 打包脚本现在会注入 <c>InformationalVersion</c>，但零反射的读取方式（构建期生成常量）还没做，
    /// 所以这里先不报版本，而不是用反射凑一个可能错误的值。
    /// </summary>
    private static Meter CreateMeter() => new(TagsMetricContract.MeterName);

    /// <summary>
    /// 入口维度：位置路径——唯一且基数有界。<br/>
    /// 有意<b>不</b>带测点名/地址（无界基数）；也<b>不</b>带 <c>driver</c>——失败可能来自入口下的辅通道，
    /// 拿主通道去标注会把责任算错，宁可少一个维度也不要错误的数据。<br/>
    /// 为什么返回数组而不是单个键值对：<c>Counter.Add</c> / <c>Histogram.Record</c> 的入参就是
    /// <c>KeyValuePair&lt;string, object?&gt;[]</c>（或它的 span），而 <c>TagList</c> 是 <c>ref struct</c>，
    /// 不能存进字段。数组是唯一“建一次、每轮复用、零额外分配”的形状；将来加维度也只改这里。
    /// </summary>
    public static KeyValuePair<string, object?>[] TagsFor(ITagGrp entry) =>
        new[] {
            new KeyValuePair<string, object?>(TagsMetricContract.EntryTag, entry.GetLocationPath())
        };

    private static readonly Counter<long> Turns =
        Meter.CreateCounter<long>(
            TagsMetricContract.Turns,
            TagsMetricContract.TurnsUnit,
            "入口完整走完的轮次数（读入 → 逻辑 → 刷写）"
        );

    private static readonly Histogram<double> TurnDuration =
        Meter.CreateHistogram<double>(
            TagsMetricContract.TurnDuration,
            TagsMetricContract.TurnDurationUnit,
            "每轮耗时：从 EnsureConnected 到刷写输出（不含轮间等待）"
        );

    private static readonly Counter<long> Failures =
        Meter.CreateCounter<long>(
            TagsMetricContract.Failures,
            TagsMetricContract.FailuresUnit,
            "入口轮次崩溃次数（停机引起的取消不计）"
        );

    private static readonly Counter<long> Reconnects =
        Meter.CreateCounter<long>(
            TagsMetricContract.Reconnects,
            TagsMetricContract.ReconnectsUnit,
            "入口崩溃后重新建连并成功走完建连阶段的次数"
        );

    /// <summary>
    /// 记录一轮完成。调用方必须传入<b>复用</b>的 tag 数组（每轮新建会抵消"指标本身零分配"的意义）。
    /// </summary>
    public static void RecordTurn(KeyValuePair<string, object?>[] tags, TimeSpan elapsed)
    {
        Turns.Add(1, tags);
        TurnDuration.Record(elapsed.TotalSeconds, tags);
    }

    /// <summary>记录一次崩溃。</summary>
    public static void RecordFailure(KeyValuePair<string, object?>[] tags) => Failures.Add(1, tags);

    /// <summary>记录一次崩溃后的恢复（重新走完建连阶段）。</summary>
    public static void RecordReconnect(KeyValuePair<string, object?>[] tags) => Reconnects.Add(1, tags);
}