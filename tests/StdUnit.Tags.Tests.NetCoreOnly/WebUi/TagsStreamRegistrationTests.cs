using System;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using StdUnit.Tags.Tests.Fakes;
using StdUnit.Tags.WebUi;
using Xunit;

namespace StdUnit.Tags.Tests.NetCoreOnly.WebUi;

/// <summary>
/// <c>AddTagsStream</c>：对外只暴露 <see cref="ITagsStreamHub"/>，且它就是 registry 里那一路的同一个实例；
/// 具名流用 <c>AddKeyedTagsStream(key)</c> 决定观测哪个 <see cref="ITagsProjectCtrl"/>。<br/>
/// 启动点是 <c>MapTagsStream()</c>（<see cref="TagsStreamRegistry.StartAll"/>）——这里直接调 registry 模拟。
/// </summary>
public class TagsStreamRegistrationTests
{
    private static ServiceCollection CreateServices(Action<TagsProjectServiceBuilder>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddFakedSupport();
            b.AddFakedTagSupport();
            configure?.Invoke(b);
        });
        return services;
    }

    private static ITagsProject CreateProject(IServiceProvider provider) =>
        provider.GetRequiredService<ITagsProjectFactory>().Create(
            StreamTestContext.ProjectRoot,
            new XElement("Project",
                StreamTestContext.Channel("fake"),
                StreamTestContext.EntryGroup("g1", StreamTestContext.Tag("a"))));

    [Fact]
    public void AddTagsStream_ExposesOnlyTheInterfaceAndReusesTheSameInstance()
    {
        var services = CreateServices();
        services.AddTagsStream();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        var hub = provider.GetRequiredService<ITagsStreamHub>();
        Assert.Same(hub, provider.GetRequiredService<ITagsStreamHub>());  // Singleton

        // 同一个实例：接口服务 = registry 里那一路（端点与启动点共用）。
        Assert.Same(hub, provider.GetRequiredService<TagsStreamRegistry>().GetRequired(null).Hub);

        Assert.Equal(0, hub.Epoch);
        var tree = hub.GetTree();
        Assert.False(tree.IsStarted);
        Assert.Empty(tree.Nodes);
    }

    [Fact]
    public async Task AddTagsStream_BindsToTheKeyedControllerWhenKeyIsGiven()
    {
        var unnamed = new FakeProjectCtrl();
        var named = new FakeProjectCtrl();

        var services = CreateServices();
        services.AddSingleton<ITagsProjectCtrl>(unnamed);                 // 后注册的实例覆盖 AddTagsProjectServices 的默认实现
        services.AddKeyedSingleton<ITagsProjectCtrl>("dev", named);
        services.AddKeyedTagsStream("dev");

        using var provider = services.BuildServiceProvider();
        using var project = CreateProject(provider);

        var hub = provider.GetRequiredKeyedService<ITagsStreamHub>("dev");
        var registry = provider.GetRequiredService<TagsStreamRegistry>();
        var entry = registry.GetRequired("dev");
        Assert.Same(hub, entry.Hub);
        registry.StartAll();
        try
        {
            // 不具名的那个被启动，本 hub 不应有任何反应（它绑的是 "dev"）。
            unnamed.RaiseStarted(project);
            Assert.Equal(0, hub.Epoch);
            Assert.False(hub.GetTree().IsStarted);

            // 具名的那个启动，hub 跟上。
            named.RaiseStarted(project);
            Assert.Equal(1, hub.Epoch);
            Assert.True(hub.GetTree().IsStarted);
            Assert.NotEmpty(hub.GetTree().Nodes);
        }
        finally
        {
            await entry.Hub.StopAsync(default);
        }
    }

    [Fact]
    public async Task AddTagsStream_RegistersOneIndependentHubPerStream()
    {
        var unnamed = new FakeProjectCtrl();
        var named = new FakeProjectCtrl();

        var services = CreateServices();
        services.AddSingleton<ITagsProjectCtrl>(unnamed);
        services.AddKeyedSingleton<ITagsProjectCtrl>("dev", named);
        services.AddTagsStream();                                       // 不具名那一路
        services.AddKeyedTagsStream("dev");                  // 具名那一路

        using var provider = services.BuildServiceProvider();
        using var project = CreateProject(provider);

        var defaultHub = provider.GetRequiredService<ITagsStreamHub>();
        var devHub = provider.GetRequiredKeyedService<ITagsStreamHub>("dev");
        Assert.NotSame(defaultHub, devHub);

        // 两路各一个 hub，且接口服务指向各自的那一个。
        var registry = provider.GetRequiredService<TagsStreamRegistry>();
        Assert.Same(defaultHub, registry.GetRequired(null).Hub);
        Assert.Same(devHub, registry.GetRequired("dev").Hub);
        registry.StartAll();

        try
        {
            // 各自只跟自己的控制器走：state/epoch 互不干扰。
            named.RaiseStarted(project);
            Assert.Equal(0, defaultHub.Epoch);
            Assert.Equal(1, devHub.Epoch);

            unnamed.RaiseStarted(project);
            Assert.Equal(1, defaultHub.Epoch);
            Assert.Equal(1, devHub.Epoch);
        }
        finally
        {
            foreach (var entry in registry.All)
            {
                await entry.Hub.StopAsync(default);
            }
        }
    }

    [Fact]
    public void AddTagsStream_RejectsDuplicateStreamKey()
    {
        var services = CreateServices();
        services.AddKeyedSingleton<ITagsProjectCtrl>("dev", new FakeProjectCtrl());
        services.AddKeyedTagsStream("dev");

        var ex = Assert.Throws<ArgumentException>(() => services.AddKeyedTagsStream("dev"));
        Assert.Contains("\"dev\"", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a/b")]
    [InlineData("ui")]
    [InlineData("Stream")]
    public void AddTagsStream_RejectsUnusableStreamKey(string key)
    {
        var services = CreateServices();
        services.AddKeyedSingleton<ITagsProjectCtrl>(key, new FakeProjectCtrl());

        Assert.Throws<ArgumentException>(() => services.AddKeyedTagsStream(key));
    }

    [Fact]
    public void AddTagsStream_RejectsASecondUnnamedStream()
    {
        var services = CreateServices();
        services.AddTagsStream();

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddTagsStream());
        Assert.Contains("AddKeyedTagsStream", ex.Message);
    }

    [Fact]
    public void AddKeyedTagsStream_RejectsNullAndEmptyKey()
    {
        var services = CreateServices();

        Assert.Throws<ArgumentNullException>(() => services.AddKeyedTagsStream(null!));
        Assert.Throws<ArgumentException>(() => services.AddKeyedTagsStream(string.Empty));
    }

    [Fact]
    public void AddKeyedTagsStream_RejectsTheReservedCatalogKey()
    {
        var services = CreateServices();
        services.AddKeyedSingleton<ITagsProjectCtrl>("catalog", new FakeProjectCtrl());

        // /catalog 是设计器元数据端点（与具体流无关），不能被某一路具名流占用。
        var ex = Assert.Throws<ArgumentException>(() => services.AddKeyedTagsStream("catalog"));
        Assert.Contains("catalog", ex.Message);
    }

    [Fact]
    public void AddTagsStream_WhenTheKeyedControllerIsMissing_SaysWhichKey()
    {
        var services = CreateServices();
        services.AddKeyedTagsStream("dev");

        using var provider = services.BuildServiceProvider();
        // 具名流要按 key 解析：此时才会去 registry 里创建 hub（进而解析 ctrl）。
        var ex = Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredKeyedService<ITagsStreamHub>("dev"));

        Assert.Contains("\"dev\"", ex.Message);
        Assert.Contains("AddKeyedTagsProjectCtrl", ex.Message);
    }

    [Fact]
    public async Task AddTagsStream_BindsToTheUnnamedControllerByDefault()
    {
        var unnamed = new FakeProjectCtrl();
        var named = new FakeProjectCtrl();

        var services = CreateServices();
        services.AddSingleton<ITagsProjectCtrl>(unnamed);
        services.AddKeyedSingleton<ITagsProjectCtrl>("dev", named);
        services.AddTagsStream();                                        // 不传 key

        using var provider = services.BuildServiceProvider();
        using var project = CreateProject(provider);

        var hub = provider.GetRequiredService<ITagsStreamHub>();
        var registry = provider.GetRequiredService<TagsStreamRegistry>();
        var entry = registry.GetRequired(null);
        registry.StartAll();
        try
        {
            named.RaiseStarted(project);
            Assert.Equal(0, hub.Epoch);

            unnamed.RaiseStarted(project);
            Assert.Equal(1, hub.Epoch);
            Assert.True(hub.GetTree().IsStarted);
        }
        finally
        {
            await entry.Hub.StopAsync(default);
        }
    }

    /// <summary>
    /// builder 版（<c>builder.AddTagsStream(...)</c>）只是转调 services 版：注册结果、hosted service 绑定、
    /// 配置回调的去向，以及"不具名只能一路"的校验，都应与 services 版完全一致。
    /// </summary>
    [Fact]
    public void AddTagsStream_OnTheProjectBuilder_RegistersTheUnnamedStream()
    {
        var services = CreateServices(b => b.AddTagsStream(o => o.HeartbeatInterval = TimeSpan.FromSeconds(7)));

        // 同一个服务集合：services 版仍能看到 builder 版注册的那一路，重复注册照样被拒。
        var ex = Assert.Throws<InvalidOperationException>(() => services.AddTagsStream());
        Assert.Contains("AddKeyedTagsStream", ex.Message);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        var hub = provider.GetRequiredService<ITagsStreamHub>();
        Assert.Same(hub, provider.GetRequiredService<ITagsStreamHub>());
        Assert.Same(hub, provider.GetRequiredService<TagsStreamRegistry>().GetRequired(null).Hub);

        // configure 落在这一路流自己的描述符上（不是被丢掉的临时对象）。
        var descriptor = Assert.Single(provider.GetServices<TagsStreamDescriptor>());
        Assert.Null(descriptor.Key);
        Assert.Equal(TimeSpan.FromSeconds(7), descriptor.Options.HeartbeatInterval);
    }

    /// <summary>
    /// builder 版 <c>AddKeyedTagsStream(key, ...)</c> 应绑定到同 key 的具名控制器，
    /// 并把它自己的 <see cref="TagsStreamOptions"/> 交给这一路流。
    /// </summary>
    [Fact]
    public void AddKeyedTagsStream_OnTheProjectBuilder_RegistersTheKeyedStream()
    {
        var named = new FakeProjectCtrl();
        var services = CreateServices(b =>
        {
            b.AddKeyedTagsProjectCtrl("dev");
            b.AddKeyedTagsStream("dev", o => o.ReconcileInterval = TimeSpan.FromMilliseconds(5));
        });

        // 后注册的实例覆盖 builder 里注册的实现，便于断言"这一路流到底绑了谁"。
        services.AddKeyedSingleton<ITagsProjectCtrl>("dev", named);

        using var provider = services.BuildServiceProvider();

        var hub = provider.GetRequiredKeyedService<ITagsStreamHub>("dev");
        Assert.Same(hub, provider.GetRequiredService<TagsStreamRegistry>().GetRequired("dev").Hub);

        var descriptor = Assert.Single(provider.GetServices<TagsStreamDescriptor>());
        Assert.Equal("dev", descriptor.Key);
        Assert.Equal(TimeSpan.FromMilliseconds(5), descriptor.Options.ReconcileInterval);

        // registry 里那一路流观测的就是具名控制器（端点/命令都走同一个 Ctrl）。
        var entry = provider.GetRequiredService<TagsStreamRegistry>().Get("dev");
        Assert.NotNull(entry);
        Assert.Same(named, entry!.Ctrl);
    }
}
