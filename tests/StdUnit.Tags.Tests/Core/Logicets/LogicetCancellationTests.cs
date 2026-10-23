using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using StdUnit.Tags.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace StdUnit.Tags.Tests.Core.Logicets;

/// <summary>
/// 验证 <see cref="ITagsProject.RunAsync(CancellationToken)"/> 把项目的生命周期令牌透传给
/// <see cref="ILogicet.ProcessAsync(ITagGrp, ITagChannel?, CancellationToken)"/>。
/// </summary>
public class LogicetCancellationTests
{
    private readonly ServiceProvider _root;

    public LogicetCancellationTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddFakedSupport();
        });

        _root = services.BuildServiceProvider();
    }

    /// <summary>
    /// 记录收到的令牌，并在本次处理中触发取消，让轮询循环尽快退出。
    /// </summary>
    private sealed class CapturingLogicet : LogicetBase
    {
        private readonly Action _onProcessed;

        public CapturingLogicet(ITagsProject project, Action onProcessed) : base(project)
        {
            _onProcessed = onProcessed;
        }

        public override int Order => 0;

        public override bool MatchEntry(ITagGrp entry) => true;

        public CancellationToken CapturedToken { get; private set; }

        public override Task ProcessAsync(ITagGrp entry, ITagChannel? thisChannel, CancellationToken ct)
        {
            CapturedToken = ct;
            _onProcessed();
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task RunAsync_PassesLifecycleTokenToLogicet()
    {
        // Arrange
        using var scope = _root.CreateScope();
        var sp = scope.ServiceProvider;
        var factory = sp.GetRequiredService<ITagsProjectFactory>();

        var channel = new XElement("Channel",
            new XAttribute("name", "fake"),
            new XAttribute("driver", "fake"));
        var entry = new XElement("TagGrp",
            new XAttribute("name", "g1"),
            new XAttribute("isEntry", true),
            new XAttribute("isEnabled", true),
            new XAttribute("channel", "fake"),
            new XAttribute("scanInterval", 0));

        using var proj = factory.Create(TestPaths.Fixture("S7Tags"), new XElement("Project", channel, entry));

        using var cts = new CancellationTokenSource();
        var logicet = new CapturingLogicet(proj, cts.Cancel);
        proj.Logicets.Add(logicet);

        // Act
        try
        {
            await proj.RunAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // 取消后轮询循环以 OCE 退出，这是当前设计（见 TagGrpRunnerTests.RunUntilCancelled）。
        }

        // Assert
        Assert.True(logicet.CapturedToken.CanBeCanceled, "ProcessAsync 应收到项目的生命周期令牌");
        Assert.Equal(cts.Token, logicet.CapturedToken);
    }
}
