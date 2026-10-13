using System;
using System.IO;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using StdUnit.Tags.SimpleFiles;
using Xunit;
// net472 没有 File.ReadAllTextAsync / WriteAllTextAsync（.NET Core 2.0+），沿用测试垫片。
using File = StdUnit.Tags.Tests.Compat.TestFileCompat;

namespace StdUnit.Tags.Tests.SimpleFilesTags;

/// <summary>
/// <see cref="JsonDirectTag{T}"/> 的"源生成元数据"路径（AOT / 裁剪安全）。<br/>
/// 反射路径的既有用例见 <c>DirectTagTests.JsonTag_*</c>；这里只验证新路径与它的对称性。
/// </summary>
public class JsonTagTypeInfoTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SimpleFilesTagChannel _channel;
    private readonly TagGrp _grp;

    public JsonTagTypeInfoTests()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), $"JsonTagTypeInfoTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(this._tempDir);

        this._channel = new SimpleFilesTagChannel(
            new SimpleFilesTagChannelDescriptor { Name = "test-channel", BaseDir = this._tempDir },
            NullLogger<SimpleFilesTagChannel>.Instance);
        this._grp = new TagGrp(new TagGrpDescriptor { Name = "test-grp", IsEntry = true }, this._channel);
    }

    public void Dispose()
    {
        if (Directory.Exists(this._tempDir))
        {
            Directory.Delete(this._tempDir, recursive: true);
        }
    }

    private JsonDirectTag<T> MakeTag<T>(string fileName, JsonTypeInfo<T> typeInfo)
        => new(
            descriptor: new TagDescriptor
            {
                TagName = "json-v",
                RawAddress = fileName,
                TagKind = "JSON",
                NormalizedAddress = Path.Combine(this._tempDir, fileName),
            },
            thisChannel: this._channel,
            container: this._grp.IntoTagContainer(),
            typeInfo);

    [Fact]
    public async Task ReadAsync_WithSourceGeneratedTypeInfo_Deserializes()
    {
        var abspath = Path.Combine(this._tempDir, "point.json");
        var text = """{"X":1,"Y":2}""";
        await File.WriteAllTextAsync(abspath, text);

        var tag = this.MakeTag("point.json", AotJsonPointContext.Default.AotJsonPoint);
        await tag.ReadAsync(CancellationToken.None);

        // assert
        Assert.Equal(new AotJsonPoint(1, 2), tag.Value);
    }

    [Fact]
    public async Task WriteAsync_WithSourceGeneratedTypeInfo_Serializes()
    {
        await File.WriteAllTextAsync(Path.Combine(this._tempDir, "point2.json"), "{}");

        var tag = this.MakeTag("point2.json", AotJsonPointContext.Default.AotJsonPoint);
        tag.Value = new AotJsonPoint(3, 4);
        await tag.WriteAsync(CancellationToken.None);

        // assert
        var abspath = Path.Combine(this._tempDir, "point2.json");
        var realtxt = await File.ReadAllTextAsync(abspath);
        Assert.Equal("""{"X":3,"Y":4}""", realtxt);
    }

    [Fact]
    public void Ctor_WithMismatchedTypeInfo_Throws()
    {
        Assert.Throws<ArgumentException>(() => new JsonDirectTag<AotJsonPoint>(
            descriptor: new TagDescriptor { TagName = "json-v", RawAddress = "point3.json", TagKind = "JSON" },
            thisChannel: this._channel,
            container: this._grp.IntoTagContainer(),
            jsonTypeInfo: AotJsonPointContext.Default.Int32));
    }

    [Fact]
    public void WithJsonTagFactory_WhenRegisteredContextLacksType_ThrowsAtBuildTime()
    {
        // 登记了上下文、但里面没有目标类型的元数据
        var builder = new SimpleFilesDirectTagBuilder();
        builder
            .WithTagDescriptor(new TagDescriptor { TagName = "json-v", RawAddress = "point4.json", TagKind = "JSON" })
            .WithParent(this._grp)
            .WithChannel(this._channel);
        builder.UseJsonSerializerContexts(new[] { AotJsonPointContext.Default });

        // 上面的 JsonSerializerContext 中缺少 UnregisteredPoint 的元数据。
        builder.WithJsonTagFactory<UnregisteredPoint>();
        // 构建测点时立刻失败（而不是等到第一次读写）
        var ex = Assert.Throws<TagsProjectConfigurationException>(() => builder.Build(this._channel));

        Assert.Contains(nameof(UnregisteredPoint), ex.Message, StringComparison.Ordinal);
        Assert.Contains("Tag(json-v)", ex.Location, StringComparison.Ordinal);
    }
}

/// <summary>JSON 测点的值类型（示例 POCO）。</summary>
internal sealed record AotJsonPoint(int X, int Y);

/// <summary>另一个 POCO，用于验证"登记了上下文但缺类型"与"元数据类型不匹配"两种错法。</summary>
internal sealed record UnregisteredPoint(int Z);

/// <summary><see cref="AotJsonPoint"/> 的源生成元数据。</summary>
[JsonSerializable(typeof(AotJsonPoint))]
[JsonSerializable(typeof(int))]
internal sealed partial class AotJsonPointContext : JsonSerializerContext
{
}
