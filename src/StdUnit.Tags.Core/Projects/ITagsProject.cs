
using System.Threading.Channels;
using System.Xml.Linq;

namespace StdUnit.Tags;


/// <summary>
/// 外部写入入口组变更意图的委托。
/// </summary>
/// <param name="entry"></param>
/// <param name="ct"></param>
/// <returns></returns>
public delegate ValueTask TagGrpWriteIntent(ITagGrp entry, CancellationToken ct);

/// <summary>
/// 外部写入入口组变更意图的委托 + 委托完成的TaskCompletionSource。
/// </summary>
/// <param name="Intent"></param>
/// <param name="Completion">
/// 这里之所以使用泛型版本的 <see cref="TaskCompletionSource{TResult}"/>，是因为netframework版本不支持非泛型的 <see cref="TaskCompletionSource"/>
/// </param>
public record IntentCompletion(TagGrpWriteIntent Intent, TaskCompletionSource<int> Completion);


/// <summary>
/// 一个测点项目，包含通道、测点、逻辑等信息。<br/>
/// 测点项目往往由<see cref="ITagsProjectFactory"/>按需构建。
/// </summary>
public interface ITagsProject : IDisposable
{
    /// <summary>
    /// 通道
    /// </summary>
    IReadOnlyList<ITagChannel> Channels { get; }

    /// <summary>
    /// 逻辑
    /// </summary>
    IList<ILogicet> Logicets { get; }

    /// <summary>
    /// 测点
    /// </summary>
    ITagGrp Tags { get; }

    /// <summary>
    /// 项目根目录
    /// </summary>
    string? ProjectRoot { get; }

    /// <summary>
    /// 用于返回最近一次 <see cref="Initialize(string, XElement?)"/> 的根元素，它是传入的 root 或从 index.xml 加载的。<br/>
    /// </summary>
    XElement? GetRootElement();

    /// <summary>
    /// 某个入口的<b>运行器开始工作</b>（含崩溃恢复后的重连）：<b>每个入口各触发一次</b>，不是"项目启动一次"，
    /// 它的运行器会再次进入轮询循环时也会再触发。要观测"项目整体启停"请用
    /// <see cref="ITagsProjectCtrl.StartedOrStopped"/>。<br/>
    /// <b>多播</b>：按注册顺序<b>串行</b> await 全部订阅者（总耗时相加）；<b>运行期订阅/退订从下一次通知开始生效</b>。
    /// <b>任一订阅者抛异常即中断</b>（后续订阅者本次不再被调用），异常原样抛出——等价于该入口本轮失败，
    /// 进入 <see cref="RunnerCrashed"/> 的恢复路径。<br/>
    /// 回调运行在轮询线程上，必须尽快返回（重活请投递到自己的队列）。观察者若比项目活得短，需要自己 <c>-=</c>。<br/>
    /// 回调拿到的是项目的生命周期取消令牌（<see cref="ITagsProjectCtrl.StopAsync"/> 时取消）。
    /// </summary>
    event RunnerStarted? RunnerStarted;

    /// <summary>
    /// 某个入口的轮询崩溃，透传自该入口运行器的 <see cref="ITagGrpRunner.RunnerCrashed"/>。<br/>
    /// <b>取消不是崩溃</b>：项目停止导致的取消<b>不会</b>触发本事件。<br/>
    /// 通常来说，在该委托正常结束后，项目会自行重启轮询循环。<br/>
    /// 但需要注意的是，这个自行重启是指<b>事件委托没有再向外抛出异常</b>的情况：<br/>
    /// 在<see cref="RunnerCrashed"/>中外抛异常会被视作对应入口需要<b>停机</b>！<br/>
    /// <b>多播</b>：按注册顺序串行 await 全部订阅者；<b>任一订阅者抛异常即中断</b>（排在它后面的订阅者本次收不到通知），
    /// 异常原样抛出并按"该入口停机"处理。所以告警/计数类观察者请自己 <c>try/catch</c>——不要把"自己不出错"的责任交给别人。<br/>
    /// 运行期订阅/退订从下一次通知开始生效；回调运行在轮询线程上，必须尽快返回。
    /// </summary>
    event RunnerCrashed? RunnerCrashed;


