using R3;

namespace StdUnit.Tags.R3;

/// <summary>
/// R3 extensions for ITag
/// </summary>
public static class TagR3Extensions
{
    /// <summary>
    /// 观测从底层读取的事件
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    public static Observable<TagSyncEventArgs> ObserveOnRead(this ITag tag)
    {
        var read = Observable.FromEvent<TagSyncEventHandler, TagSyncEventArgs>(
            h => (sender, e) => h(e),
            d => tag.OnTagRead += d,
            d => tag.OnTagRead -= d
            );
        return read;
    }

    /// <summary>
    /// 观测向底层写入的事件
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    public static Observable<TagSyncEventArgs> ObserveOnWritten(this ITag tag)
    {
        var written = Observable.FromEvent<TagSyncEventHandler, TagSyncEventArgs>(
            h => (sender, e) => h(e),
            d => tag.OnTagWritten += d,
            d => tag.OnTagWritten -= d
            );
        return written;
    }

    /// <summary>
    /// 观测从底层读取或者向底层写入事件
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="startWithCurrent">true表示开始时推送当前值，false表示不推送</param>
    /// <returns></returns>
    public static Observable<TagSyncEventArgs> Watch(this ITag tag, bool startWithCurrent = true)
    {
        var read = tag.ObserveOnRead();
        var written = tag.ObserveOnWritten();
        var obs = read.Merge(written);
        if (startWithCurrent)
        {
            var ev = new TagSyncEventArgs(tag.Value, tag.Timestamp, TagSyncEventArgs.Kinds.None);
            return obs.Prepend(ev);
        }
        else
        {
            return obs;
        }
    }
}
