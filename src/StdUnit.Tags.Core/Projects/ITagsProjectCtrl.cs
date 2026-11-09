using System.Xml.Linq;

namespace StdUnit.Tags;

/// <summary>
/// 测点项目的运行状态。<br/>
/// 只有 <see cref="Running"/> 与 <see cref="Stopped"/> 是"稳定态"，
/// 也是 <see cref="ITagsProjectCtrl.StartedOrStopped"/> 唯一会派发的两个状态。<br/>
/// <see cref="Starting"/> / <see cref="Stopping"/> 是过渡态，只用于让 <see cref="ITagsProjectCtrl.State"/>
/// 能如实反映"正在启动/正在停止"，不会单独派发事件。
/// </summary>
public enum TagsProjectState
{
    /// <summary>未运行：从未启动、已停止，或启动失败收尾之后。</summary>
    Stopped = 0,

    /// <summary>正在启动：项目实例已创建，正在执行 <c>hook</c>，轮询尚未开始。</summary>
    Starting = 1,

    /// <summary>运行中：<c>hook</c> 已执行完，轮询进行中。此时 <see cref="ITagsProjectCtrl.Project"/> 必不为 <c>null</c>。</summary>
    Running = 2,

    /// <summary>正在停止：已发出取消，正在等待轮询退出、释放项目、断开通道。</summary>
    Stopping = 3,
}

/// <summary>
/// 测点项目控制器接口。
/// 核心方法是<br/>
/// - <see cref="StartPollAsync(string?, XElement?, Func{ITagsProject, IServiceProvider, CancellationToken, Task})"/>：用于启动测点项目轮询。<br/>
/// - <see cref="StopAsync"/>：方法用于停止测点项目轮询。<br/>
/// </summary>
public interface ITagsProjectCtrl
{
    /// <summary>
    /// 当前运行状态，可随时查询（不依赖事件）。<br/>
    /// <b>判断"是否已就绪"请用 <see cref="TryGetRunning"/></b>：
    /// <see cref="Project"/> 在 <c>hook</c> 执行之前就已赋值（此时状态是 <see cref="TagsProjectState.Starting"/>），
    /// 单看 <c>Project != null</c> 会早于"项目确实已就绪"。
    /// </summary>
    TagsProjectState State { get; }

    /// <summary>
    /// 当前（或最近一次）运行的标识：每次 <c>StartPollAsync</c> 递增，与
    /// <see cref="TagsProjectEventArgs.RunId"/> 同源。<br/>
    /// 用于把"事件"和"事后补查到的状态"对上。<br/>
    /// <b>需要与 <see cref="Project"/> 成对使用（判断"现在跑的是哪一次"）时，请改用
    /// <see cref="TryGetRunning"/>：</b>两个属性是两次独立读取，"停止后立刻重启"的窗口里可能读到
    /// 「旧项目 + 新 RunId」这种不一致的组合。
    /// </summary>
    long RunId { get; }

    /// <summary>
    /// 一次读取拿到"正在运行的项目 + 它的运行标识"，<b>保证两者来自同一次运行</b>。<br/>
    /// 返回值等价于"读到的瞬间 <see cref="State"/> 是 <see cref="TagsProjectState.Running"/> 且
    /// <see cref="Project"/> 非 null"，但状态、项目、运行号是同一次快照读出的，
    /// 不存在"分两次读、中间被一次启停插进来"的问题。观察者晚启动、需要补查当前状态时，用它最稳。<br/>
    /// <br/>
    /// <b>观察者晚于项目启动时，用它补偿</b>：先订阅 <see cref="StartedOrStopped"/>，
    /// 再调用本方法一次，覆盖"订阅之前已经发生的变化"。
    /// </summary>
    /// <param name="project">正在运行的项目</param>
    /// <param name="runId">该项目的运行标识，与 <see cref="TagsProjectEventArgs.RunId"/> 同源</param>
    /// <returns>当前处于 <see cref="TagsProjectState.Running"/> 时为 <c>true</c></returns>
    bool TryGetRunning(out ITagsProject project, out long runId);

