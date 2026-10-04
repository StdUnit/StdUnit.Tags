using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.Core;

/// <summary>
/// <see cref="XElementExensions.GetTagUnionTagKind"/> 的测试。<br/>
/// 背景：net472 支持分支把该方法的 <c>string.IsNullOrEmpty</c> 换成了语言级 null 检查
/// （net472 的引用程序集无可空标注，编译器看不到 <c>[NotNullWhen(false)]</c>，会报 CS8603）。<br/>
/// 两者语义必须完全一致：只有“属性缺失（null）”与“空字符串”才回退到 <see cref="BuiltinTagKinds.Unknown"/>。
/// </summary>
public class XElementExensionsTests
{
    [Theory]
    [InlineData("BIT", "BIT")]
    [InlineData("int16", "int16")] // 原样返回，大小写/合法性由上层处理
    [InlineData(" ", " ")]         // 仅空白不算空
    public void GetTagUnionTagKind_ReturnsConfiguredValue(string configured, string expected)
    {
        var e = new XElement("Tag", new XAttribute("type", configured));

        var kind = e.GetTagUnionTagKind("t");

        Assert.Equal(expected, kind);
    }

    [Fact]
    public void GetTagUnionTagKind_ReturnsUnknown_WhenTypeAttributeMissing()
    {
        var e = new XElement("Tag");

        var kind = e.GetTagUnionTagKind("t");

        Assert.Equal(BuiltinTagKinds.Unknown, kind);
    }

    [Fact]
    public void GetTagUnionTagKind_ReturnsUnknown_WhenTypeAttributeIsEmpty()
    {
        var e = new XElement("Tag", new XAttribute("type", ""));

        var kind = e.GetTagUnionTagKind("t");

        Assert.Equal(BuiltinTagKinds.Unknown, kind);
    }
}
