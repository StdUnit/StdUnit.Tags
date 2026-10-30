using System.Threading.Channels;

namespace StdUnit.Tags.WebUi.Internal;

/// <summary>
/// 一个 SSE 订阅者：一个有界发送队列 + 一个写循环。<br/>
/// 生产端（发送循环/请求线程）只做 <see cref="Enqueue"/>（非阻塞），队列满则丢弃并标记重同步——
/// <b>绝不反压到轮询线程或发送循环</b>。
/// </summary>
internal sealed class SseFramesPiper : IDisposable
{
    /// <summary>
    /// 队列里的"唤醒信号"占位帧（真实帧永远非空）。<see cref="MarkResync"/> 只置标志不足以唤醒
    /// 已阻塞在 <see cref="ChannelReader{T}.WaitToReadAsync"/> 上的写循环，所以顺手塞一个空帧。
    /// </summary>
    private static readonly ReadOnlyMemory<byte> WakeUpSignal = ReadOnlyMemory<byte>.Empty;

    private readonly ITagsStreamHub _hub;
    private readonly Channel<ReadOnlyMemory<byte>> _queue;
    private readonly IReadOnlySet<string>? _entries;
    private int _needsResync;
    private int _disposed;

    public SseFramesPiper(ITagsStreamHub hub, IReadOnlySet<string>? entries, int capacity)
    {
        this._hub = hub;
        this._entries = entries;
        this._queue = Channel.CreateBounded<ReadOnlyMemory<byte>>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait, // 配合 TryWrite：满时返回 false，而不是静默丢弃
            SingleReader = true,
            SingleWriter = false,
        });
    }

    /// <summary>该订阅者是否关心指定入口（<c>?entries=</c> 为空表示全部）。</summary>
    public bool ContainsEntry(string entry) => this._entries is null || this._entries.Contains(entry);

    /// <summary>入队一帧（已渲染的 UTF-8 SSE 字节）；队列满就丢弃这一帧并标记需要重同步。</summary>
    public void Enqueue(ReadOnlyMemory<byte> frame)
    {
        if (!this._queue.Writer.TryWrite(frame))
        {
            this.MarkResync();
        }
    }

    /// <summary>
    /// 标记需要重同步，并<b>唤醒</b>可能正阻塞在 <see cref="ChannelReader{T}.WaitToReadAsync"/> 上的写循环。<br/>
    /// 只置标志是不够的：写循环"刚读完队列里的最后一帧"与"标志置位"之间总有窗口，错过这一次唤醒，
    /// 重同步就要等到下一次心跳/增量才会被处理（订阅者表现为迟迟收不到基线）。
    /// </summary>
    public void MarkResync()
    {
        if (Interlocked.Exchange(ref this._needsResync, 1) == 0)
        {
            // 队列满时 TryWrite 失败没关系：那说明写循环并不空闲，它回到循环顶部仍会看到标志。
            this._queue.Writer.TryWrite(WakeUpSignal);
        }
    }

    /// <summary>
    /// 写循环：把队列里的帧写进响应体。<br/>
    /// </summary>
    public async Task PipeFramesToStreamAsync(Stream body, CancellationToken ct)
    {
        await WriteFrameAsync(body, SseFrames.Retry, ct).ConfigureAwait(false);

        while (!ct.IsCancellationRequested)
        {
            // 需要重新同步?
            if (Interlocked.Exchange(ref this._needsResync, 0) == 1)
            {
                foreach (var frame in this._hub.BuildSyncFrames(this))
                {
                    await WriteFrameAsync(body, frame, ct).ConfigureAwait(false);
                }
                continue;
            }

            if (!await this._queue.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                break;
            }

            while (this._queue.Reader.TryRead(out var frame))
            {
                if (frame.IsEmpty)
                {
                    // 唤醒信号：真正的工作在循环顶部的标志位上做。
                    continue;
                }

                await WriteFrameAsync(body, frame, ct).ConfigureAwait(false);
            }
        }
    }

    private static async ValueTask WriteFrameAsync(Stream body, ReadOnlyMemory<byte> bytes, CancellationToken ct)
    {
        await body.WriteAsync(bytes, ct).ConfigureAwait(false);
        await body.FlushAsync(ct).ConfigureAwait(false);
    }


    public void Dispose()
    {
        if (Interlocked.Exchange(ref this._disposed, 1) != 0)
        {
            return;
        }

        this._hub.RemovePiper(this);
        this._queue.Writer.TryComplete();
    }

}
