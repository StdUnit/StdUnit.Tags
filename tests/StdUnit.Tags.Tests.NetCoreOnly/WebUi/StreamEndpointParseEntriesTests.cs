using System;
using System.Linq;
using StdUnit.Tags.WebUi.Internal;
using Xunit;

namespace StdUnit.Tags.Tests.NetCoreOnly.WebUi;

/// <summary>
/// <c>?entries=</c> 的解析：逗号分隔、去空白、去重、空段忽略；解析不出来就是"全部入口"。
/// </summary>
public class StreamEndpointParseEntriesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(",")]
    [InlineData(" , , ")]
    public void ParseEntries_NothingUsefulMeansAllEntries(string? query)
    {
        Assert.Null(StreamEndpoint.ParseEntries(query));
    }

    [Fact]
    public void ParseEntries_SplitsTrimsAndDeduplicates()
    {
        var entries = StreamEndpoint.ParseEntries(" g1 , g2 ,g1,, g3 ");

        Assert.NotNull(entries);
        Assert.Equal(new[] { "g1", "g2", "g3" }, entries!.OrderBy(e => e, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void ParseEntries_IsCaseSensitive()
    {
        // 入口名与 TagName 一致，按序数比较（不做大小写折叠）。
        var entries = StreamEndpoint.ParseEntries("g1,G1");

        Assert.NotNull(entries);
        Assert.Equal(2, entries!.Count);
    }
}
