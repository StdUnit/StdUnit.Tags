using System;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StdUnit.Tags.Tests.Fakes;
using StdUnit.Tags.WebStream;
using Xunit;

namespace StdUnit.Tags.Tests.NetCoreOnly.WebStream;

/// <summary>
/// <c>AddTagsStream</c>：对外只暴露 <see cref="ITagsStreamHub"/>，且它与 hosted service 是同一个实例；
/// 具名流用 <c>AddKeyedTagsStream(key)</c> 决定观测哪个 <see cref="ITagsProjectCtrl"/>。
/// </summary>
public class TagsStreamRegistrationTests
{
    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddFakedSupport();
            b.AddFakedTagSupport();
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

        // 同一个实例既是接口服务，又是随宿主启停的 IHostedService。
        Assert.Contains(provider.GetServices<IHostedService>(), h => ReferenceEquals(h, hub));

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
        var hosted = Assert.Single(provider.GetServices<IHostedService>());
        Assert.Same(hub, hosted);
        await hosted.StartAsync(default);
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
            await hosted.StopAsync(default);
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

        // 两路各一个 hosted service，且都指向各自的 hub。
        var hosted = provider.GetServices<IHostedService>().ToArray();
        Assert.Equal(2, hosted.Length);
        Assert.Contains(hosted, h => ReferenceEquals(h, defaultHub));
        Assert.Contains(hosted, h => ReferenceEquals(h, devHub));
        foreach (var service in hosted)
        {
            await service.StartAsync(default);
        }

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
            foreach (var service in hosted)
            {
                await service.StopAsync(default);
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
        var hosted = Assert.Single(provider.GetServices<IHostedService>());
        await hosted.StartAsync(default);
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
            await hosted.StopAsync(default);
        }
    }
}