    /// <summary>
    /// 为<b>指定入口</b>设置"轮次处理完成"回调（<b>读取输入 + 逻辑处理</b> 之后、<b>刷写输出</b> 之前触发），
    /// 等价于该入口的 <see cref="ITagGrpRunner.TurnProcess"/>。<br/>
    /// <b>每个入口最多一个回调；重复设置是替换（不是追加）。</b>需要多个观察者时，请自行组合成一个
    /// <see cref="TurnProcess"/> 再设置——这样调用顺序由你在组合处显式决定，比"按注册顺序"更可控：<br/>
    /// <code>
    /// proj.SetTurnProcessHandler("g1", async (entry, channel, ct) =>
    /// {
    ///     await 遥测(entry, channel, ct);
    ///     await 推送(entry, channel, ct);
    /// });
    /// </code>
    /// <br/>
    /// <b>运行前、运行中都可以设置</b>：入口列表在 <see cref="Initialize(string, XElement?)"/> 之后固定，本方法只替换该入口的回调；
    /// 运行期设置/清除<b>从下一轮开始生效</b>（不中断正在执行的那一轮）。常见设置点是
    /// <see cref="ITagsProjectCtrl.StartPollAsync(string?, XElement?, Func{ITagsProject, IServiceProvider, CancellationToken, Task})"/>
    /// 的 <c>hook</c> 或 <see cref="ITagsProjectCtrl.StartedOrStopped"/> 的"已启动"事件（两者都在轮询开始前同步执行），
    /// 但<b>晚挂同样有效</b>——例如观测端（界面推送流）比轮询晚启动时，可以直接在项目运行中补挂。<br/>
    /// 不要用"反复设置/清除"来做动态开关：每个入口只有一个回调，那样会与其它观察者互相覆盖；
    /// 要动态开关就在自己的回调内部判断（例如先检查订阅者集合是否为空再直接返回）。<br/>
    /// <br/>
    /// <b>回调运行在轮询线程上，必须尽快返回</b>：不要阻塞，也不要在回调里做 IO / 序列化。
    /// 需要较重的处理时，请只把必要信息投递到自己的队列，由独立的后台循环消费。<br/>
    /// <br/>
    /// <b>不同入口的回调是并行的</b>（各入口由各自的运行器并行驱动）。同一个回调实例若被设置到多个入口，
    /// 必须自行保证线程安全。<br/>
    /// <br/>
    /// 回调签名是 <c>(entry, channel, ct)</c>：<c>ct</c> 是项目的生命周期取消令牌。
    /// 回调里发起的异步操作请把它透传下去，否则"停止项目"要等到 <see cref="ITagsProjectCtrl.StopAsync"/> 的等待超时才收场。<br/>
    /// <br/>
    /// 回调抛出的异常<b>不会被吞掉</b>：它等价于该入口本轮失败（与 <see cref="ILogicet"/> 抛异常同义），
    /// 会进入 <see cref="RunnerCrashed"/> 的恢复路径。观察者请自行 try/catch，不要让观测逻辑影响轮询。<br/>
    /// <br/>
    /// 注意：
    /// <list type="number">
    /// <item>必须在 <see cref="Initialize(string, XElement?)"/> 之后调用（运行期也可以：从下一轮开始生效）；</item>
    /// <item>入口名不存在时抛 <see cref="KeyNotFoundException"/>（与 <c>WriteIntent</c> 的校验一致）；</item>
    /// <item>不需要清除：回调的作用域就是项目实例本身，项目停止后实例被释放，回调随之消失；</item>
    /// <item>只覆盖"产生了轮次"的情形：入口被禁用、项目未启动或已停止时不会产生轮次，也就不会调用；
    ///       生命周期请用 <see cref="ITagsProjectCtrl.StartedOrStopped"/> 与 <see cref="RunnerStarted"/>。</item>
    /// </list>
    /// </summary>
    /// <param name="entry">入口测点组的名称（即 <c>entry.TagName()</c>）</param>
    /// <param name="handler">回调；传 <c>null</c> 表示清除该入口已设置的回调</param>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> 为 null</exception>
    /// <exception cref="KeyNotFoundException">未找到指定的入口测点组</exception>
    /// <exception cref="ObjectDisposedException">项目已释放（回调的作用域就是项目实例）</exception>
    void SetTurnProcessHandler(string entry, TurnProcess? handler);


    /// <summary>
    /// 初始化，如果root为空，则默认取 projRoot下的index.xml文件
    /// </summary>
    /// <param name="projRoot"></param>
    /// <param name="root"></param>
    void Initialize(string projRoot, XElement? root = null);

    /// <summary>
    /// 运行本项目下所有的入口测点组。<br/>
    /// 这个方法在所有入口组都运行结束之前，不会返回！
    /// <para>
    /// 启动是<b>两阶段</b>的：先对全部入口算出逻辑组件匹配（纯计算），再准备 runner 并启动轮询。
    /// 匹配/准备阶段失败时<b>不会有入口处于"已经跑起来"的状态</b>；启动阶段失败则会<b>取消并等待已启动的入口退出</b>后
    /// 再把原异常抛出，不会留下"Task 无人等待、令牌永不取消"的孤儿轮询。
    /// </para>
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">项目未初始化（通道/测点/逻辑组件为空，或没有任何入口测点组）</exception>
    /// <exception cref="TagsProjectConfigurationException">
    /// 为入口匹配逻辑组件时 <see cref="ILogicet.MatchEntry"/> / <see cref="ILogicet.Order"/> 抛出了异常。
    /// 这是确定性的配置错误，不走 <see cref="RunnerCrashed"/> 的重试路径（也不会启动任何入口）。
    /// </exception>
    Task RunAsync(CancellationToken ct);


    #region

    /// <summary>
    /// 意图的容量。<br/>
    /// 仅在运行之前有效，运行过程中不允许修改。
    /// </summary>
    int IntentCapacity { get; set; }


    /// <summary>
    /// 写入意图，必须在 <see cref="Initialize(string, XElement?)"/> 之后调用
    /// </summary>
    /// <param name="entry"></param>
    /// <param name="intent"></param>
    /// <param name="task">代表意图是否被执行，如果写入失败，则直接设为异常</param>
    bool WriteIntent(string entry, TagGrpWriteIntent intent, out Task task);

    /// <summary>
    /// 写入意图
    /// </summary>
    /// <param name="entry"></param>
    /// <param name="intent"></param>
    [Obsolete("Use WriteIntent(string entry, TagGrpWriteIntent intent, out Task task) instead.")]
    bool WriteIntent(string entry, TagGrpWriteIntent intent);


    /// <summary>
    /// 获取意图通道读取器。<br/>
    /// </summary>
    /// <param name="entry"></param>
    /// <returns></returns>
    ChannelReader<IntentCompletion>? GetIntentReader(string entry);
    #endregion
}