using System.Collections.Generic;
using StdUnit.Tags.Compat;
using Xunit;

namespace StdUnit.Tags.Tests.Core.Compat;

/// <summary>
/// <see cref="EqualityComparersCompat"/> 的测试。<br/>
/// <br/>
/// 这个类型本身的代码量几乎为零，但它是「按引用比较」语义的**唯一入口**——
/// net8.0 转发到 BCL 的 <c>ReferenceEqualityComparer</c>，net472 转发到仓库内的 polyfill。
/// 两个分支必须给出完全相同的语义，否则 <c>ITagGrpExtensions.CollectChannels</c> 会在
/// 两个框架下去重的结果不同（表现为某个通道在 net472 下永远不被建连）。
/// </summary>
public class EqualityComparersCompatTests
{
    /// <summary>
    /// 故意重写 Equals/GetHashCode 的类型：任意两个实例都「值相等」。
    /// 用来区分「按引用」与「按值」两种去重语义。
    /// </summary>
    private sealed class ValueEqual
    {
        public override bool Equals(object? obj) => obj is ValueEqual;

        public override int GetHashCode() => 0;
    }

    [Fact]
    public void ByReference_ReturnsNonNull()
    {
        var comparer = EqualityComparersCompat.ByReference<string>();

        Assert.NotNull(comparer);
    }

    [Fact]
    public void ByReference_SameInstance_IsEqual()
    {
        var comparer = EqualityComparersCompat.ByReference<ValueEqual>();
        var item = new ValueEqual();

        Assert.True(comparer.Equals(item, item));
    }

    [Fact]
    public void ByReference_DifferentInstances_AreNotEqual_EvenWhenValueEqual()
    {
        // 核心断言：两个「值相等」但不是同一实例的对象必须被判为不同。
        // 若实现退化成默认比较器（或按值比较），这里会失败。
        var comparer = EqualityComparersCompat.ByReference<ValueEqual>();
        var a = new ValueEqual();
        var b = new ValueEqual();

        Assert.True(a.Equals(b), "前置条件：ValueEqual 按值相等");
        Assert.False(comparer.Equals(a, b));
    }

    [Fact]
    public void ByReference_NullHandling_MatchesReferenceSemantics()
    {
        // 用 null! 是有意的：net472 的引用程序集没有可空标注，IEqualityComparer<T>.Equals
        // 的参数在那边被当作非 null，直接传 null 字面量会报 CS8625（net8.0 不报）。
        // 语义上这里就是要验证 null 的处理，故显式抑制而非绕开。
        var comparer = EqualityComparersCompat.ByReference<ValueEqual>();
        var item = new ValueEqual();

        Assert.True(comparer.Equals(null!, null!));
        Assert.False(comparer.Equals(item, null!));
        Assert.False(comparer.Equals(null!, item));
    }

    [Fact]
    public void ByReference_InHashSet_DeduplicatesByInstance()
    {
        var a = new ValueEqual();
        var b = new ValueEqual();
        var c = a;

        var set = new HashSet<ValueEqual>(EqualityComparersCompat.ByReference<ValueEqual>())
        {
            a,
            b,
            c,
        };

        // a 与 c 是同一实例 → 去重；b 是不同实例 → 保留
        Assert.Equal(2, set.Count);
        Assert.Contains(a, set);
        Assert.Contains(b, set);
    }

    [Fact]
    public void ByReference_DefaultComparer_WouldDeduplicateByValue()
    {
        // 对照组：说明「为什么需要这个比较器」——默认比较器会把两个实例判成同一个。
        var a = new ValueEqual();
        var b = new ValueEqual();

        var byValue = new HashSet<ValueEqual> { a, b };
        var byReference = new HashSet<ValueEqual>(EqualityComparersCompat.ByReference<ValueEqual>()) { a, b };

        Assert.Single(byValue);
        Assert.Equal(2, byReference.Count);
    }
}
