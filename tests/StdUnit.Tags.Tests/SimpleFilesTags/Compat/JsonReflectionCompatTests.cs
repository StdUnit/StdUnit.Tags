using System;
using StdUnit.Tags.SimpleFiles.Compat;
using Xunit;

namespace StdUnit.Tags.Tests.SimpleFilesTags.Compat;

/// <summary>
/// <see cref="JsonReflectionCompat"/> 的测试。<br/>
/// <br/>
/// "宿主禁用了反射序列化"这一情形无法在 JIT 测试进程里造出来
/// （STJ 的 feature switch 是进程级且首次访问即固化），所以本类**直接**校验判定函数的两条分支；
/// 真实 AOT 宿主下的行为由 <c>tests/AotSmokeTest.SimpleFiles</c> 端到端验证。
/// </summary>
public class JsonReflectionCompatTests
{
    [Fact]
    public void EnsureReflectionSupported_WhenHostSupportsReflection_DoesNotThrow()
    {
        // 普通 JIT 宿主：没有元数据也只是退化为反射，不该报错
        JsonReflectionCompat.EnsureReflectionSupported(
            typeof(string), "Tag(json-v)", reflectionSupported: true);
    }

    [Fact]
    public void EnsureReflectionSupported_WhenHostCannotReflect_ThrowsNotSupported()
    {
        var ex = Assert.Throws<TagsProjectNotSupportedException>(() =>
            JsonReflectionCompat.EnsureReflectionSupported(
                typeof(Uri), "Tag(json-v)", reflectionSupported: false));

        // 加载期异常必须点名"哪个类型缺元数据"、"怎么补"，并带定位上下文
        Assert.Equal("Tag(json-v)", ex.Location);
        Assert.Contains(typeof(Uri).FullName!, ex.Message, StringComparison.Ordinal);
        Assert.Contains("AddJsonSerializerContext", ex.Message, StringComparison.Ordinal);
    }
}
