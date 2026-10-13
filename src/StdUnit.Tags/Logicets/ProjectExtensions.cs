using System.Diagnostics.CodeAnalysis;

namespace StdUnit.Tags.Logicets;

/// <summary>
/// extensions for <see cref="ITagsProject"/>
/// </summary>
public static class TagProjectExtensions
{
    /// <summary>
    /// 注册逻辑
    /// </summary>
    /// <typeparam name="TLogicet">
    /// 逻辑组件实现类型。<br/>
    /// </typeparam>
    /// <param name="project"></param>
    /// <param name="sp"></param>
    /// <returns></returns>
    public static bool TryAddLogicet<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
        TLogicet>(this ITagsProject project, IServiceProvider sp)
        where TLogicet : class, ILogicet
    {
        var (logicet, ex) = LogicetProviderUtils.CreateLogicet(sp, typeof(TLogicet), project.Channels, project.Tags);
        if (logicet is null)
        {
            return false;
        }

        project.Logicets.Add(logicet);
        return true;
    }
}