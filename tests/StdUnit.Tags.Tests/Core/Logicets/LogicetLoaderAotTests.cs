using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StdUnit.Tags.Logicets;
using Xunit;

namespace StdUnit.Tags.Tests.Core.Logicets;

/// <summary>
/// <see cref="LogicetLoader"/> 在"宿主运行时不支持动态加载程序集"（NativeAOT）时的行为。<br/>
/// 真实 AOT 宿主没法在单元测试里跑，这里用重写探测点的子类模拟；真正的端到端验证放在
/// <c>tests/AotSmokeTest</c>（CI 会以 NativeAOT 发布并运行它）。
/// </summary>
public class LogicetLoaderAotTests
{
    /// <summary>模拟"宿主不支持动态加载"的加载器。</summary>
    private sealed class NoDynamicLoadingLogicetLoader : LogicetLoader
    {
        public NoDynamicLoadingLogicetLoader()
            : base(Options.Create(new LogicetLoadOptions()), NullLogger<LogicetLoader>.Instance)
        {
        }

        protected override bool SupportsDynamicAssemblyLoading => false;
    }

    private sealed class DummyProject : ITagsProject
    {
        public IReadOnlyList<ITagChannel> Channels { get; } = [];

        public IList<ILogicet> Logicets { get; } = [];

        public ITagGrp Tags { get; } = new TagGrp(new TagGrpDescriptor() { }, null);

        public string? ProjectRoot => throw new NotImplementedException();

        public int IntentCapacity { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public event RunnerStarted? RunnerStarted;
        public event RunnerCrashed? RunnerCrashed;
        public void SetTurnProcessHandler(string entry, TurnProcess? handler) => throw new NotImplementedException();

        public void Dispose() { }

        public ChannelReader<IntentCompletion>? GetIntentReader(string entry)
        {
            throw new NotImplementedException();
        }

        public XElement? GetRootElement()
        {
            throw new NotImplementedException();
        }

        public void Initialize(string projRoot, XElement? root = null)
        {
            throw new NotImplementedException();
        }

        public Task RunAsync(CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public bool WriteIntent(string entry, TagGrpWriteIntent intent, out Task task)
        {
            throw new NotImplementedException();
        }

        public bool WriteIntent(string entry, TagGrpWriteIntent intent)
        {
            throw new NotImplementedException();
        }
    }

    private static LogicetLoader CreateDefaultLoader() =>
        new(Options.Create(new LogicetLoadOptions()), NullLogger<LogicetLoader>.Instance);

    private static IServiceProvider CreateServices() => new ServiceCollection().BuildServiceProvider();

    [Fact]
    public void LoadLogicets_WhenHostCannotLoadDynamically_ThrowsNotSupported()
    {
        var loader = new NoDynamicLoadingLogicetLoader();

        var ex = Assert.Throws<TagsProjectNotSupportedException>(() =>
            loader.LoadLogicets(
                CreateServices(),
                new[] { "plugins/Demo.Logicets.dll" },
                new DummyProject()));

        // 加载期异常必须带定位上下文（与 Channel(S7-3) 同一语法族），并且要说清补救办法。
        Assert.Equal("Logicet(plugins/Demo.Logicets.dll)", ex.Location);
        Assert.Contains("TryAddLogicet", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadLogicets_WhenNoPluginDeclared_DoesNotThrowEvenIfHostCannotLoadDynamically()
    {
        var loader = new NoDynamicLoadingLogicetLoader();

        var result = loader.LoadLogicets(
            CreateServices(),
            Array.Empty<string>(),
            new DummyProject());

        // "不配 <Logicet> 就不受影响" 是 AOT 支持的核心承诺：宿主不支持动态加载也必须能正常跑。
        Assert.Empty(result.Logicets);
        Assert.Empty(result.Disposables);
    }

    [Fact]
    public void LoadLogicets_WhenPluginDllCannotBeLoaded_LogsAndContinues()
    {
        var loader = CreateDefaultLoader();

        var result = loader.LoadLogicets(
            CreateServices(),
            new[] { "not-exists/Demo.Logicets.dll" },
            new DummyProject());

        // 单个插件坏了不应阻断项目加载（只有"宿主不支持"才快速失败）——这是既有语义，勿改。
        Assert.Empty(result.Logicets);
        Assert.Empty(result.Disposables);
    }
}
