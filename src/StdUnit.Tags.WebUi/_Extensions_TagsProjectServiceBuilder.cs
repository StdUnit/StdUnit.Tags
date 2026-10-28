namespace StdUnit.Tags.WebUi;

/// <summary>
/// extensions for AddTagsStream on TagsProjectServiceBuilder
/// </summary>
public static class TagsStreamExtensions_TagsProjectServiceBuilder
{
    /// <summary>
    /// 注册不具名的观测流。<br/>
    /// 和不具名的<see cref="ITagsProjectCtrl"/>相关联。最多只有一路不具名的观测流。
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public static TagsProjectServiceBuilder AddTagsStream(this TagsProjectServiceBuilder builder, Action<TagsStreamOptions>? configure = null)
    {
        builder.Services.AddTagsStream(configure);
        return builder;
    }


    /// <summary>
    /// 注册一路具名的观测流。<br/>
    /// 和具名的<see cref="ITagsProjectCtrl"/>相关联，每个名称下最多只有一路观测流。
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="projctrlKey"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public static TagsProjectServiceBuilder AddKeyedTagsStream(this TagsProjectServiceBuilder builder, string projctrlKey, Action<TagsStreamOptions>? configure = null)
    {
        builder.Services.AddKeyedTagsStream(projctrlKey, configure);
        return builder;
    }

}