    /// <summary>
    /// 测点项目，<see cref="TagsProjectState.Starting"/> / <see cref="TagsProjectState.Running"/> 时非 null，
    /// 其余状态为 <c>null</c>。<br/>
    /// <br/>
    /// 注意，非<c>null</c>并不意味着项目在执行。要获取当前正在执行的项目，使用<see cref="TryGetRunning"/>。
    /// </summary>
    ITagsProject? Project { get; }

    /// <summary>
    /// 当正在启动测点项目时发生异常时的回调。<br/>
    /// 返回值表示是否已经处理了异常，如果返回true，则不会再向外层抛出异常。<br/>
    /// <b>该属性在每次读取时取值</b>：启动过程中被替换不会影响本次启动已取到的委托。
    /// </summary>
    Func<Exception, Task<bool>>? OnStartingException { get; set; }

    /// <summary>
    /// 测点项目启动或停止事件。<b>只在两个稳定态上派发</b>（<see cref="TagsProjectState.Running"/> / <see cref="TagsProjectState.Stopped"/>）：<br/>
    /// <list type="bullet">
    /// <item><b>配对由库保证</b>：只要派发过一次"已启动"，本次运行结束前<b>必定</b>派发一次"已停止"，
    /// 两者的 <see cref="TagsProjectEventArgs.RunId"/> 相同；启动失败、<c>RunAsync</c> 异常退出都算运行结束，
    /// 结束时 <see cref="TagsProjectEventArgs.Failure"/> 带异常，正常停机为 <c>null</c>。</item>
    /// <item><b>热事件</b>：只在状态迁移时派发一次，晚订阅者收不到历史通知。
    /// 需要"当前是否已启动"请查 <see cref="State"/>，需要"现在就是这个状态"请自行补偿。</item>
    /// <item><b>订阅者抛异常不影响启停</b>：逐个 <c>try/catch</c> 并记 Error 日志，后续订阅者仍会收到通知
    /// （与 <see cref="ITagsProject.RunnerStarted"/> 的 fast-fail 语义不同——项目级观测是纯旁路，
    /// 要参与启动的初始化请用 <c>hook</c>）。</item>
    /// <item>派发"已停止"事件时，轮询已退出、项目已释放、通道已断开，<see cref="TagsProjectEventArgs.Project"/> 为 <c>null</c>；
    /// 此时可以安全复用通道实例、目录，或替换 XML。</item>
    /// </list>
    /// </summary>
    event TagsProjectStartedOrStopped? StartedOrStopped;

    /// <summary>
    /// 启动测点项目轮询。<br/>
    /// 此方法通常不会结束，除非测点项目被停止或在启动阶段发生异常。<br/>
    /// 如果在项目启动阶段就发生异常，则会触发<see cref="OnStartingException"/>回调。<br/>
    /// 状态与会话：进入时 <see cref="State"/> 变为 <see cref="TagsProjectState.Starting"/>（此时 <see cref="Project"/> 已非 null、
    /// <c>hook</c> 正在执行），<c>hook</c> 完成后变为 <see cref="TagsProjectState.Running"/> 并派发"已启动"；
    /// 无论正常结束还是异常结束，退出前都会回到 <see cref="TagsProjectState.Stopped"/>，
    /// 并且只要派发过"已启动"，就必定补派一次"已停止"（异常时 <see cref="TagsProjectEventArgs.Failure"/> 非 null）。<br/>
    /// 示例：
    /// <example><![CDATA[
    /// ctrl.StartPollAsync(dir: "D:\\MyProject", root: null, hook: async (proj, sp, ct) =>{
    ///     proj.Logicets.Add(new MyLogicet(proj.Channels, proj.Tags)); 
    ///     // ...
    ///     // 其它事件绑定
    /// });
    /// ]]></example>
    /// <br/>
    /// 注意1：<see cref="StopAsync"/> 会等待轮询循环退出（有界），所以「<c>await StopAsync()</c> 之后再调用本方法」是安全的；
    /// 但不要"发了停止就不管"地并发启动，否则旧循环的收尾可能清理掉刚创建的项目。<br/>
    /// <br/>
    /// 注意2：hook回调的第二个参数 <c>IServiceProvider</c> 的有效期与本次启动的测点项目一致，
    /// 即从 hook 调用开始，直到 <see cref="StartPollAsync"/> 返回（测点项目停止）为止。
    /// 因此，在 hook 内使用 <c>sp</c> 创建 Logicet 等随项目一起销毁的对象并持有 <c>sp</c> 是安全的；
    /// 但请勿将其捕获进生命周期超出测点项目运行期的对象（如静态字段、注册在根容器的单例等）中延迟使用。
    /// </summary>
    /// <param name="dir"></param>
    /// <param name="root">如果为null，则使用dir下的index.xml构建项目</param>
    /// <param name="hook">项目启动之前的回调，你可以在这里注册<see cref="ILogicet"/>、绑定事件等操作</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    Task StartPollAsync(string? dir, XElement? root, Func<ITagsProject, IServiceProvider, CancellationToken, Task> hook);

