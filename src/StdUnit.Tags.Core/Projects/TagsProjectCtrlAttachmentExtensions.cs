using System;

namespace StdUnit.Tags;

/// <summary>
/// 把一段观测挂到<b>当前正在运行的 <see cref="ITagsProject"/></b>（tagsproj）上。
/// </summary>
public static class TagsProjectCtrlAttachmentExtensions
{
    /// <summary>
    /// 把一段观测挂到<b>当前正在运行的 <see cref="ITagsProject"/></b>（tagsproj）上。<br/>
    /// <br/>
    /// <paramref name="attach"/> 的每一次挂载都只对应<b>一个正在运行中的</b> tagsproj：
    /// 项目停止或换运行时，先自动释放其上一次挂载，再自动挂载到新运行的 tagsproj 上。<br/>
    /// <br/>
    /// 注意：本方法<b>对 <c>projctrl.StartedOrStopped</c> 事件不会自动退订</b>：<c>StartedOrStopped</c> 是感知"下一次运行开始了"的唯一途径，
    /// 因此只有调用 <c>Dispose()</c> 本方法返回的句柄才会退订它。
    /// 不释放，则可以一直自动 attach 到新的项目运行；而一旦释放，则立刻退订 <see cref="ITagsProjectCtrl.StartedOrStopped"/> 并摘掉当前挂载（幂等）。
    /// </summary>
    /// <param name="ctrl">项目控制器（projctrl）</param>
    /// <param name="attach">
    /// 对某一次运行的 tagsproj 的挂载：参数是本次运行的 tagsproj 与运行标识（与 <see cref="TagsProjectEventArgs.RunId"/> 同源）。
    /// <c>attach</c> 返回的句柄在相应的 tagsproj 运行结束时，会被自动释放；可以把多个订阅打包成一个复合句柄返回。
    /// </param>
    /// <returns>对 projctrl 的 <c>StartedOrStopped</c> 事件的退订句柄。释放时会退订事件并摘掉当前挂载。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ctrl"/> 或 <paramref name="attach"/> 为 null</exception>
    public static IDisposable AttachRunningProject(this ITagsProjectCtrl ctrl, Func<ITagsProject, long, IDisposable> attach)
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

    /// <summary>
    /// 一次"挂到某个 tagsproj"的会话；持有 projctrl 的启停事件订阅直到 <see cref="Dispose"/>。
    /// </summary>
    private sealed class Attachment : IDisposable
    {
        private readonly object _gate = new();
        private readonly ITagsProjectCtrl _projctrl;
        private readonly Func<ITagsProject, long, IDisposable> _attach;

        private IDisposable? _projSubscription;

        /// <summary>已挂载的运行标识；<c>-1</c> 表示当前没有挂载（<c>RunId</c> 从 1 开始）。</summary>
        private long _attachedRunId = -1;

        private bool _disposed;

        internal Attachment(ITagsProjectCtrl projctrl, Func<ITagsProject, long, IDisposable> attach)
        {
            this._projctrl = projctrl;
            this._attach = attach;

            // 晚挂补偿：先订阅再补查。顺序不能反——观察者比项目晚启动时，"已启动"那次热事件已经过去了，
            // 只有订阅之后紧接着 TryGetRunning 才能补上。
            projctrl.StartedOrStopped += this.OnStartedOrStopped;
            try
            {
                this.Sync();
            }
            catch
            {
                // 构造失败不能把事件订阅留在外面（否则 projctrl 会一直引用一个半死的对象）。
                projctrl.StartedOrStopped -= this.OnStartedOrStopped;
                throw;
            }
        }

        private void OnStartedOrStopped(ITagsProjectCtrl sender, TagsProjectEventArgs args) => this.Sync();

        /// <summary>
        /// 让"当前挂载"与"当前这次运行"对齐：该挂的挂、该摘的摘、已挂过的跳过。
        /// 加锁串行化"事件线程"与"调用方线程"；按 RunId 去重，补查与事件描述同一次运行时不重复挂载。
        /// </summary>
        private void Sync()
        {
            lock (this._gate)
            {
                if (this._disposed)
                {
                    return;
                }

                // 单次读取：tagsproj 与 RunId 必定来自同一次运行。
                if (!this._projctrl.TryGetRunning(out var project, out var runId))
                {
                    this.DetachProj();          // 已停止
                    return;
                }

                if (runId == this._attachedRunId)
                {
                    return;                 // 同一次运行：补查与事件都走到过这里
                }

                this.DetachProj();              // 换运行了：先摘旧的

                var subscription = this._attach(project, runId);
                this._projSubscription = subscription;
                this._attachedRunId = runId;   // 即便回调返回 null，也算处理过这次运行
            }
        }

        /// <summary>
        /// 摘掉<b>当前这次运行</b>的挂载（幂等）。<br/>
        /// 有意<b>不</b>在这里退订 projctrl 的 <see cref="ITagsProjectCtrl.StartedOrStopped"/>：本方法在"停止"与"换运行"时都会被调用，
        /// 而"下一次运行开始了"只能靠那条订阅感知——在这里 <c>-=</c> 会让句柄从此失活，再也跟不上重启。
        /// 退订只发生在 <see cref="Dispose"/>。
        /// </summary>
        private void DetachProj()
        {
            var subscription = this._projSubscription;
            this._projSubscription = null;
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

                // 先退订（Sync 开头的 _disposed 判定保证之后到达的通知不会复活挂载），再摘掉当前这次运行的挂载。
                this._disposed = true;
                this._projctrl.StartedOrStopped -= this.OnStartedOrStopped;
                this.DetachProj();
            }
        }
    }
}
