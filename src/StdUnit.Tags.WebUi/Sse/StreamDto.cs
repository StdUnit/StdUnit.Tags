namespace StdUnit.Tags.WebUi;

/// <summary>
/// 一个测点的值。
/// </summary>
/// <param name="Path">与 <see cref="TagNodeDto.Path"/> 一致。</param>
/// <param name="TagKind">冗余的 <see cref="BuiltinTagKinds"/> 类型名，前端不必回查树。</param>
/// <param name="Raw">
/// 值的文本形式（<c>Convert.ToString(value, InvariantCulture)</c>）。<br/>
/// 用字符串而不是 JSON 数字：AOT 下不能靠运行时类型元数据序列化 <c>object</c>，
/// 且 JS 的 number 只有 double，<c>INT64</c>/<c>UINT64</c> 会静默丢精度。
/// </param>
/// <param name="TimestampUtc">对应 <see cref="ITag.Timestamp"/>（UTC）——该测点最近一次被读/写的时刻。</param>
/// <param name="IsDirty">是否有未刷写的改动。</param>
/// <param name="IsScanned">本轮是否已被扫描。</param>
public sealed record TagValueDto(
    string Path,
    string? TagKind,
    string? Raw,
    DateTime TimestampUtc,
    bool IsDirty,
    bool IsScanned);

/// <summary>
/// 容器的运行时标志（目前只有使能状态）。数量远小于测点数，所以单独一个数组。<br/>
/// 只有 <see cref="ITagGrp"/> / <see cref="ITagCbnt"/> 有这些标志（<see cref="ITag"/> 没有 <c>IsEnabled</c>）。
/// </summary>
/// <param name="Path">与 <see cref="TagNodeDto.Path"/> 一致。</param>
/// <param name="IsEnabled">自动轮询是否使能。</param>
public sealed record ContainerFlagDto(string Path, bool IsEnabled);

/// <summary>
/// <c>values</c> 事件：<b>一个入口</b>的一次轮次的结果。<br/>
/// 一帧内的值来自该入口的同一次轮次；<b>不同入口的帧互不同步</b>（各入口并行、各自有扫描周期）。
/// </summary>
/// <param name="Epoch">代际号；与客户端当前 epoch 不一致时整帧丢弃。</param>
/// <param name="Entry">入口键（<c>entry.TagName()</c>）。</param>
/// <param name="Seq">该入口内单调递增的序号；用于发现缺口（缺口 ⇒ 请求重新订阅或等待下一次 <c>Full</c>）。</param>
/// <param name="SampledAtUtc">本次轮次完成时刻（UTC）——"这批数据是什么时候采的"。</param>
/// <param name="Full">
/// true 表示这是该入口**全部测点**的当前值（订阅基线 / 丢帧后的重同步）：客户端应整体替换该入口的值表；<br/>
/// false 表示增量：只更新 <paramref name="Values"/> 里出现的测点。
/// </param>
/// <param name="Values">值列表（增量模式下只含发生变化的测点）。</param>
/// <param name="ContainerFlags">该入口内容器的使能状态变化（无变化时为 null）。</param>
public sealed record EntryValuesDto(
    long Epoch,
    string Entry,
    long Seq,
    DateTime SampledAtUtc,
    bool Full,
    IReadOnlyList<TagValueDto> Values,
    IReadOnlyList<ContainerFlagDto>? ContainerFlags);

/// <summary>
/// <c>state</c> 事件：项目生命周期状态。<br/>
/// 新订阅者在连接建立时一定会先收到一帧当前状态（等价于 Rx 的 <c>Replay(1)</c>）。
/// </summary>
/// <param name="Epoch">代际号：每次启动递增，停止事件里保持即停止前的值。</param>
/// <param name="IsStarted">项目是否在运行。</param>
/// <param name="ProjectRoot">项目根目录（停止后保留最后一次的值）。</param>
public sealed record StateDto(
    long Epoch,
    bool IsStarted,
    string? ProjectRoot);
