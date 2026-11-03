using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using StdUnit.Tags.Tests.Fakes;
using Xunit;

namespace StdUnit.Tags.Tests.Core.Projects;

/// <summary>
/// 验证 <see cref="ITagsProject.RunAsync"/> 的启动是<b>事务性</b>的：<br/>
/// - 为入口匹配逻辑组件（<see cref="ILogicet.MatchEntry"/> / <see cref="ILogicet.Order"/>）抛出的异常
///   报成 <see cref="TagsProjectConfigurationException"/>（带入口路径与逻辑组件类型），而不是变成
///   <c>AggregateException</c> 或走 <see cref="ITagsProject.RunnerCrashed"/> 的重试路径；<br/>
/// - 匹配失败时<b>没有任何入口</b>开始轮询；<br/>
/// - 启动前准备工作（runner 工厂、意图通道）失败时同样一个 runner 都没启动，不留无人等待的孤儿 runner。
/// </summary>
public class RunAsyncStartupTests
{
    private readonly string _projectRoot;

    public RunAsyncStartupTests()
    {
        _projectRoot = TestPaths.TempPath("RunAsyncStartupTests");
        System.IO.Directory.CreateDirectory(_projectRoot);
    }

    #region fakes

    private abstract class ProbeLogicetBase : ILogicet
    {
        protected ProbeLogicetBase(ITagsProject project) => Project = project;

        public ITagsProject Project { get; }

        public virtual int Order => 0;

        public virtual bool Enabled => true;

        public abstract bool MatchEntry(ITagGrp entry);

        public Task ProcessAsync(ITagGrp entry, ITagChannel? thisChannel, CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>对指定入口抛异常的 <see cref="ILogicet.MatchEntry"/>。</summary>
    private sealed class MatchThrowingLogicet : ProbeLogicetBase
    {
        private readonly string _throwingEntry;

        public MatchThrowingLogicet(ITagsProject project, string throwingEntry) : base(project)
            => _throwingEntry = throwingEntry;

        public override bool MatchEntry(ITagGrp entry)
            => entry.TagName() == _throwingEntry
                ? throw new InvalidOperationException("模拟的 MatchEntry 失败")
                : true;
    }

    /// <summary>读取 <see cref="ILogicet.Order"/> 就抛异常的逻辑组件。</summary>
    private sealed class OrderThrowingLogicet : ProbeLogicetBase
    {
        public OrderThrowingLogicet(ITagsProject project) : base(project)
        {
        }

        public override int Order => throw new InvalidOperationException("模拟的 Order 失败");

        public override bool MatchEntry(ITagGrp entry) => true;
    }

    /// <summary>统计 Create / StartAsync 次数（可指定第几次 Create 抛异常）的 runner 工厂。</summary>
    private sealed class CountingRunnerFactory : ITagGrpRunnerFactory
    {
        private int _createCount;
        private int _startCount;

        public int CreateCount => Volatile.Read(ref _createCount);

        public int StartCount => Volatile.Read(ref _startCount);

        /// <summary>第几次 <see cref="Create"/> 抛异常（0 表示从不抛）。</summary>
        public int ThrowOnCreate { get; set; }

        public ITagGrpRunner Create(ITagsProject project)
        {
            var n = Interlocked.Increment(ref _createCount);
            if (n == ThrowOnCreate)
            {
                throw new InvalidOperationException("模拟的 runner 工厂失败");
            }
            return new CountingRunner(this);
        }

        private sealed class CountingRunner : ITagGrpRunner
        {
            private readonly CountingRunnerFactory _owner;

            public CountingRunner(CountingRunnerFactory owner) => _owner = owner;

            public RunnerStarted? RunnerStarted { get; set; }

            public RunnerCrashed? RunnerCrashed { get; set; }

            public TurnProcess? TurnProcess { get; set; }

            public Task StartAsync(ITagGrp entry, CancellationToken ct)
            {
                Interlocked.Increment(ref _owner._startCount);
                return Task.CompletedTask;
            }
        }
    }

    /// <summary>第 N 个 runner 的 <see cref="ITagGrpRunner.StartAsync"/> 会同步抛出；其余会一直挂到令牌取消。</summary>
    private sealed class SyncThrowingRunnerFactory : ITagGrpRunnerFactory
    {
        private int _createCount;

        public List<HangingRunner> Created { get; } = new();

        /// <summary>第几个 runner 同步抛出（默认永不）。</summary>
        public int ThrowOnCreate { get; set; } = int.MaxValue;

        public ITagGrpRunner Create(ITagsProject project)
        {
            var n = Interlocked.Increment(ref _createCount);
            var runner = new HangingRunner(throwOnStart: n == ThrowOnCreate);
            Created.Add(runner);
            return runner;
        }

        internal sealed class HangingRunner : ITagGrpRunner
        {
            private readonly bool _throwOnStart;

            public HangingRunner(bool throwOnStart) => _throwOnStart = throwOnStart;

            /// <summary>启动返回的 Task（同步抛出时为 null）。</summary>
            public Task? StartTask { get; private set; }

            public RunnerStarted? RunnerStarted { get; set; }

            public RunnerCrashed? RunnerCrashed { get; set; }

            public TurnProcess? TurnProcess { get; set; }

            public Task StartAsync(ITagGrp entry, CancellationToken ct)
            {
                if (_throwOnStart)
                {
                    throw new InvalidOperationException("模拟的同步抛出");
                }

                StartTask = Task.Delay(Timeout.Infinite, ct);
                return StartTask;
            }
        }
    }

    #endregion

    private static XElement Channel(string name) =>
        new("Channel", new XAttribute("name", name), new XAttribute("driver", "fake"));

    private static XElement Entry(string name, string channelName) =>
        new("TagGrp",
            new XAttribute("name", name),
            new XAttribute("isEntry", "true"),
            new XAttribute("isEnabled", "true"),
            new XAttribute("channel", channelName),
            new XAttribute("scanInterval", 0),
            new XElement("Tag",
                new XAttribute("name", "v"),
                new XAttribute("type", "INT16"),
                new XAttribute("access", "RW"),
                new XAttribute("address", "0")));

    /// <summary>每个入口用独立通道（入口通道独占校验要求），并覆盖默认的 runner 工厂。</summary>
    private ServiceProvider CreateServices(ITagGrpRunnerFactory factory)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddFakedSupport();
            b.AddFakedTagSupport();
        });

