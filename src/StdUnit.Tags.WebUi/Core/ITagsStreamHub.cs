using StdUnit.Tags.WebUi.Internal;

namespace StdUnit.Tags.WebUi;

internal interface ITagsStreamHub
{
    /// <summary>当前 epoch（每次项目启动递增）。</summary>
    long Epoch { get; }

    /// <summary>取当前结构树。项目未运行时返回 <see cref="TreeDto.IsStarted"/> = false 的空树。</summary>
    TreeDto GetTree();

    /// <summary>
    /// 建立一个FramesPiper。<paramref name="entries"/> 为 null 表示订阅全部入口。<br/>
    /// 返回的订阅者<b>已经</b>入队了当前状态与各入口的全量值基线（在同一把锁内完成，不会漏帧），
    /// </summary>
    SseFramesPiper Subscribe(IReadOnlySet<string>? entries);

    void RemovePiper(SseFramesPiper piper);

    /// <summary>
    /// 重同步帧：当前状态 + 该订阅者关心的各入口全量值。
    /// </summary>
    IReadOnlyList<ReadOnlyMemory<byte>> BuildSyncFrames(SseFramesPiper piper);
}
