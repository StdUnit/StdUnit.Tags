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
    /// 运行顺序（较小的先执行）。<b>启动期</b>读取一次（<see cref="ITagsProject.RunAsync"/> 为入口匹配逻辑组件时），
    /// 必须无副作用、不阻塞、<b>不得抛异常</b>：抛出会被报成 <see cref="TagsProjectConfigurationException"/>（配置错误），
    /// 而不是按轮询故障重试——重试对确定性的配置错误没有意义。
    /// </summary>
    int Order { get; }

    /// <summary>
    /// 启用？
    /// </summary>
    bool Enabled { get; }

    /// <summary>
    /// 是否能匹配入口？返回true表示应该处理当前entry，否则应该跳过处理。<br/>
    /// <br/>
    /// 调用时机：<see cref="ITagsProject.RunAsync"/> 启动时，对每个"入口 × 逻辑组件"组合调用一次（重新启动会重新求值）。<br/>
    /// <b>契约</b>：必须是纯函数——只读已加载的配置、幂等、不阻塞、不做 IO/等待，<b>不得抛异常</b>。<br/>
    /// - 判定不出来就返回 <c>false</c>，不要把"要不要匹配"变成一次可能失败的操作；<br/>
    /// - 真正的配置错误请在构造函数 / 加载期报告（<see cref="TagsProjectLoadException"/>）；<br/>
    /// - 一旦抛异常，框架会把它报成 <see cref="TagsProjectConfigurationException"/> 并<b>拒绝启动项目</b>——
    ///   这是确定性的配置错误，既不会重试，也不会走 <see cref="ITagsProject.RunnerCrashed"/> 的断开重连恢复路径；
    ///   而且此时<b>没有任何入口</b>开始轮询（框架先算完所有入口的匹配，再启动）。
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
    /// 它<b>不是</b>"单轮超时"令牌——框架不会为某一轮单独取消，同一入口每一轮拿到的都是同一个<b>实例</b>
    /// （同一次运行内所有回调共享它；但它不必与传给 <see cref="ITagsProject.RunAsync"/> 的令牌是同一个实例，
    /// 也不要拿它们做 <c>==</c> 比较，"取消时机"才是契约）。<br/>
    /// 建议用 <c>if (ct.IsCancellationRequested) return;</c> 提前结束本轮；<b>不要</b>在这里抛
    /// <see cref="OperationCanceledException"/>，令牌没取消时抛它会被运行器当成一次失败（触发 <see cref="ITagsProject.RunnerCrashed"/> 并按策略断开重连）。<br/>
    /// 不要缓存它、也不要每轮 <c>Register</c>；它也不能让本方法变成"可以慢慢跑"——本方法仍必须尽快返回。
    /// </param>
    /// <returns></returns>
    Task ProcessAsync(ITagGrp entry, ITagChannel? thisChannel, CancellationToken ct = default);
}
