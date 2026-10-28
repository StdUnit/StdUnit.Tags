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
    /// 一个简单的 ILogicet 实现，可通过 <see cref="ITagsProject"/> 构造。
    /// </summary>
    private class SimpleLogicet : ILogicet
    {
        public int Order => 0;
        public IReadOnlyList<ITagChannel> Channels { get; }
        public ITagGrp Tags { get; }
        public bool Enabled => true;

        public ITagsProject Project { get; }

        public SimpleLogicet(ITagsProject project)
        {
            Project = project;
            Channels = project.Channels;
            Tags = project.Tags;
        }

        public bool MatchEntry(ITagGrp entry) => true;
        public Task ProcessAsync(ITagGrp entry, ITagChannel? thisChannel, CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>
    /// 构造函数会抛出异常的 ILogicet。
    /// </summary>
    private class ThrowingLogicet : ILogicet
    {
        public ITagsProject Project { get; }
        public int Order => 0;
        public IReadOnlyList<ITagChannel> Channels => Array.Empty<ITagChannel>();
        public ITagGrp Tags => null!;
        public bool Enabled => false;

        public ThrowingLogicet(ITagsProject project)
        {
            throw new InvalidOperationException("ctor failure");
        }

        public bool MatchEntry(ITagGrp entry) => false;
        public Task ProcessAsync(ITagGrp entry, ITagChannel? thisChannel, CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>
    /// 一个 ILogicetCreator，可根据配置决定返回什么实例。
    /// </summary>
    private class ConfigurableLogicetCreator : ILogicetCreator
    {
        /// <summary>当不为 null 时，始终返回此实例。</summary>
        public ILogicet? FixedInstance { get; set; }

        public ILogicet? CreateLogicet(IServiceProvider sp, Type type, ITagsProject proj)
            => FixedInstance;
    }

    [Fact]
    public void TryAddLogicet_Simple_Success()
    {
        // Arrange
        var channels = Array.Empty<ITagChannel>();
        var tags = new MockTagGrp(new TagGrpDescriptor { Name = "mock" });
        var project = new MockProject(channels, tags);

        var sp = new ServiceCollection().BuildServiceProvider();

        // Act
        var result = project.TryAddLogicet<SimpleLogicet>(sp);

        // Assert
        Assert.True(result);
        var logicet = Assert.IsType<SimpleLogicet>(Assert.Single(project.Logicets));
        Assert.Same(project, logicet.Project);
        Assert.Same(project.Tags, logicet.Tags);
    }

    [Fact]
    public void TryAddLogicet_ThrowingConstructor_ReturnsFalse()
    {
        // Arrange
        var channels = Array.Empty<ITagChannel>();
        var tags = new MockTagGrp(new TagGrpDescriptor { Name = "mock" });
        var project = new MockProject(channels, tags);

        var sp = new ServiceCollection().BuildServiceProvider();

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

        var customLogicet = new SimpleLogicet(project);
        var creator = new ConfigurableLogicetCreator { FixedInstance = customLogicet };

        var services = new ServiceCollection();
        services.AddSingleton<ILogicetCreator>(creator);
        var sp = services.BuildServiceProvider();

        // Act
        var result = project.TryAddLogicet<SimpleLogicet>(sp);

        // Assert
        Assert.True(result);
        Assert.Contains(project.Logicets, l => ReferenceEquals(l, customLogicet));
        Assert.Same(project, customLogicet.Project);
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
        services.AddSingleton<ILogicetCreator>(creator);
        var sp = services.BuildServiceProvider();

        // Act
        var result = project.TryAddLogicet<SimpleLogicet>(sp);

        // Assert
        Assert.True(result);
        var logicet = Assert.IsType<SimpleLogicet>(Assert.Single(project.Logicets));
        Assert.Same(project, logicet.Project);
    }

    /// <summary>
    /// 继承 <see cref="LogicetBase"/> 的最小实现，用于验证基类成员都从项目转发。
    /// </summary>
    private sealed class ProbeLogicet : LogicetBase
    {
        public ProbeLogicet(ITagsProject project) : base(project) { }

        public override int Order => 0;

        public override bool MatchEntry(ITagGrp entry) => false;

        public override Task ProcessAsync(ITagGrp entry, ITagChannel? thisChannel, CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public void LogicetBase_ExposesProject()
    {
        // Arrange
        var tags = new MockTagGrp(new TagGrpDescriptor { Name = "mock" });
        var project = new MockProject(Array.Empty<ITagChannel>(), tags);

        // Act
        var logicet = new ProbeLogicet(project);

        // Assert
        Assert.Same(project, logicet.Project);
        Assert.Same(project.Channels, logicet.Channels);
        Assert.Same(project.Tags, logicet.Tags);
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
        public void SetTurnProcessHandler(string entry, TurnProcess? handler) => throw new NotImplementedException();
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
        public Task ReadAsync(TraversalMode mode, CancellationToken ct) => Task.CompletedTask;
        public Task WriteAsync(TraversalMode mode, CancellationToken ct) => Task.CompletedTask;
        public bool IsDirty() => false;
    }
}
