using System.Reactive.Linq;

namespace Itminus.Tags.Rx;

/// <summary>
/// Rx extensions for ITagsProjectCtrl
/// </summary>
public static class TagsProjectCtrlExtensions
{
    /// <summary>
    /// 观测项目启动或停止事件
    /// </summary>
    /// <param name="ctrl"></param>
    /// <returns></returns>
    public static IObservable<TagsProjectEventArgs> ObserveStartedOrStopped(this ITagsProjectCtrl ctrl)
    {
        var obs = Observable.FromEventPattern<TagsProjectStartedOrStopped, ITagsProjectCtrl, TagsProjectEventArgs>(
                d => ctrl.StartedOrStopped += d,
                d => ctrl.StartedOrStopped -= d
            )
            .Select(t => t.EventArgs);
        return obs;
    }
}
