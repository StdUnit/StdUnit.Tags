using R3;

namespace StdUnit.Tags.R3;

/// <summary>
/// R3 extensions for ITagsProjectCtrl
/// </summary>
public static class TagsProjectCtrlExtensions
{
    /// <summary>
    /// 观测项目启动或停止事件
    /// </summary>
    /// <param name="ctrl"></param>
    /// <returns></returns>
    public static Observable<TagsProjectEventArgs> ObserveStartedOrStopped(this ITagsProjectCtrl ctrl)
    {
        var obs = Observable.FromEvent<TagsProjectStartedOrStopped, TagsProjectEventArgs>(
            h => (sender, e) => h(e),
            d => ctrl.StartedOrStopped += d,
            d => ctrl.StartedOrStopped -= d
        );
        return obs;
    }
}