        // 后注册者胜出：覆盖默认的 TagGrpRunnerFactory
        services.AddSingleton(factory);
        return services.BuildServiceProvider();
    }

    private ITagsProject CreateProject(IServiceProvider sp, XElement root) =>
        sp.GetRequiredService<ITagsProjectFactory>().Create(_projectRoot, root);

    [Fact]
    public void RunAsync_MatchEntryThrows_ReportsConfigurationError_AndStartsNoRunner()
    {
        var factory = new CountingRunnerFactory();
        using var root = CreateServices(factory);
        using var scope = root.CreateScope();
        using var proj = CreateProject(scope.ServiceProvider,
            new XElement("Project",
                Channel("fake1"), Entry("g1", "fake1"),
                Channel("fake2"), Entry("g2", "fake2")));
        proj.Logicets.Add(new MatchThrowingLogicet(proj, throwingEntry: "g2"));

        var ex = Assert.Throws<TagsProjectConfigurationException>(
            () => { _ = proj.RunAsync(CancellationToken.None); });

        Assert.Equal("TagGrp(g2)", ex.Location);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains(nameof(MatchThrowingLogicet), ex.Message, StringComparison.Ordinal);
        Assert.Contains("MatchEntry", ex.Message, StringComparison.Ordinal);

        // 阶段 1（匹配）就失败了：连 runner 都没建，更没有入口开始轮询。
        Assert.Equal(0, factory.CreateCount);
        Assert.Equal(0, factory.StartCount);
    }

    [Fact]
    public void RunAsync_OrderThrows_ReportsConfigurationError_AndStartsNoRunner()
    {
        var factory = new CountingRunnerFactory();
        using var root = CreateServices(factory);
        using var scope = root.CreateScope();
        using var proj = CreateProject(scope.ServiceProvider,
            new XElement("Project", Channel("fake1"), Entry("g1", "fake1")));
        proj.Logicets.Add(new OrderThrowingLogicet(proj));

        var ex = Assert.Throws<TagsProjectConfigurationException>(
            () => { _ = proj.RunAsync(CancellationToken.None); });

        Assert.Equal("TagGrp(g1)", ex.Location);
        Assert.Contains(nameof(OrderThrowingLogicet), ex.Message, StringComparison.Ordinal);
        Assert.Contains("Order", ex.Message, StringComparison.Ordinal);

        Assert.Equal(0, factory.CreateCount);
        Assert.Equal(0, factory.StartCount);
    }

    [Fact]
    public void RunAsync_RunnerFactoryThrowsOnSecondEntry_StartsNoRunner()
    {
        var factory = new CountingRunnerFactory { ThrowOnCreate = 2 };
        using var root = CreateServices(factory);
        using var scope = root.CreateScope();
        using var proj = CreateProject(scope.ServiceProvider,
            new XElement("Project",
                Channel("fake1"), Entry("g1", "fake1"),
                Channel("fake2"), Entry("g2", "fake2")));

        var ex = Assert.Throws<InvalidOperationException>(
            () => { _ = proj.RunAsync(CancellationToken.None); });

        Assert.Contains("模拟的 runner 工厂失败", ex.Message, StringComparison.Ordinal);

        // 关键：全部准备动作都在任何 StartAsync 之前，所以第二个入口准备失败时第一个入口也还没启动。
        // （若恢复成"每入口 准备+启动"的旧结构，这里会是 1，留下一个没人等待的孤儿 runner。）
        Assert.Equal(2, factory.CreateCount);
        Assert.Equal(0, factory.StartCount);
    }

    [Fact]
    public void RunAsync_InvalidIntentCapacity_StartsNoRunner()
    {
        var factory = new CountingRunnerFactory();
        using var root = CreateServices(factory);
        using var scope = root.CreateScope();
        using var proj = CreateProject(scope.ServiceProvider,
            new XElement("Project", Channel("fake1"), Entry("g1", "fake1")));
        proj.IntentCapacity = 0;

        Assert.Throws<ArgumentOutOfRangeException>(
            () => { _ = proj.RunAsync(CancellationToken.None); });

        Assert.Equal(0, factory.CreateCount);
        Assert.Equal(0, factory.StartCount);
    }

    [Fact]
    public async Task RunAsync_StartAsyncThrowsSynchronously_RollsBackAlreadyStartedRunners()
    {
        // 第三方 runner 可以在返回 Task 之前直接抛出（Task 返回值不保证"异常都在 Task 上"）。
        // 此时框架必须取消并等待此前已启动的入口退出，再把原异常抛出——不能留下无人持有的轮询。
        var factory = new SyncThrowingRunnerFactory { ThrowOnCreate = 2 };
        using var root = CreateServices(factory);
        using var scope = root.CreateScope();
        using var proj = CreateProject(scope.ServiceProvider,
            new XElement("Project",
                Channel("fake1"), Entry("g1", "fake1"),
                Channel("fake2"), Entry("g2", "fake2")));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => proj.RunAsync(CancellationToken.None));

        Assert.Contains("模拟的同步抛出", ex.Message, StringComparison.Ordinal);
        Assert.Equal(2, factory.Created.Count);

        var first = factory.Created[0].StartTask;
        Assert.NotNull(first);
        Assert.True(first!.IsCompleted, "回滚必须等待已启动的入口退出，不能留下孤儿轮询");
        Assert.True(first.IsCanceled, "回滚的方式是取消（生命周期令牌被取消）");
        Assert.Null(factory.Created[1].StartTask); // 第二个入口同步抛出，从未产生 Task
    }
}
