namespace StdUnit.Tags;


/// <summary>
/// 逻辑小组件，用于对测点添加逻辑
/// </summary>
public interface ILogicet
{
    /// <summary>
    /// 所属的测点项目。<br/>
    /// 逻辑组件创建时注入：构造期间 <see cref="ITagsProject.Channels"/> / <see cref="ITagsProject.Tags"/> 已加载完成，可直接使用。
    /// </summary>
    ITagsProject Project { get; }

    /// <summary>
    /// 运行顺序
    /// </summary>
    int Order { get; }

    /// <summary>
    /// 启用？
    /// </summary>
    bool Enabled { get; }

    /// <summary>
    /// 是否能匹配入口？返回true表示应该处理当前entry，否则应该跳过处理
    /// </summary>
    /// <param name="entry"></param>
    /// <returns></returns>
    bool MatchEntry(ITagGrp entry);

    /// <summary>
    /// 处理
    /// </summary>
    /// <param name="entry">当前入口测点组</param>
    /// <param name="thisChannel">入口的<b>主通道</b>（<see cref="ITagGrpExtensions.SearchChannel"/> 的解析结果，可能为 null）</param>
    /// <param name="ct">
    /// 项目的<b>生命周期</b>令牌：项目停止（<see cref="ITagsProjectCtrl.StopAsync"/>）时取消。<br/>
    /// 它<b>不是</b>"单轮超时"令牌——框架不会为某一轮单独取消，同一入口每一轮拿到的都是同一个令牌。<br/>
    /// 建议用 <c>if (ct.IsCancellationRequested) return;</c> 提前结束本轮；<b>不要</b>在这里抛
    /// <see cref="OperationCanceledException"/>，它会被运行器当成一次失败（触发 <see cref="ITagsProject.RunnerCrashed"/> 并按策略断开重连）。<br/>
    /// 不要缓存它、也不要每轮 <c>Register</c>；它也不能让本方法变成"可以慢慢跑"——本方法仍必须尽快返回。
    /// </param>
    /// <returns></returns>
    Task ProcessAsync(ITagGrp entry, ITagChannel? thisChannel, CancellationToken ct = default);
}
