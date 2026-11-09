using System;

namespace StdUnit.Tags;

/// <summary>
/// "给正在运行的项目挂一段观测"的通用封装。
/// </summary>
public static class TagsProjectCtrlAttachmentExtensions
{
    /// <summary>
    /// 把一段观测挂到"当前正在运行的项目"上，并在项目停止、换运行、或返回的句柄被 <c>Dispose</c> 时自动摘掉。<br/>
    /// <br/>
    /// 调用方只需要提供 <paramref name="attach"/>，其余样板由本方法负责：
    /// <list type="number">
    /// <item><b>晚挂补偿</b>：先订阅 <see cref="ITagsProjectCtrl.StartedOrStopped"/>，再立刻补查一次
    /// （<see cref="ITagsProjectCtrl.TryGetRunning"/>）——观察者比项目晚启动时，"已启动"那次热事件已经过去了。</item>
    /// <item><b>去重</b>：补查与事件可能描述同一次运行，按 <c>RunId</c> 判定，<paramref name="attach"/> 每次运行最多调用一次。</item>
    /// <item><b>配对摘除</b>：收到"已停止"、或发现 <c>RunId</c> 变了，先 Dispose 上一次 <paramref name="attach"/> 返回的句柄，再挂新的。</item>
    /// <item><b>串行化</b>：内部加锁，"事件线程"与"调用方线程"同时触发也不会重复挂载。</item>
    /// </list>
    /// <br/>
    /// <b>异常</b>：<paramref name="attach"/> 抛出的异常在构造时原样抛出（同时会撤掉事件订阅，不留悬挂）；
    /// 若是在"已启动"事件里触发，则由 <see cref="ITagsProjectCtrl.StartedOrStopped"/> 的派发器接住并记日志，
    /// 项目启停不受影响；该次运行不会被标记为已挂载，下一次通知会重试。<br/>
    /// <br/>
    /// <b>回调线程</b>：<paramref name="attach"/> / 上一次返回的 <c>Dispose</c> 运行在
    /// <c>StartPollAsync</c> / <c>StopAsync</c> 的执行上下文（不是轮询线程），应当尽快返回。
    /// </summary>
    /// <param name="ctrl">项目控制器</param>
    /// <param name="attach">
    /// 挂载回调：参数是正在运行的项目及其运行标识（与 <see cref="TagsProjectEventArgs.RunId"/> 同源）。
    /// 返回的句柄会在解挂时被 <c>Dispose</c>；可以返回一个把多个订阅打包起来的复合句柄。
    /// </param>
    /// <returns>解挂句柄：<c>Dispose</c> 即退订事件并摘掉当前挂载（幂等）</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ctrl"/> 或 <paramref name="attach"/> 为 null</exception>
    public static IDisposable AttachRunningProject(
        this ITagsProjectCtrl ctrl,
        Func<ITagsProject, long, IDisposable> attach)
    {
        if (ctrl is null)
        {
            throw new ArgumentNullException(nameof(ctrl));
        }

        if (attach is null)
        {
            throw new ArgumentNullException(nameof(attach));
        }

        return new Attachment(ctrl, attach);
    }

    /// <summary>一次"挂到运行中项目"的会话。</summary>
    private sealed class Attachment : IDisposable
    {
        private readonly object _gate = new();
        private readonly ITagsProjectCtrl _ctrl;
        private readonly Func<ITagsProject, long, IDisposable> _attach;

        private IDisposable? _subscription;

        /// <summary>已挂载的运行标识；<c>-1</c> 表示当前没有挂载（<c>RunId</c> 从 1 开始）。</summary>
        private long _attachedRunId = -1;

        private bool _disposed;

        internal Attachment(ITagsProjectCtrl ctrl, Func<ITagsProject, long, IDisposable> attach)
        {
            this._ctrl = ctrl;
            this._attach = attach;

            // 顺序不能反：先订阅再补查，才覆盖"订阅之前已经发生的变化"。
            ctrl.StartedOrStopped += this.OnStartedOrStopped;
            try
            {
                this.Sync();
            }
            catch
            {
                // 构造失败不能让事件订阅留在外面（否则 _ctrl 会一直引用一个半死的对象）。
                ctrl.StartedOrStopped -= this.OnStartedOrStopped;
                throw;
            }
        }

        private void OnStartedOrStopped(ITagsProjectCtrl sender, TagsProjectEventArgs args) => this.Sync();

        /// <summary>让"当前挂载"与"当前运行"对齐：该挂的挂、该摘的摘、已挂过的跳过。</summary>
        private void Sync()
        {
            lock (this._gate)
            {
                if (this._disposed)
                {
                    return;
                }

                // TryGetRunning 是单次读取：项目与 RunId 必定来自同一次运行。
                if (!this._ctrl.TryGetRunning(out var project, out var runId))
                {
                    this.Detach();          // 已停止
                    return;
                }

                if (runId == this._attachedRunId)
                {
                    return;                 // 同一次运行：补查与事件都走到过这里
                }

                this.Detach();              // 换运行了：先摘旧的

                var subscription = this._attach(project, runId);
                this._subscription = subscription;
                this._attachedRunId = runId;   // 即便回调返回 null，也算处理过这次运行
            }
        }

        /// <summary>摘掉当前挂载（幂等）。</summary>
        private void Detach()
        {
            var subscription = this._subscription;
            this._subscription = null;
            this._attachedRunId = -1;

            subscription?.Dispose();
        }

        public void Dispose()
        {
            lock (this._gate)
            {
                if (this._disposed)
                {
                    return;
                }

                this._disposed = true;
                this._ctrl.StartedOrStopped -= this.OnStartedOrStopped;
                this.Detach();
            }
        }
    }
}
