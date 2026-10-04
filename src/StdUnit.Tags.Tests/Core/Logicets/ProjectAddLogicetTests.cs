using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Xml.Linq;
using StdUnit.Tags.Logicets;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace StdUnit.Tags.Tests.Core.Logicets;

/// <summary>
/// 测试 <see cref="TagProjectExtensions.TryAddLogicet{TLogicet}(StdUnit.Tags.ITagsProject, IServiceProvider)"/>。
/// </summary>
public class ProjectAddLogicetTests
{
    /// <summary>
    /// 一个简单的 ILogicet 实现，可通过 (IReadOnlyList&lt;ITagChannel&gt;, ITagGrp) 构造。
    /// </summary>
    private class SimpleLogicet : ILogicet
    {
        public int Order => 0;
        public IReadOnlyList<ITagChannel> Channels { get; }
        public ITagGrp Tags { get; }
        public bool Enabled => true;

        public SimpleLogicet(IReadOnlyList<ITagChannel> channels, ITagGrp tags)
        {
            Channels = channels;
            Tags = tags;
        }

        public bool MatchEntry(ITagGrp entry) => true;
        public Task ProcessAsync(ITagGrp entry, ITagChannel? thisChannel) => Task.CompletedTask;
    }

    /// <summary>
    /// 构造函数会抛出异常的 ILogicet。
    /// </summary>
    private class ThrowingLogicet : ILogicet
    {
        public int Order => 0;
        public IReadOnlyList<ITagChannel> Channels => Array.Empty<ITagChannel>();
        public ITagGrp Tags => null!;
        public bool Enabled => false;

        public ThrowingLogicet(IReadOnlyList<ITagChannel> channels, ITagGrp tags)
        {
            throw new InvalidOperationException("ctor failure");
        }

        public bool MatchEntry(ITagGrp entry) => false;
        public Task ProcessAsync(ITagGrp entry, ITagChannel? thisChannel) => Task.CompletedTask;
    }

    /// <summary>
    /// 一个 ILogicetCreator，可根据配置决定返回什么实例。
    /// </summary>
    private class ConfigurableLogicetCreator : ILogicetCreator
    {
        /// <summary>当不为 null 时，始终返回此实例。</summary>
        public ILogicet? FixedInstance { get; set; }

        public ILogicet? CreateLogicet(IServiceProvider sp, Type type, IReadOnlyList<ITagChannel> channels, ITagGrp tags)
            => FixedInstance;
    }

    [Fact]
    public void TryAddLogicet_Simple_Success()
    {
        // Arrange
        var channels = Array.Empty<ITagChannel>();
        var tags = new MockTagGrp(new TagGrpDescriptor { Name = "mock" });
        var project = new MockProject(channels, tags);

        var services = new ServiceCollection();
        services.AddScoped(_ => channels);
        services.AddScoped(_ => tags);
        var sp = services.BuildServiceProvider();

        // Act
        var result = project.TryAddLogicet<SimpleLogicet>(sp);

        // Assert
        Assert.True(result);
        Assert.Contains(project.Logicets, l => l is SimpleLogicet);
    }

    [Fact]
    public void TryAddLogicet_ThrowingConstructor_ReturnsFalse()
    {
        // Arrange
        var channels = Array.Empty<ITagChannel>();
        var tags = new MockTagGrp(new TagGrpDescriptor { Name = "mock" });
        var project = new MockProject(channels, tags);

        var services = new ServiceCollection();
        services.AddScoped(_ => channels);
        services.AddScoped(_ => tags);
        var sp = services.BuildServiceProvider();

        // Act
        var result = project.TryAddLogicet<ThrowingLogicet>(sp);

        // Assert
        Assert.False(result);
        Assert.Empty(project.Logicets);
    }

