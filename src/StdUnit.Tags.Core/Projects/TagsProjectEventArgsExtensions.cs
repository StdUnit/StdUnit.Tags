using System;

namespace StdUnit.Tags;

/// <summary>
/// <see cref="TagsProjectEventArgs"/> 的扩展方法。
/// </summary>
public static class TagsProjectEventArgsExtensions
{
    /// <summary>
    /// 本次通知是否为"已启动"。等价于 <c>args.State == TagsProjectState.Running</c>。<br/>
    /// 做成扩展方法而不是属性：事件参数里只保留 <see cref="TagsProjectEventArgs.State"/> 这一个"真相"，
    /// 不再有"派生属性可能与 State 不一致"的空间（自定义实现、手写测试替身都不会跑偏）。<br/>
    /// 需要同时拿到项目时用 <see cref="IsStarted(TagsProjectEventArgs, out ITagsProject)"/>，
    /// 可以省掉调用点的 <c>Project is { }</c> 判断。
    /// </summary>
    /// <param name="args">事件参数</param>
    /// <returns>是"已启动"通知时为 <c>true</c></returns>
    /// <exception cref="ArgumentNullException"><paramref name="args"/> 为 null</exception>
    public static bool IsStarted(this TagsProjectEventArgs args) => args.IsStarted(out _);

    /// <summary>
    /// 本次通知是否为"已启动"，是则一并交出项目实例。<br/>
    /// 判定比无参版本更严格：<c>State == Running</c> <b>且</b> <c>Project</c> 非 null 才返回 <c>true</c>。
    /// 因此返回 <c>true</c> 时 <paramref name="project"/> 必定可用，返回 <c>false</c> 时它是 <c>null</c>
    /// ——刻意不提供"半截状态"，免得调用点再判一次空。<br/>
    /// <br/>
    /// 为什么用 <c>out</c> 而不是返回可空值：Core 同时编译 <c>net472</c>，那里没有 <c>NotNullWhen</c> 可空标注，
    /// 返回可空值会让调用点在 <c>if</c> 里被迫写 <c>!</c>；而 <c>out</c> 形参在这里天然是"成功才有值"。
    /// </summary>
    /// <param name="args">事件参数</param>
    /// <param name="project">处于运行中的项目；未启动时被置为 <c>null</c></param>
    /// <returns>是"已启动"通知且项目可用时为 <c>true</c></returns>
    /// <exception cref="ArgumentNullException"><paramref name="args"/> 为 null</exception>
    public static bool IsStarted(this TagsProjectEventArgs args, out ITagsProject project)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        if (args.State == TagsProjectState.Running && args.Project is { } running)
        {
            project = running;
            return true;
        }

        // 返回 false 时该 out 形参不可用；声明成非可空是为了让调用点在 if 里直接使用，
        // 这里的 null! 只是满足编译器，不做任何运行时保证的让步。
        project = null!;
        return false;
    }
}