    /// <summary>
    /// 停止测点项目轮询，会导致测点项目停止并释放相关资源。<br/>
    /// 语义：<b>发取消信号 → 等待轮询循环退出（有界）→ 释放项目 → 断开通道 → 触发 <see cref="StartedOrStopped"/> 的"已停止"事件</b>。<br/>
    /// 因此返回后可以安全地复用通道实例/项目目录或替换 XML；"已停止"事件也因此是确定性信号。<br/>
    /// 唯一的例外是底层驱动不响应取消：此时等待会在 <c>PollExitWaitTimeout</c>（默认 30s）后超时，
    /// 记一条 Warning 并继续清理，不再等待循环退出。<br/>
    /// <b>未运行时调用是空操作</b>：不会派发"已停止"（重复调用也只会派发一次）。
    /// </summary>
    /// <returns></returns>
    Task StopAsync();
}


/// <summary>
/// 测点项目启动或停止事件参数
/// </summary>
public class TagsProjectEventArgs : EventArgs
{
    /// <summary>
    /// c'tor。控制器与自定义 <see cref="ITagsProjectCtrl"/> 实现都用它构造事件参数——
    /// 只有 <see cref="State"/> 这一个"真相"，不再存在"bool 与 State 各说各话"的可能。
    /// </summary>
    /// <param name="state">本次通知对应的状态（只会是 <see cref="TagsProjectState.Running"/> 或 <see cref="TagsProjectState.Stopped"/>）</param>
    /// <param name="project">项目实例；停止事件传 <c>null</c></param>
    /// <param name="runId">运行标识；自定义实现没有这个概念时可留默认值</param>
    /// <param name="failure">导致本次运行结束的异常；正常停机传 <c>null</c></param>
    public TagsProjectEventArgs(TagsProjectState state, ITagsProject? project, long runId = 0, Exception? failure = null)
    {
        this.State = state;
        this.Project = project;
        this.RunId = runId;
        this.Failure = failure;
    }

    /// <summary>
    /// 测点项目。
    /// 如果是停止事件，则为null。
    /// </summary>
    public ITagsProject? Project { get; }

    /// <summary>
    /// 本次通知对应的状态：只会是 <see cref="TagsProjectState.Running"/> 或 <see cref="TagsProjectState.Stopped"/>。
    /// </summary>
    public TagsProjectState State { get; }

    /// <summary>
    /// 运行标识：每次 <c>StartPollAsync</c> 递增。"已启动"与"已停止"的 <c>RunId</c> 相同，
    /// 可以据此配对、关联日志（例如界面推送流用它区分不同批次的快照）。
    /// </summary>
    public long RunId { get; }

    /// <summary>
    /// 导致本次运行结束的异常：启动失败或 <c>RunAsync</c> 异常退出时非 null；
    /// 正常停机（<c>StopAsync</c>）为 null。"已启动"通知里恒为 null。
    /// </summary>
    public Exception? Failure { get; }
}

/// <summary>
/// 测点项目启动或停止事件委托
/// </summary>
/// <param name="sender"></param>
/// <param name="args"></param>

public delegate void TagsProjectStartedOrStopped(ITagsProjectCtrl sender, TagsProjectEventArgs args);