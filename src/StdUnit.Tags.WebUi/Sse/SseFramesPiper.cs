using System.Threading.Channels;

namespace StdUnit.Tags.WebUi.Internal;

/// <summary>
/// 一个 SSE 订阅者：一个有界发送队列 + 一个写循环。<br/>
/// 生产端（发送循环/请求线程）只做 <see cref="Enqueue"/>（非阻塞），队列满则丢弃并标记重同步——
/// <b>绝不反压到轮询线程或发送循环</b>。
/// </summary>
internal sealed class SseFramesPiper : IDisposable
{
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

    public void MarkResync() => Interlocked.Exchange(ref this._needsResync, 1);

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
