using System.Collections.Generic;

namespace Itminus.Tags.Compat;

/// <summary>
/// 按需获取「按引用比较」的比较器，屏蔽两个目标框架的差异。<br/>
/// <br/>
/// net8.0 用标准库的 <c>System.Collections.Generic.ReferenceEqualityComparer</c>（有标准库就用标准）；<br/>
/// net472 用仓库内的同语义 polyfill（<c>ReferenceEqualityComparerCompat</c>；它仅在 net472 编译，故此处不用 cref）。<br/>
/// <br/>
/// 调用点因此不必写 <c>#if</c>，差异集中在<b>这一处</b>。
/// </summary>
internal static class EqualityComparersCompat
{
    /// <summary>
    /// 取得针对 <typeparamref name="T"/> 的按引用比较器。<br/>
    /// 利用 <see cref="IEqualityComparer{T}"/> 的逆变，由 <c>IEqualityComparer&lt;object&gt;</c> 转换而来。
    /// </summary>
    internal static IEqualityComparer<T> ByReference<T>() where T : class
    {
#if NETFRAMEWORK
        return ReferenceEqualityComparerCompat.Instance;
#else
        return System.Collections.Generic.ReferenceEqualityComparer.Instance;
#endif
    }
}
