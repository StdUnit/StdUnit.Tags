using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StdUnit.Tags.Tests.Fakes;
using StdUnit.Tags.WebStream;
using Xunit;

namespace StdUnit.Tags.Tests.NetCoreOnly.WebStream;

/// <summary>
/// <c>AddTagsStream</c>：对外只暴露 <see cref="ITagsStreamHub"/>，且它与 hosted service 是同一个实例。
/// </summary>
public class TagsStreamRegistrationTests
{
    [Fact]
    public void AddTagsStream_ExposesOnlyTheInterfaceAndReusesTheSameInstance()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddFakedSupport();
            b.AddFakedTagSupport();
        });
        services.AddTagsStream();

        // ValidateOnBuild + ValidateScopes：注册表本身也要能被校验通过
        // （实现类是 internal，若 DI 的可访问性检查过不去，这里会先炸）。
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
}
