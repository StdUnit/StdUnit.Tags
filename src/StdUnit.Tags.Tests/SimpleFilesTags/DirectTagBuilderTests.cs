using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using StdUnit.Tags.SimpleFiles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace StdUnit.Tags.Tests.SimpleFilesTags;

public class DirectTagBuilderTests
{
    private static readonly TagDescriptor DefaultDescriptor = new()
    {
        TagName = "t",
        RawAddress = "test.txt",
        TagKind = BuiltinTagKinds.INT32,
    };

    private static SimpleFilesDirectTagBuilder CreateBuilder(ITagChannel? selfChannel, TagDescriptor? descriptor = null, string baseDir = "")
    {
        var chdescriptor = new SimpleFilesTagChannelDescriptor()
        {
            Name = "ch",
            BaseDir = baseDir,
        };
        var channel = new SimpleFilesTagChannel(chdescriptor, NullLogger<SimpleFilesTagChannel>.Instance);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "g" }, channel);
        var builder = new SimpleFilesDirectTagBuilder();
        builder
            .WithTagDescriptor(descriptor ?? DefaultDescriptor)
            .WithParent(grp)
            .WithChannel(selfChannel);
        return builder;
    }

    [Fact]
    public void Build_WhenSelfChannelIsNull_UsesFallbackChannel()
    {
        var builder = CreateBuilder(selfChannel: null);
        var channel = builder.Channel ?? builder.Parent.IntoTagContainer().SearchRequiredChannel();
        var tag = builder.Build(channel);

        Assert.IsType<IntDirectTag>(tag);
        Assert.Equal("t", tag.TagName());
    }

    [Fact]
    public void Build_WhenSelfChannelIsSimpleFiles_CreatesTagWithSelfChannel()
    {
        var descriptor = new SimpleFilesTagChannelDescriptor()
        {
            Name = "self-ch",
            BaseDir = "",
        };
        var sfChannel = new SimpleFilesTagChannel(descriptor, NullLogger<SimpleFilesTagChannel>.Instance);
        var builder = CreateBuilder(selfChannel: sfChannel);
        var channel = builder.Channel ?? builder.Parent.IntoTagContainer().SearchRequiredChannel();
        var tag = builder.Build(channel);

        Assert.IsType<IntDirectTag>(tag);
        Assert.Equal("t", tag.TagName());
    }

    [Fact]
    public void Build_SetsTagNameFromDescriptor()
    {
        var descriptor = new TagDescriptor
        {
            TagName = "myTag",
            RawAddress = "data.txt",
            TagKind = BuiltinTagKinds.FLOAT,
        };
        var builder = CreateBuilder(selfChannel: null, descriptor: descriptor);
        var channel = builder.Channel ?? builder.Parent.IntoTagContainer().SearchRequiredChannel();
        var tag = builder.Build(channel);

        Assert.Equal("myTag", tag.TagName());
    }

    [Fact]
    public void Build_SetsNormalizedAddressFromDescriptor()
    {
        var descriptor = new TagDescriptor
        {
            TagName = "v",
            RawAddress = "path/to/file.txt",
            TagKind = BuiltinTagKinds.FLOAT,
        };
        var builder = CreateBuilder(selfChannel: null, descriptor: descriptor);
        var channel = builder.Channel ?? builder.Parent.IntoTagContainer().SearchRequiredChannel();
        var tag = builder.Build(channel);

        Assert.Equal("path/to/file.txt", tag.NormalizedAddress());
    }

    #region 泛型 WithFactory<TVal> 与 WithJsonTagFactory

    /// <summary>
    /// 测试用自定义测点：文本原样往返。<br/>
    /// 用于验证泛型 <see cref="SimpleFilesDirectTagBuilder.WithFactory{TVal}"/>。
    /// </summary>
    class CustomStringTag : SimpleFilesDirectTagBase<string>
    {
        public CustomStringTag(TagDescriptor descriptor, SimpleFilesTagChannel? thisChannel, TagContainer container)
            : base(descriptor, thisChannel, container)
        {
        }

        protected override string? ParseValue(string text) => text;

        protected override string FormatValue(string? value) => value ?? string.Empty;
    }

    record JsonPoint(int X, int Y);

    /// <summary>
    /// 创建独立临时目录（跨平台），测试结束由调用方清理。<br/>
    /// 用作非空 BaseDir，避免硬编码平台相关路径（如 C:\base）。
    /// </summary>
    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "SimpleFilesDirectTagBuilder_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void WithFactory_Generic_ResolvesChannelForNormalization_ButPassesRawThisChannelToFactory()
    {
        var tempDir = CreateTempDir();
        try
        {
            var descriptor = new TagDescriptor
            {
                TagName = "custom",
                RawAddress = "dir/custom.txt",
                TagKind = BuiltinTagKinds.STR,
            };
            // 非空 BaseDir（临时目录），确保 MakePath 产生与 RawAddress 不同的地址，验证归一化确实发生
            var builder = CreateBuilder(selfChannel: null, descriptor: descriptor, baseDir: tempDir);

            SimpleFilesTagChannel? receivedChannel = null;
            TagDescriptor? receivedDescriptor = null;
            builder.WithFactory<string>((d, ch, container) =>
            {
                receivedChannel = ch;
                receivedDescriptor = d;
                return new CustomStringTag(d, ch, container);
            });

            var channel = builder.Channel ?? builder.Parent.IntoTagContainer().SearchRequiredChannel();
            var sfsChannel = Assert.IsAssignableFrom<SimpleFilesTagChannel>(channel);
            var tag = builder.Build(channel);

            // 工厂创建的类型被直接返回
            Assert.IsType<CustomStringTag>(tag);
            // 自身通道为空时，工厂收到的是原始 thisChannel（null），而不是冒泡解析出的通道：
            // 解析出的通道只用于地址归一化，不应冒充测点自身的通道（与内部 SimpleFilesDirectTagFactory 惯例一致）
            Assert.Null(receivedChannel);
            // 地址归一化发生在工厂调用之前：工厂收到时 NormalizedAddress 已等于 MakePath 结果
            Assert.Equal(sfsChannel.MakePath(descriptor.RawAddress), receivedDescriptor!.NormalizedAddress);
            // 构建出的测点同样使用归一化地址；测点自身 Channel 保持 null，读写通过冒泡解析
            Assert.Equal(sfsChannel.MakePath(descriptor.RawAddress), tag.NormalizedAddress());
            Assert.Null(tag.Channel);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void WithFactory_Generic_WhenSelfChannelProvided_PassesExactChannelToFactory()
    {
        var sfChannel = new SimpleFilesTagChannel(
            new SimpleFilesTagChannelDescriptor { Name = "self-ch", BaseDir = "" },
            NullLogger<SimpleFilesTagChannel>.Instance);
        var builder = CreateBuilder(selfChannel: sfChannel);

        SimpleFilesTagChannel? received = null;
        builder.WithFactory<string>((d, ch, container) =>
        {
            received = ch;
            return new CustomStringTag(d, ch, container);
        });

        var channel = builder.Channel ?? builder.Parent.IntoTagContainer().SearchRequiredChannel();
        builder.Build(channel);

        // 设置了自身通道时，工厂收到的是该通道本身
        Assert.Same(sfChannel, received);
    }

    [Fact]
    public void WithFactory_Generic_WhenChannelTypeMismatch_ThrowsInvalidOperationException()
    {
        var builder = CreateBuilder(selfChannel: new FakeNonSimpleFilesChannel());
        builder.WithFactory<string>((d, ch, container) => new CustomStringTag(d, ch, container));

        var channel = builder.Channel ?? builder.Parent.IntoTagContainer().SearchRequiredChannel();
        Assert.Throws<InvalidOperationException>(() => builder.Build(channel));
    }

    [Fact]
    public void WithJsonTagFactory_BuildsJsonDirectTag_WithNormalizedAddress()
    {
        var tempDir = CreateTempDir();
        try
        {
            var descriptor = new TagDescriptor
            {
                TagName = "j",
                RawAddress = "a.json",
                TagKind = "JSON",
            };
            // 非空 BaseDir（临时目录），确保归一化地址与 RawAddress 不同，验证归一化确实发生
            var builder = CreateBuilder(selfChannel: null, descriptor: descriptor, baseDir: tempDir);
            builder.WithJsonTagFactory<JsonPoint>();

            var channel = builder.Channel ?? builder.Parent.IntoTagContainer().SearchRequiredChannel();
            var sfsChannel = Assert.IsAssignableFrom<SimpleFilesTagChannel>(channel);
            var tag = builder.Build(channel);

            Assert.IsType<JsonDirectTag<JsonPoint>>(tag);
            Assert.Equal(sfsChannel.MakePath("a.json"), tag.NormalizedAddress());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    #endregion

    private class FakeNonSimpleFilesChannel : ITagChannel
    {
        public TagChannelDescriptor Descriptor => new TagChannelDescriptor
        {
            Name = "NotSimpleFiles",
            Driver = "Fake",
        };
        public Task EnsureConnectedAsync(bool force, CancellationToken ct) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;
        public void Dispose() { }
    }
}
