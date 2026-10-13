using System;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using StdUnit.Tags.SimpleFiles;
using Xunit;

// net472 没有 File.ReadAllTextAsync / WriteAllTextAsync（.NET Core 2.0+），沿用测试垫片。
using File = StdUnit.Tags.Tests.Compat.TestFileCompat;

namespace StdUnit.Tags.Tests.SimpleFilesTags;

/// <summary>
/// <see cref="TagsProject_Extensions.AddJsonSerializerContext"/> 的注册语义，
/// 以及"登记好的源生成元数据经 DI 一路流到构建器、最终被 <see cref="JsonDirectTag{T}"/> 用上"的端到端路径
/// （AOT / 裁剪宿主下唯一可用的 JSON 玩法）。<br/>
/// <br/>
/// "宿主不允许反射序列化"的失败语义见 <c>JsonReflectionCompatTests</c>。
/// </summary>
public class AddJsonSerializerContextTests : IDisposable
{
    private readonly string _tempDir;

    public AddJsonSerializerContextTests()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), $"AddJsonSerializerContextTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(this._tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(this._tempDir))
        {
            Directory.Delete(this._tempDir, recursive: true);
        }
    }

    /// <summary>一个基本类型测点 + 一个 JSON 测点。</summary>
    private XElement BuildXml() => XElement.Parse($@"
<root>
    <Channel name='sf' driver='SimpleFiles'>
        <BaseDir>{this._tempDir}</BaseDir>
    </Channel>
    <TagGrp name='g' isEntry='true' channel='sf' scanInterval='0'>
        <Tag name='int-v'  address='int.txt'   type='INT32' />
        <Tag name='json-v' address='point.json' type='JSON' />
    </TagGrp>
</root>");

    private static ServiceProvider BuildRoot(params JsonSerializerContext[] contexts)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddSimpleFilesChannel();

            // 登记元数据：这是 AOT / 裁剪宿主下 JSON 测点的唯一玩法
            foreach (var context in contexts)
            {
                b.AddJsonSerializerContext(context);
            }

            // JSON 测点走登记好的元数据；其余走内部工厂
            b.AddSimpleFilesDirectTagBuilder(
                predicate: bd => bd.TagDescriptor.TagKind == "JSON",
                configure: b => b.WithJsonTagFactory<CamelJsonPoint>())
             .AddSimpleFilesDirectTagBuilder();
        });
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddJsonSerializerContext_WhenNull_Throws()
    {
        TagsProjectServiceBuilder? builder = null;
        new ServiceCollection().AddTagsProjectServices(b => builder = b);

        Assert.NotNull(builder);
        Assert.Throws<ArgumentNullException>(() => builder.AddJsonSerializerContext(null!));
    }

    [Fact]
    public void AddJsonSerializerContext_ReturnsSameBuilder_AndPutsContextIntoDI()
    {
        TagsProjectServiceBuilder? builder = null;
        var services = new ServiceCollection();
        services.AddTagsProjectServices(b => builder = b);
        Assert.NotNull(builder);

        var context = CamelJsonPointContext.Default;

        // 链式书写是文档里推荐的样子，必须能返回同一个 builder
        Assert.Same(builder, builder.AddJsonSerializerContext(context));

        using var root = services.BuildServiceProvider();
        Assert.Same(context, Assert.Single(root.GetServices<JsonSerializerContext>()));
    }

    [Fact]
    public async Task JsonTag_WithRegisteredContext_SerializesWithSourceGeneratedMetadata()
    {
        var intPath = Path.Combine(this._tempDir, "int.txt");
        var jsonPath = Path.Combine(this._tempDir, "point.json");
        await File.WriteAllTextAsync(intPath, "1");
        // 预创建 JSON 文件：没开 autoCreateFile 时，文件不存在会跳过写入
        await File.WriteAllTextAsync(jsonPath, "{}");

        using var root = BuildRoot(CamelJsonPointContext.Default);
        using var scope = root.CreateScope();
        using var proj = scope.ServiceProvider.MakeProject(null, this.BuildXml());

        var grp = proj.Tags.SelectGrp("g");
        Assert.Equal(BuiltinTagKinds.INT32, grp.SelectTag("int-v").TagKind());

        var jsonTag = Assert.IsType<JsonDirectTag<CamelJsonPoint>>(grp.SelectTag("json-v"));

        // 写入：必须用**登记好的**元数据（camelCase）序列化。
        // 若退化成反射，属性名会是 X/Y——所以这条断言同时证明了"元数据确实被用上"。
        jsonTag.Value = new CamelJsonPoint(3, 4);
        await jsonTag.WriteAsync(CancellationToken.None);
        Assert.Equal("""{"x":3,"y":4}""", await File.ReadAllTextAsync(jsonPath));

        // 读取：对称地反序列化回来
        await jsonTag.ReadAsync(CancellationToken.None);
        Assert.Equal(new CamelJsonPoint(3, 4), jsonTag.Value);
    }

    [Fact]
    public async Task JsonTag_WithoutRegisteredContext_FallsBackToReflection()
    {
        var jsonPath = Path.Combine(this._tempDir, "point.json");
        await File.WriteAllTextAsync(jsonPath, """{"X":5,"Y":6}""");

        // 普通 JIT 宿主：一份元数据都不登记，行为与 2.0 之前完全一致（反射序列化）
        using var root = BuildRoot();
        using var scope = root.CreateScope();
        using var proj = scope.ServiceProvider.MakeProject(null, this.BuildXml());

        var jsonTag = Assert.IsType<JsonDirectTag<CamelJsonPoint>>(proj.Tags.SelectGrp("g").SelectTag("json-v"));

        await jsonTag.ReadAsync(CancellationToken.None);
        Assert.Equal(new CamelJsonPoint(5, 6), jsonTag.Value);
    }
}

/// <summary>JSON 测点的值类型（示例 POCO）。</summary>
internal sealed record CamelJsonPoint(int X, int Y);

/// <summary>
/// <see cref="CamelJsonPoint"/> 的源生成元数据。<br/>
/// 有意把属性名策略设成 camelCase：它与反射默认（原样属性名）不同，
/// 于是"文件里是不是 camelCase"就成了"有没有真用上源生成元数据"的判据。
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CamelJsonPoint))]
internal sealed partial class CamelJsonPointContext : JsonSerializerContext
{
}
