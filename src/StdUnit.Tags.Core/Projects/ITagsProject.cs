
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
    /// 轮询开始，会透传给<see cref="ITagGrpRunner.RunnerStarted"/>。<br/>
    /// </summary>
    event RunnerStarted? RunnerStarted;

    /// <summary>
    /// 轮询崩溃，会透传给<see cref="ITagGrpRunner.RunnerCrashed"/>。<br/>
    /// 通常来说，在该委托正常结束后，项目会自行重启轮询循环。<br/>
    /// 但需要注意的是，这个自行重启是指<b>事件委托没有再向外抛出异常</b>的情况：<br/>
    /// 在<see cref="RunnerCrashed"/>中外抛异常会被视作对应入口需要<b>停机</b>！
    /// </summary>
    event RunnerCrashed? RunnerCrashed;


    /// <summary>
    /// 初始化，如果root为空，则默认取 projRoot下的index.xml文件
    /// </summary>
    /// <param name="projRoot"></param>
    /// <param name="root"></param>
    void Initialize(string projRoot, XElement? root = null);

    /// <summary>
    /// 运行本项目下所有的入口测点组。<br/>
    /// 这个方法在所有入口组都运行结束之前，不会返回！
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
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