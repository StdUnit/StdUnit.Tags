using StdUnit.Tags.Logicets;
#if !NETFRAMEWORK
using System.Runtime.CompilerServices;
#endif

namespace StdUnit.Tags.Compat;

/// <summary>
/// 提供是否支持动态加载程序集的兼容性判断。使用场景主要是 <see cref="LogicetLoader"/>。
/// </summary>
internal static class DynamicLoadingCompat
{
    /// <summary>
    /// 当前是否支持动态加载程序集。
    /// </summary>
    internal static bool IsSupported
    {
        get
        {
#if NETFRAMEWORK
            return true;
#else
            return RuntimeFeature.IsDynamicCodeSupported;
#endif
        }
    }
}
