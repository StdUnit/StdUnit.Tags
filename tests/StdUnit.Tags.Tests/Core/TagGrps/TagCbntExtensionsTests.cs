using Xunit;

namespace StdUnit.Tags.Tests.Core.TagGrps;

/// <summary>
/// 测试 ITagCbnt 扩展方法
/// </summary>
public class TagCbntExtensionsTests
{
    [Fact]
    public void TagName_ForITagCbnt_ReturnsDescriptorName()
    {
        var cbnt = new TestByteTagCbnt(new TagCbntDescriptor { Name = "myCbnt" });

        var result = cbnt.TagName();

        Assert.Equal("myCbnt", result);
    }
}
