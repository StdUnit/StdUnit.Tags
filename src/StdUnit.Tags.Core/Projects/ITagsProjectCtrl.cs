using System.Xml.Linq;

namespace StdUnit.Tags;

/// <summary>
/// 测点项目控制器接口。
/// 核心方法是<br/>
/// - <see cref="StartPollAsync(string?, XElement?, Func{ITagsProject, IServiceProvider, CancellationToken, Task})"/>：用于启动测点项目轮询。<br/>
/// - <see cref="StopAsync"/>：方法用于停止测点项目轮询。<br/>
/// </summary>
public interface ITagsProjectCtrl
{
    /// <summary>
    /// 测点项目，如果测点项目未启动，则为null
    /// </summary>
    ITagsProject? Project { get; }

    /// <summary>
    /// 当正在启动测点项目时发生异常时的回调。<br/>
    /// 返回值表示是否已经处理了异常，如果返回true，则不会再向外层抛出异常。
    /// </summary>
    Func<Exception, Task<bool>>? OnStartingException { get; set; }

    /// <summary>
    /// 测点项目启动或停止事件
    /// </summary>
    event TagsProjectStartedOrStopped? StartedOrStopped;

    /// <summary>
    /// 启动测点项目轮询。<br/>
    /// 此方法通常不会结束，除非测点项目被停止或在启动阶段发生异常。<br/>
    /// 如果在项目启动阶段就发生异常，则会触发<see cref="OnStartingException"/>回调。<br/>
    /// 示例：
    /// <example><![CDATA[
    /// ctrl.StartPollAsync(dir: "D:\\MyProject", root: null, hook: async (proj, sp, ct) =>{
    ///     proj.Logicets.Add(new MyLogicet(proj.Channels, proj.Tags)); 
    ///     // ...
    ///     // 其它事件绑定
    /// });
    /// ]]></example>
    /// <br/>
    /// 注意1：如果你调用了<see cref="StopAsync"/>
    /// 请务必等待该异步方法完成再调用<see cref="StartPollAsync(string?, XElement?, Func{ITagsProject,IServiceProvider, CancellationToken, Task})"/>，
    /// 否则，可能会导致新创建的项目被清理。<br/>
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
    /// 停止测点项目轮询，会导致测点项目停止并释放相关资源。
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
    /// c'tor
    /// </summary>
    /// <param name="isstarted"></param>
    /// <param name="project"></param>
    public TagsProjectEventArgs(bool isstarted, ITagsProject? project)
    {
        this.IsStarted = isstarted;
        Project = project;
    }

    /// <summary>
    /// 测点项目。
    /// 如果是停止事件，则为null。
    /// </summary>
    public ITagsProject? Project { get; }

    /// <summary>
    /// 测点项目是否启动
    /// </summary>
    public bool IsStarted { get; }
}

/// <summary>
/// 测点项目启动或停止事件委托
/// </summary>
/// <param name="sender"></param>
/// <param name="args"></param>

public delegate void TagsProjectStartedOrStopped(ITagsProjectCtrl sender, TagsProjectEventArgs args);