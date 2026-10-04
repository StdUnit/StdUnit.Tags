using System.Threading.Channels;

namespace StdUnit.Tags;


/// <summary>
/// <see cref="ITagGrpRunner"/>中有一轮处理
/// </summary>
/// <param name="grp">入口测点组</param>
/// <param name="channel">
/// 入口的<b>主通道</b>（<see cref="ITagGrpExtensions.SearchChannel"/> 的解析结果，可能为 null）。<br/>
/// 入口下若还挂了其它通道，运行器会自动为它们建连，但不会在这里逐个暴露；
/// 需要用到辅通道时，请自行从 <paramref name="grp"/> 上解析（如 <c>grp.SelectGrp("grp2").SearchChannel()</c>）。
/// </param>
/// <returns></returns>
public delegate Task TurnProcess(ITagGrp grp, ITagChannel? channel);

/// <summary>
/// <see cref="ITagGrpRunner"/>中有轮训启动
/// </summary>
/// <param name="grp">入口测点组</param>
/// <param name="channel">
/// 入口的<b>主通道</b>（<see cref="ITagGrpExtensions.SearchChannel"/> 的解析结果，可能为 null）。
/// </param>
/// <returns></returns>
public delegate Task RunnerStarted(ITagGrp grp, ITagChannel? channel);

/// <summary>
/// <see cref="ITagGrpRunner"/>中有错误出现
/// </summary>
/// <param name="grp">入口测点组</param>
/// <param name="channel">
/// 入口的<b>主通道</b>（<see cref="ITagGrpExtensions.SearchChannel"/> 的解析结果，可能为 null）；
/// 清理路径断开的是入口相关的<b>全部</b>通道，不只是它。
/// </param>
/// <param name="ex"></param>
/// <returns></returns>
public delegate Task RunnerCrashed(ITagGrp grp, ITagChannel? channel, Exception ex);


/// <summary>
/// 测点群组运行器。<br/>
/// 这个运行期运行机制类似于一个PLC。它遵循:<br/>
///     <b>读取输入</b>-&gt;<b>处理</b>-&gt;<b>输出</b> 
/// 的循环。<br/>
/// 一旦成功启动循环，除非主动取消或者被异常处理的二次异常打断，否则不会停止。
/// <para>
/// <b>多通道</b>：一个入口下可以挂多个通道。每轮在读写之前，运行器会先为入口子树中
/// 所有会被用到的通道（见 <see cref="ITagGrpExtensions.CollectChannels"/>）逐一执行
/// <see cref="ITagChannel.EnsureConnectedAsync"/>，这样不会出现“读了一半才发现辅通道未连接、整轮作废”的
/// 情况。入口自身解析出的通道称为<b>主通道</b>，它是各事件委托中 <c>channel</c> 参数的语义；
/// 辅通道不进事件，需要时请从入口上自行解析。
/// </para>
/// <para>
/// <b>通道独占约束</b>：崩溃/取消后的清理路径会断开入口相关的<b>全部</b>通道，
/// 因此同一个通道实例<b>不得被多个入口共用</b>（包括父入口与嵌套子入口之间）。
/// 否则一个入口的崩溃或取消会提前断掉另一个入口正在使用的连接，导致对方反复失败。
/// 请为每个入口配置独立的通道实例，不要跨入口复用。
/// </para>
/// </summary>
public interface ITagGrpRunner
{

    /// <summary>
    /// 启动事件
    /// </summary>
    event RunnerStarted? RunnerStarted;

    /// <summary>
    /// 崩溃事件。<br/>
    /// 注意这里不要再有异常发生，否则会视作对应的入口轮询需要停机
    /// </summary>
    event RunnerCrashed? RunnerCrashed;

    /// <summary>
    /// 每一轮处理
    /// </summary>
    event TurnProcess? TurnProcess;

    /// <summary>
    /// 启动对群组的监控: loop(意图执行-> 读取输入 -> 逻辑处理 -> 刷写输出)<br/>
    /// 一旦成功启动轮询，除非主动取消，否则不会抛出异常。
    /// </summary>
    /// <param name="entry">入口；其子树中的通道由本运行器建立与断开连接</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task StartAsync(ITagGrp entry, CancellationToken ct);
}