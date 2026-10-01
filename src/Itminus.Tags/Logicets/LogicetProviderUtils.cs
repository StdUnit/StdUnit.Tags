using Microsoft.Extensions.DependencyInjection;

namespace Itminus.Tags.Logicets;

internal static class LogicetProviderUtils
{

    /// <summary>
    /// 创建Logicet实例。
    /// 传入的 type 参数必须是一个实现了 ILogicet 接口的非抽象类。
    /// </summary>
    internal static (ILogicet?, Exception?) CreateLogicet(IServiceProvider sp, Type type, IReadOnlyList<ITagChannel> channels, ITagGrp tags)
    {
        if (!typeof(ILogicet).IsAssignableFrom(type))
        {
            throw new InvalidOperationException($"传入的类型必须是 {nameof(ILogicet)}, 实际是{type.Name}");
        }

        // 首先尝试使用 ILogicetCreator 创建实例
        var creator = sp.GetService<ILogicetCreator>();
        if (creator is not null)
        {
            var item = creator.CreateLogicet(sp, type, channels, tags);
            if (item is not null)
                return (item, null);
        }

        // fallback
        try
        {
            var logicet = ActivatorUtilities.CreateInstance(sp, type, channels, tags);
            return (logicet as ILogicet, null);
        }
        // 吞掉任何异常，返回 null 以表示创建失败
        catch (Exception ex)
        {
            return (null, ex);
        }
    }
}