    [Fact]
    public void TryAddLogicet_WithCreatorInterceptor_UsesCreator()
    {
        // Arrange
        var channels = Array.Empty<ITagChannel>();
        var tags = new MockTagGrp(new TagGrpDescriptor { Name = "mock" });
        var project = new MockProject(channels, tags);

        var customLogicet = new SimpleLogicet(channels, tags);
        var creator = new ConfigurableLogicetCreator { FixedInstance = customLogicet };

        var services = new ServiceCollection();
        services.AddScoped(_ => channels);
        services.AddScoped(_ => tags);
        services.AddSingleton<ILogicetCreator>(creator);
        var sp = services.BuildServiceProvider();

        // Act
        var result = project.TryAddLogicet<SimpleLogicet>(sp);

        // Assert
        Assert.True(result);
        Assert.Contains(project.Logicets, l => ReferenceEquals(l, customLogicet));
    }

    [Fact]
    public void TryAddLogicet_CreatorReturnsNull_FallsBack()
    {
        // Arrange
        var channels = Array.Empty<ITagChannel>();
        var tags = new MockTagGrp(new TagGrpDescriptor { Name = "mock" });
        var project = new MockProject(channels, tags);

        // creator 返回 null，应 fallback 到 ActivatorUtilities
        var creator = new ConfigurableLogicetCreator { FixedInstance = null };

        var services = new ServiceCollection();
        services.AddScoped(_ => channels);
        services.AddScoped(_ => tags);
        services.AddSingleton<ILogicetCreator>(creator);
        var sp = services.BuildServiceProvider();

        // Act
        var result = project.TryAddLogicet<SimpleLogicet>(sp);

        // Assert
        Assert.True(result);
        Assert.Contains(project.Logicets, l => l is SimpleLogicet);
    }

    /// <summary>
    /// 用于测试的 ITagsProject 存根。
    /// </summary>
    private class MockProject : ITagsProject
    {
        public IReadOnlyList<ITagChannel> Channels { get; }
        public IList<ILogicet> Logicets { get; } = new List<ILogicet>();
        public ITagGrp Tags { get; }
        public string? ProjectRoot { get; set; }
        public int IntentCapacity { get; set; }

        public MockProject(IReadOnlyList<ITagChannel> channels, ITagGrp tags)
        {
            Channels = channels;
            Tags = tags;
        }

        public void Dispose() { }
        public XElement? GetRootElement() => null;
        public void Initialize(string projRoot, XElement? root = null) { }
        public Task RunAsync(CancellationToken ct) => Task.CompletedTask;
        public bool WriteIntent(string entry, TagGrpWriteIntent intent, out Task task) { task = Task.CompletedTask; return true; }
#pragma warning disable CS0618
        public bool WriteIntent(string entry, TagGrpWriteIntent intent) => true;
#pragma warning restore CS0618
        public ChannelReader<IntentCompletion>? GetIntentReader(string entry) => null;

        public event RunnerStarted? RunnerStarted;
        public event RunnerCrashed? RunnerCrashed;
    }

    /// <summary>
    /// 用于测试的 ITagGrp 存根。
    /// </summary>
    private class MockTagGrp : ITagGrp
    {
        public MockTagGrp(TagGrpDescriptor descriptor)
        {
            Descriptor = descriptor;
        }

        public ITagGrp? Parent { get; set; }
        public bool IsEntry { get; } = false;
        public IDictionary<string, TagUnion> Children { get; } = new Dictionary<string, TagUnion>();
        public TagUnion this[string tagName] => throw new NotImplementedException();
        public TagUnion Descendant(string path) => throw new NotImplementedException();
        public ITagGrp AddTag(ITag tag) => this;
        public ITagGrp AddTag(ITagCbnt tagCbnt) => this;
        public ITagGrp AddTag(ITagGrp tagGrp) => this;
        public bool IsEnabled { get; set; } = true;
        public TagGrpDescriptor Descriptor { get; set; }
        public ITagChannel? Channel { get; set; }
        public Task ReadAsync(CancellationToken ct) => Task.CompletedTask;
        public Task WriteAsync(CancellationToken ct) => Task.CompletedTask;
        public bool IsDirty() => false;
    }
}
