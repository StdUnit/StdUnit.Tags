using System.Reactive;
using System.Reactive.Linq;

namespace Itminus.Tags.Rx;

/// <summary>
/// Rx extensions for ITag
/// </summary>
public static class TagRxExtensions
{
    /// <summary>
    /// 观测从底层读取的事件
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    public static IObservable<EventPattern<ITag, TagSyncEventArgs>> ObserveOnRead(this ITag tag)
    {
        var read = Observable.FromEventPattern<TagSyncEventHandler, ITag, TagSyncEventArgs>(
            h => tag.OnTagRead += h,
            h => tag.OnTagRead -= h
            );
        return read;
    }

    /// <summary>
    /// 观测向底层写入的事件
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    public static IObservable<EventPattern<ITag, TagSyncEventArgs>> ObserveOnWritten(this ITag tag)
    {
        var written = Observable.FromEventPattern<TagSyncEventHandler, ITag, TagSyncEventArgs>(
            h => tag.OnTagWritten += h,
            h => tag.OnTagWritten -= h
            );
        return written;
    }

    /// <summary>
    /// 观测从底层读取或者向底层写入事件
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="startWithCurrent">true表示开始时推送当前值，false表示不推送</param>
    /// <returns></returns>
    public static IObservable<EventPattern<ITag, TagSyncEventArgs>> Watch(this ITag tag, bool startWithCurrent = true)
    {
        var read = tag.ObserveOnRead();
        var written = tag.ObserveOnWritten();
        var obs = read.Merge(written);
        if (startWithCurrent)
        {
            var ev = new EventPattern<ITag, TagSyncEventArgs>(
                tag,
                new TagSyncEventArgs(tag.Value, tag.Timestamp, TagSyncEventArgs.Kinds.None)
                );
            return obs.StartWith(ev);
        }
        else
        {
            return obs;
        }
    }
}
