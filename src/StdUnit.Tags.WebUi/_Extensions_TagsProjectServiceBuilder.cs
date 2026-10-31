namespace StdUnit.Tags.WebUi;

/// <summary>
/// <see cref="TagsProjectServiceBuilder"/> 上的观测流注册：实现就是转调 <c>builder.Services</c> 上
/// <see cref="TagsStreamExtensions_ServiceCollection"/> 里的同名扩展。<br/>
/// 两条路径写的是同一个服务集合，所以"不具名只能一路"、"key 不能重复/不能是保留字"等校验完全一致；
/// 把注册写进 <c>AddTagsProjectServices</c> 的回调里，等价于直接写 <c>services.AddTagsStream(...)</c>。
/// </summary>
public static class TagsStreamExtensions_TagsProjectServiceBuilder
{
    /// <summary>
    /// 注册不具名的观测流（等价于 <c>builder.Services.AddTagsStream(configure)</c>）。<br/>
    /// 和不具名的 <see cref="ITagsProjectCtrl"/> 关联；一个容器里最多只有一路不具名的观测流。
    /// </summary>
    /// <remarks>
    /// <b>启动时机</b>：hub 在 <c>MapTagsStream()</c> 映射端点时启动（早于任何请求，也早于 <c>StartPollAsync</c>），
    /// 不依赖 Generic Host 的 <c>IHostedService</c>。它在启动时按 <c>ITagsProjectCtrl.Project</c> 对账，
    /// 所以<b>晚启动也能补上</b>——即使项目已在轮询，结构树与值流都正常（轮次回调运行期也能挂，从下一轮开始生效）。
    /// </remarks>
    /// <param name="builder"></param>
    /// <param name="configure">这一路流的配置回调</param>
    /// <returns>同一个 builder，便于链式配置</returns>
    public static TagsProjectServiceBuilder AddTagsStream(this TagsProjectServiceBuilder builder, Action<TagsStreamOptions>? configure = null)
    {
        builder.Services.AddTagsStream(configure);
        return builder;
    }


    /// <summary>
    /// 注册一路具名的观测流（等价于 <c>builder.Services.AddKeyedTagsStream(projctrlKey, configure)</c>）。<br/>
    /// 和具名的 <see cref="ITagsProjectCtrl"/> 关联，每个名称下最多只有一路观测流。
    /// </summary>
    /// <remarks>
    /// 时序约束同 <see cref="AddTagsStream(TagsProjectServiceBuilder, Action{TagsStreamOptions})"/>：
    /// hub 在 <c>MapTagsStream()</c> 时启动，端口映射发生在任何请求之前，所以顺序只影响"多早开始观测"。
    /// </remarks>
    /// <param name="builder"></param>
    /// <param name="projctrlKey">
    /// 控制器的注册键，同时作为路由里的 <c>{keyed}</c> 段：允许字母/数字/<c>.</c>/<c>-</c>/<c>_</c>，
    /// 且不能是 <c>ui</c>/<c>designer</c>/<c>tree</c>/<c>stream</c>/<c>value</c>/<c>rescan</c>/<c>catalog</c>。
    /// </param>
    /// <param name="configure">这一路流的配置回调（每次调用各一份，互不影响）</param>
    /// <returns>同一个 builder，便于链式配置</returns>
    public static TagsProjectServiceBuilder AddKeyedTagsStream(this TagsProjectServiceBuilder builder, string projctrlKey, Action<TagsStreamOptions>? configure = null)
    {
        builder.Services.AddKeyedTagsStream(projctrlKey, configure);
        return builder;
    }

}
