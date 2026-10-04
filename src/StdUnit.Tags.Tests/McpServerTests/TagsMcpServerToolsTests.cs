using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using StdUnit.Tags.McpServer;
using StdUnit.Tags.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Moq;
using Xunit;

namespace StdUnit.Tags.Tests.McpServerTests;

public class TagsMcpServerToolsTests : IAsyncDisposable
{
    /// <summary>
    /// Unix 纪元。net472 没有 <c>DateTime.UnixEpoch</c>（.NET Core 2.1+ 才加入），故在此自定义，
    /// 保证两个目标框架下断言同一个值。
    /// </summary>
    private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ServiceProvider _root;
    private readonly IServiceScope _scope;
    private readonly IServiceProvider _sp;

    public TagsMcpServerToolsTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddFakedSupport();
            b.AddFakedTagSupport();
        });
        _root = services.BuildServiceProvider();
        _scope = _root.CreateScope();
        _sp = _scope.ServiceProvider;
    }

    public ValueTask DisposeAsync()
    {
        _scope.Dispose();
        _root.Dispose();
        return default;
    }

    private ITagsProjectFactory Factory => _sp.GetRequiredService<ITagsProjectFactory>();
    private static ILogger<TagsMcpServerTools> Logger => NullLogger<TagsMcpServerTools>.Instance;

    private static XElement TestXml => new("Project",
        new XElement("Channel", new XAttribute("name", "fake"), new XAttribute("driver", "fake")),
        // 通道不能跨入口共用（一个通道 = 一个轮询回路），所以 g2 用它自己的 fake2
        new XElement("Channel", new XAttribute("name", "fake2"), new XAttribute("driver", "fake")),
        new XElement("TagGrp",
            new XAttribute("name", "g1"), new XAttribute("isEntry", "true"),
            new XAttribute("isEnabled", "true"), new XAttribute("channel", "fake"),
            new XElement("TagGrp", new XAttribute("name", "sub"),
                new XElement("Tag", new XAttribute("name", "bit_tag"), new XAttribute("type", "BIT"), new XAttribute("access", "RW"), new XAttribute("address", "0")),
                new XElement("Tag", new XAttribute("name", "int16_tag"), new XAttribute("type", "INT16"), new XAttribute("access", "RW"), new XAttribute("address", "2")),
                new XElement("Tag", new XAttribute("name", "ro_tag"), new XAttribute("type", "BIT"), new XAttribute("access", "RO"), new XAttribute("address", "3"))
            )
        ),
        new XElement("TagGrp",
            new XAttribute("name", "g2"), new XAttribute("isEntry", "true"),
            new XAttribute("isEnabled", "true"), new XAttribute("channel", "fake2"),
            new XElement("Tag", new XAttribute("name", "float_tag"), new XAttribute("type", "FLOAT"), new XAttribute("access", "RW"), new XAttribute("address", "0")),
            new XElement("Tag", new XAttribute("name", "int32_tag"), new XAttribute("type", "INT32"), new XAttribute("access", "RW"), new XAttribute("address", "4"))
        )
    );

    private TagsMcpServerTools MakeTools(ITagsProject proj, TagsProjectStartedOrStopped? handler = null)
    {
        var ctrl = new ProjectCtrl(proj);
        if (handler is not null)
        {
            ctrl.StartedOrStopped += handler;
        }
        var tools = new TagsMcpServerTools(ctrl, Logger);
        return tools;
    }


    private static void AssertSuccess(WriteResult result)
    {
        Assert.True(result.Success, $"Expected success, got: {result.Message}");
        Assert.NotEmpty(result.Message);
    }

    private static void AssertFail(WriteResult result, string? expectedFragment = null)
    {
        Assert.False(result.Success, $"Expected failure, got success: {result.Message}");
        Assert.NotEmpty(result.Message);
        if (expectedFragment is not null)
            Assert.Contains(expectedFragment, result.Message);
    }


    [Fact]
    public void EnsureProject_WhenNoProject_Throws()
    {
        var tools = new TagsMcpServerTools(new ProjectCtrl(null), Logger);
        Assert.Throws<InvalidOperationException>(() => tools.ReadTagValue("g1/sub/bit_tag"));
    }

    [Fact]
    public async Task GetToolGuideAsync_ReturnsEmbeddedMarkdown()
    {
        var result = await TagsMcpResources.GetToolGuideAsync();

        Assert.Equal("docs://stdunit.tags/tool_guide", result.Uri);
        Assert.Equal("text/markdown", result.MimeType);
        Assert.Contains("StdUnit.Tags是一套面向工业场景的测点通信库", result.Text);
    }


    #region 测试 读取
    [Fact]
    public void ReadTag_ExistingTag_ReturnsValue()
    {
        using var proj = Factory.Create(null!, TestXml);
        var tools = MakeTools(proj);
        var result = tools.ReadTagValue("g1/sub/bit_tag");
        Assert.NotNull(result);
        Assert.NotNull(result.Descriptor);
        Assert.Equal("bit_tag", result.Descriptor.TagName);
        Assert.Equal(BuiltinTagKinds.BIT, result.Descriptor.TagKind);
        Assert.Equal(TagAccessMode.RW, result.Descriptor.AccessMode);
    }

    [Fact]
    public void ReadTag_NonExistentPath_Throws()
    {
        using var proj = Factory.Create(null!, TestXml);
        var tools = MakeTools(proj);
        Assert.Throws<InvalidOperationException>(() => tools.ReadTagValue("g1/nonexistent/tag"));
    }


    [Fact]
    public void ReadTags_MultipleTags_ReturnsAll()
    {
        using var proj = Factory.Create(null!, TestXml);
        var tools = MakeTools(proj);
        var results = tools.ReadTagValues(new[] { "g1/sub/bit_tag", "g1/sub/int16_tag", "g2/int32_tag" });
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.NotNull(r.Descriptor));
        Assert.All(results, r => Assert.Null(r.Error));
    }

    [Fact]
    public void ReadTags_SomeNonExistent_ReturnsErrorsForThose()
    {
        using var proj = Factory.Create(null!, TestXml);
        var tools = MakeTools(proj);
        var results = tools.ReadTagValues(new[] { "g1/sub/bit_tag", "g1/no/such/tag" });
        Assert.Equal(2, results.Count);
        Assert.Null(results[0].Error);
        Assert.NotNull(results[1].Error);
        Assert.Null(results[1].Descriptor);
    }
    #endregion

    #region 测试 DescribeProject

    [Fact]
    public void DescribeProject_WhenProjectExists_ReturnsRootXml()
    {
        using var proj = Factory.Create(null!, TestXml);
        var tools = MakeTools(proj);

        var result = tools.DescribeProject();

        Assert.StartsWith("<Project>", result);
        Assert.Contains("name=\"g1\"", result);
        Assert.Contains("name=\"g2\"", result);
        Assert.Contains("isEntry=\"true\"", result);
    }

    [Fact]
    public void DescribeProject_ReturnsSameXmlAsInputRoot()
    {
        using var proj = Factory.Create(null!, TestXml);
        var tools = MakeTools(proj);
        var expected = TestXml.ToString();

        var result = tools.DescribeProject();

        Assert.Equal(expected, result);
    }

    [Fact]
    public void DescribeProject_WhenNoProject_ReturnsHintMessage()
    {
        var tools = new TagsMcpServerTools(new ProjectCtrl(null), Logger);

        var result = tools.DescribeProject();

        Assert.Contains("there's no project yet", result);
    }

    [Fact]
    public void DescribeProject_ContainsChannelInfo()
    {
        using var proj = Factory.Create(null!, TestXml);
        var tools = MakeTools(proj);

        var result = tools.DescribeProject();

        Assert.Contains("Channel", result);
        Assert.Contains("fake", result);
    }

    [Fact]
    public void DescribeProject_ContainsTagNames()
    {
        using var proj = Factory.Create(null!, TestXml);
        var tools = MakeTools(proj);

        var result = tools.DescribeProject();

        Assert.Contains("bit_tag", result);
        Assert.Contains("int16_tag", result);
        Assert.Contains("float_tag", result);
    }

    #endregion


    #region 测试 写入
    [Fact]
    public async Task WriteTag_RwTag_SetsValue()
    {
        using var proj = Factory.Create(null!, TestXml);
        using var cts = new CancellationTokenSource();
        _ = Task.Run(() => proj.RunAsync(cts.Token));
        var tools = MakeTools(proj);

        var result = await tools.WriteTagValue("g1/sub/bit_tag", "true", waitForCompletion: true);
        AssertSuccess(result);

        cts.Cancel();
    }

    [Fact]
    public async Task WriteTag_ReadOnly_ReturnsError()
    {
        using var proj = Factory.Create(null!, TestXml);
        using var cts = new CancellationTokenSource();
        _ = Task.Run(() => proj.RunAsync(cts.Token));
        var tools = MakeTools(proj);

        var result = await tools.WriteTagValue("g1/sub/ro_tag", "true");
        AssertFail(result, "read-only");

        cts.Cancel();
    }

    [Fact]
    public async Task WriteTag_NonExistent_ReturnsError()
    {
        using var proj = Factory.Create(null!, TestXml);
        using var cts = new CancellationTokenSource();
        _ = Task.Run(() => proj.RunAsync(cts.Token));
        var tools = MakeTools(proj);

        var result = await tools.WriteTagValue("g1/no/such/tag", "42");
        AssertFail(result, "Failed to find tag");

        cts.Cancel();
    }


    [Fact]
    public async Task WriteTags_BatchAcrossEntries_GroupsCorrectly()
    {
        using var proj = Factory.Create(null!, TestXml);
        using var cts = new CancellationTokenSource();
        _ = Task.Run(() => proj.RunAsync(cts.Token));
        var tools = MakeTools(proj);

        var result = await tools.WriteTagValues(new Dictionary<string, string>
        {
            ["g1/sub/bit_tag"] = "true",
            ["g1/sub/int16_tag"] = "123",
            ["g2/float_tag"] = "3.14",
            ["g2/int32_tag"] = "42",
        }, waitForCompletion: true);

        AssertSuccess(result);
        Assert.Contains("4 tag(s) across 2 entry group(s)", result.Message);

        cts.Cancel();
    }

    [Fact]
    public async Task WriteTags_IncludesReadOnly_Aborts()
    {
        using var proj = Factory.Create(null!, TestXml);
        using var cts = new CancellationTokenSource();
        _ = Task.Run(() => proj.RunAsync(cts.Token));
        var tools = MakeTools(proj);

        var result = await tools.WriteTagValues(new Dictionary<string, string>
        {
            ["g1/sub/bit_tag"] = "true",
            ["g1/sub/ro_tag"] = "true",
        });
        AssertFail(result, "read-only");

        cts.Cancel();
    }

    [Fact]
    public async Task WriteTags_NonExistent_ReturnsError()
    {
        using var proj = Factory.Create(null!, TestXml);
        using var cts = new CancellationTokenSource();
        _ = Task.Run(() => proj.RunAsync(cts.Token));
        var tools = MakeTools(proj);

        var result = await tools.WriteTagValues(new Dictionary<string, string>
        {
            ["g1/sub/bit_tag"] = "true",
            ["g1/no/such"] = "0",
        });
        AssertFail(result, "Failed to find tag");

        cts.Cancel();
    }


    [Fact]
    public async Task WriteTag_ConvertsInt16Value()
    {
        using var proj = Factory.Create(null!, TestXml);
        using var cts = new CancellationTokenSource();
        _ = Task.Run(() => proj.RunAsync(cts.Token));
        var tools = MakeTools(proj);

        var result = await tools.WriteTagValue("g1/sub/int16_tag", "42");
        AssertSuccess(result);

        cts.Cancel();
    }

    [Fact]
    public async Task WriteTag_ConvertsFloatValue()
    {
        using var proj = Factory.Create(null!, TestXml);
        using var cts = new CancellationTokenSource();
        _ = Task.Run(() => proj.RunAsync(cts.Token));
        var tools = MakeTools(proj);

        var result = await tools.WriteTagValue("g2/float_tag", "3.14");
        AssertSuccess(result);

        cts.Cancel();
    }
    #endregion


    #region 结果测试
    [Fact]
    public void WriteResult_Ok_IsSuccess()
    {
        var wr = WriteResult.Ok("done");
        Assert.True(wr.Success);
        Assert.Equal("done", wr.Message);
    }

    [Fact]
    public void WriteResult_Fail_IsNotSuccess()
    {
        var wr = WriteResult.Fail("bad");
        Assert.False(wr.Success);
        Assert.Equal("bad", wr.Message);
    }


    [Fact]
    public void TagValue_WithError_HasNullDescriptor()
    {
        var tv = new TagValue(null, null, DateTime.MinValue, "boom");
        Assert.Null(tv.Descriptor);
        Assert.Null(tv.Value);
        Assert.Equal("boom", tv.Error);
    }

    [Fact]
    public void TagValue_Success_HasDescriptorAndValue()
    {
        var desc = new TagDescriptor { TagName = "t", TagKind = BuiltinTagKinds.BIT, AccessMode = TagAccessMode.RW };
        var now = DateTime.UtcNow;
        var tv = new TagValue(desc, true, now);
        Assert.Same(desc, tv.Descriptor);
        Assert.Equal(true, tv.Value);
        Assert.Equal(now, tv.Timestamp);
        Assert.Null(tv.Error);
    }
    #endregion


    #region 测试 AddTagsMcp（ServiceExtensions）

    [Fact]
    public void AddTagsMcp_RegistersAllToolsAndGuideResource()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddMcpServer();

        var returned = builder.AddTagsMcp();

        Assert.Same(builder, returned);

        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<McpServerOptions>>().Value;

        var toolNames = options.ToolCollection!
            .Select(t => t.ProtocolTool.Name ?? "")
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            new[] { "describe_project", "read_tag_value", "read_tag_values", "write_tag_value", "write_tag_values" },
            toolNames);

        var resourceUris = options.ResourceCollection!
            .Select(r => r.ProtocolResource?.Uri ?? r.ProtocolResourceTemplate?.UriTemplate ?? "")
            .ToArray();
        Assert.Equal(new[] { "docs://stdunit.tags/tool_guide" }, resourceUris);
    }

    #endregion


    #region 测试 TryParseValue（按 TagKind 做精确类型转换）

    /// <summary>
    /// 覆盖 <see cref="TagsMcpServerTools"/> 中按 TagKind 转换分支所用的测点项目：
    /// 每个测点的 name 即其 TagKind（其中 <c>vendor</c> 用非内置类型，走 default 分支）。
    /// </summary>
    private static XElement ConversionXml => new("Project",
        new XElement("Channel", new XAttribute("name", "cv"), new XAttribute("driver", "fake")),
        new XElement("TagGrp",
            new XAttribute("name", "cv"), new XAttribute("isEntry", "true"),
            new XAttribute("isEnabled", "true"), new XAttribute("channel", "cv"),
            ConversionTag("bit", BuiltinTagKinds.BIT, 0),
            ConversionTag("byte", BuiltinTagKinds.BYTE, 2),
            ConversionTag("int16", BuiltinTagKinds.INT16, 4),
            ConversionTag("uint16", BuiltinTagKinds.UINT16, 6),
            ConversionTag("int32", BuiltinTagKinds.INT32, 8),
            ConversionTag("uint32", BuiltinTagKinds.UINT32, 12),
            ConversionTag("int64", BuiltinTagKinds.INT64, 16),
            ConversionTag("uint64", BuiltinTagKinds.UINT64, 24),
            ConversionTag("float", BuiltinTagKinds.FLOAT, 32),
            ConversionTag("double", BuiltinTagKinds.DOUBLE, 36),
            ConversionTag("str", BuiltinTagKinds.STR, 44),
            ConversionTag("di", BuiltinTagKinds.DI, 46),
            ConversionTag("do", BuiltinTagKinds.DO, 47),
            ConversionTag("vendor", "VENDOR_CUSTOM", 48)
        )
    );

    private static XElement ConversionTag(string name, string kind, int address) => new("Tag",
        new XAttribute("name", name), new XAttribute("type", kind),
        new XAttribute("access", "RW"), new XAttribute("address", address));

    /// <summary>
    /// 没有任何入口的项目：其测点找不到所属入口（<see cref="ITagExtensions.SearchEntry"/> 返回 null）。
    /// </summary>
    private static XElement NoEntryXml => new("Project",
        new XElement("Channel", new XAttribute("name", "lone"), new XAttribute("driver", "fake")),
        new XElement("TagGrp",
            new XAttribute("name", "lone"), new XAttribute("channel", "lone"),
            new XElement("Tag", new XAttribute("name", "t"), new XAttribute("type", "BIT"),
                new XAttribute("access", "RW"), new XAttribute("address", "0"))
        )
    );

    [Theory]
    [InlineData("bit", "true", typeof(bool))]
    [InlineData("bit", "False", typeof(bool))]
    [InlineData("byte", "255", typeof(byte))]
    [InlineData("int16", "-32768", typeof(short))]
    [InlineData("uint16", "65535", typeof(ushort))]
    [InlineData("int32", "-2147483648", typeof(int))]
    [InlineData("uint32", "4294967295", typeof(uint))]
    [InlineData("int64", "-9223372036854775808", typeof(long))]
    [InlineData("uint64", "18446744073709551615", typeof(ulong))]
    [InlineData("float", "3.14", typeof(float))]
    [InlineData("double", "2.718281828", typeof(double))]
    [InlineData("str", "hello 测点", typeof(string))]
    [InlineData("di", "true", typeof(bool))]
    [InlineData("do", "false", typeof(bool))]
    [InlineData("vendor", "vendor-specific-literal", typeof(string))]
    public async Task WriteTag_ConvertsValueToExactTagKindType(string tagName, string input, Type expectedType)
    {
        using var proj = Factory.Create(null!, ConversionXml);
        using var cts = new CancellationTokenSource();
        _ = Task.Run(() => proj.RunAsync(cts.Token));
        var tools = MakeTools(proj);

        var result = await tools.WriteTagValue($"cv/{tagName}", input);

        AssertSuccess(result);
        var written = proj.Tags.SelectTag($"cv/{tagName}").Value;
        Assert.NotNull(written);
        Assert.Equal(expectedType, written!.GetType());
        // 期望值同样按当前区域设置解析，与 TryParseValue 保持一致（避免小数分隔符依赖）
        Assert.Equal(Convert.ChangeType(input, expectedType), written);

        cts.Cancel();
    }

    [Theory]
    [InlineData("bit", "not-a-bool")]
    [InlineData("byte", "256")]
    [InlineData("int16", "abc")]
    [InlineData("uint16", "-1")]
    [InlineData("int32", "1.5")]
    [InlineData("uint32", "-1")]
    [InlineData("int64", "abc")]
    [InlineData("uint64", "-1")]
    [InlineData("float", "abc")]
    [InlineData("double", "abc")]
    [InlineData("di", "abc")]
    [InlineData("do", "abc")]
    public async Task WriteTag_ValueNotParsableAsTagKind_ReturnsConversionFailure(string tagName, string input)
    {
        using var proj = Factory.Create(null!, ConversionXml);
        // 转换失败发生在提交写入意图之前，因此无需启动轮询
        var tools = MakeTools(proj);

        var result = await tools.WriteTagValue($"cv/{tagName}", input);

        AssertFail(result, "Failed to convert value");
    }

    [Fact]
    public async Task WriteTag_NullValue_ReturnsConversionFailure()
    {
        using var proj = Factory.Create(null!, ConversionXml);
        var tools = MakeTools(proj);

        var result = await tools.WriteTagValue("cv/bit", null!);

        AssertFail(result, "Failed to convert value");
    }

    [Fact]
    public async Task WriteTags_NullValue_ReturnsConversionFailure()
    {
        using var proj = Factory.Create(null!, ConversionXml);
        var tools = MakeTools(proj);

        var result = await tools.WriteTagValues(new Dictionary<string, string> { ["cv/bit"] = null! });

        AssertFail(result, "Failed to convert value");
    }

    #endregion


    #region 测试 写入的失败路径

    [Fact]
    public async Task WriteTag_TagWithoutAncestorEntry_ReturnsFailure()
    {
        using var proj = Factory.Create(null!, NoEntryXml);
        var tools = MakeTools(proj);

        var result = await tools.WriteTagValue("lone/t", "true");

        AssertFail(result, "Could not find an entry group");
    }

    [Fact]
    public async Task WriteTags_TagWithoutAncestorEntry_ReturnsFailure()
    {
        using var proj = Factory.Create(null!, NoEntryXml);
        var tools = MakeTools(proj);

        var result = await tools.WriteTagValues(new Dictionary<string, string> { ["lone/t"] = "true" });

        AssertFail(result, "Could not find an entry group");
    }

    [Fact]
    public async Task WriteTag_WhenIntentQueueIsFull_ReturnsFailure()
    {
        using var proj = Factory.Create(null!, TestXml);
        var tools = MakeTools(proj);

        // 不启动轮询 ⇒ 无人排空意图队列； IntentCapacity 默认为 5
        for (var i = 0; i < proj.IntentCapacity; i++)
        {
            AssertSuccess(await tools.WriteTagValue("g1/sub/bit_tag", "true", waitForCompletion: false));
        }

        var result = await tools.WriteTagValue("g1/sub/bit_tag", "true", waitForCompletion: false);

        AssertFail(result, "Intent queue may be full");
    }

    [Fact]
    public async Task WriteTags_WhenIntentQueueIsFull_ReturnsFailure()
    {
        using var proj = Factory.Create(null!, TestXml);
        var tools = MakeTools(proj);

        for (var i = 0; i < proj.IntentCapacity; i++)
        {
            AssertSuccess(await tools.WriteTagValue("g1/sub/bit_tag", "true", waitForCompletion: false));
        }

        var result = await tools.WriteTagValues(new Dictionary<string, string> { ["g1/sub/bit_tag"] = "true" });

        AssertFail(result, "Intent queue may be full");
    }

    [Fact]
    public async Task WriteTag_WhenPendingIntentAborted_ReportsFailure()
    {
        var proj = Factory.Create(null!, TestXml);
        var tools = MakeTools(proj);

        // 意图已入队但无人消费；释放项目会让未消费的意图以异常收尾
        var pending = tools.WriteTagValue("g1/sub/bit_tag", "true");
        proj.Dispose();

        var result = await pending;

        AssertFail(result, "failed");
    }

    [Fact]
    public async Task WriteTags_NotWaitingForCompletion_ReportsQueued()
    {
        using var proj = Factory.Create(null!, TestXml);
        var tools = MakeTools(proj);

        var result = await tools.WriteTagValues(
            new Dictionary<string, string> { ["g1/sub/bit_tag"] = "true" },
            waitForCompletion: false);

        AssertSuccess(result);
        Assert.Contains("queued successfully", result.Message);
    }

    [Fact]
    public async Task WriteTags_WhenPendingIntentsAborted_ReportsBatchFailure()
    {
        var proj = Factory.Create(null!, TestXml);
        var tools = MakeTools(proj);

        var pending = tools.WriteTagValues(new Dictionary<string, string>
        {
            ["g1/sub/bit_tag"] = "true",
            ["g2/float_tag"] = "1.0",
        });
        proj.Dispose();

        var result = await pending;

        AssertFail(result, "Batch write failed");
    }

    [Fact]
    public void ReadTag_WhenReadingValueThrows_ReturnsErrorInsteadOfThrowing()
    {
        var descriptor = new TagDescriptor { TagName = "boom", TagKind = BuiltinTagKinds.BIT, AccessMode = TagAccessMode.RW };
        var tag = new Mock<ITag>();
        tag.SetupGet(t => t.TagDescriptor).Returns(descriptor);
        tag.SetupGet(t => t.Value).Throws(new InvalidCastException("boom"));
        tag.SetupGet(t => t.Timestamp).Returns(UnixEpoch);

        var grp = new Mock<ITagGrp>();
        grp.Setup(g => g.Descendant("g1/boom")).Returns(new TagUnion.TagUnit(tag.Object));

        var proj = new Mock<ITagsProject>();
        proj.SetupGet(p => p.Tags).Returns(grp.Object);
        var tools = MakeTools(proj.Object);

        var result = tools.ReadTagValue("g1/boom");

        Assert.Same(descriptor, result.Descriptor);
        Assert.Null(result.Value);
        Assert.Equal(UnixEpoch, result.Timestamp);
        Assert.Equal("boom", result.Error);
    }

    [Fact]
    public async Task ToolsWithoutProject_ThrowForAllOperations()
    {
        var tools = new TagsMcpServerTools(new ProjectCtrl(null), Logger);

        Assert.Throws<InvalidOperationException>(() => tools.ReadTagValue("g1/sub/bit_tag"));
        Assert.Throws<InvalidOperationException>(() => tools.ReadTagValues(new[] { "g1/sub/bit_tag" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => tools.WriteTagValue("g1/sub/bit_tag", "true"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tools.WriteTagValues(new Dictionary<string, string> { ["g1/sub/bit_tag"] = "true" }));
    }

    #endregion

    private sealed class ProjectCtrl(ITagsProject? project) : ITagsProjectCtrl
    {
        public ITagsProject? Project => project;
        public Func<Exception, Task<bool>>? OnStartingException { get; set; }

        public event TagsProjectStartedOrStopped? StartedOrStopped;
        public Task StartPollAsync(string? dir, XElement? root, Func<ITagsProject, IServiceProvider, CancellationToken, Task> hook)
            => throw new NotSupportedException();
        public Task StopAsync() => Task.CompletedTask;
    }
}
