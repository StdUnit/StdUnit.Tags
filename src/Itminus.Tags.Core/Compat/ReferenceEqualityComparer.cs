#if NETFRAMEWORK
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Itminus.Tags.Compat;

/// <summary>
/// <c>System.Collections.Generic.ReferenceEqualityComparer</c>（.NET Core 5+）的 net472 polyfill。<br/>
/// <br/>
/// net472 的标准库没有这个类型，但本库需要「按引用（而非按值）去重」的语义——
/// 见 <see cref="ITagGrpExtensions.CollectChannels"/>：通道代表一条物理连接，
/// 若某个驱动重写了 <c>Equals</c>/<c>GetHashCode</c>，按值去重会把两个不同实例误判为同一条连接，
/// 导致该通道永远不被建连。<br/>
/// <br/>
/// 仅 net472 编译：net8.0 直接使用标准库实现（有标准库就用标准，不遮蔽同名 BCL 类型）。
/// 行为与标准库一致：<c>Equals</c> 走 <see cref="object.ReferenceEquals"/>，
/// 哈希走 <see cref="RuntimeHelpers.GetHashCode"/>（恒为标识哈希，不受重写的 <c>GetHashCode</c> 影响）。
/// </summary>
internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>
{
    private ReferenceEqualityComparer()
    {
    }

    /// <summary>单例，形态与标准库的 <c>Instance</c> 保持一致。</summary>
    public static ReferenceEqualityComparer Instance { get; } = new ReferenceEqualityComparer();

    bool IEqualityComparer<object>.Equals(object? x, object? y) => ReferenceEquals(x, y);

    int IEqualityComparer<object>.GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
}
#endif
