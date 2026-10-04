using System;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace StdUnit.Tags.Tests.Core.TagsProjectCtrls;

/// <summary>
/// <see cref="TagsProjectServiceCollection"/> DI 扩展方法的单元测试。
/// </summary>
public class TagsProjectServiceCollectionTests
{
    /// <summary>
    /// <see cref="TagsProjectServiceCollection.AddTagsProjectServices(IServiceCollection, Action{TagsProjectServiceBuilder})"/>
    /// 应注册一个默认（无 key）的 <see cref="ITagsProjectCtrl"/>，
    /// 可通过 <see cref="ServiceProviderServiceExtensions.GetRequiredService{T}(IServiceProvider)"/> 解析。
    /// </summary>
    [Fact]
    public void AddTagsProjectServices_RegistersDefaultCtrl()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        // 最小配置，仅注册基础服务
        services.AddTagsProjectServices(builder => { });

        // 默认（无 key）解析应成功
        using var sp = services.BuildServiceProvider();
        var ctrl = sp.GetRequiredService<ITagsProjectCtrl>();
        Assert.NotNull(ctrl);
    }

    /// <summary>
    /// <see cref="TagsProjectServiceCollection.AddKeyedTagsProjectCtrl(IServiceCollection, string)"/>
    /// 应注册一个指定 key 的 <see cref="ITagsProjectCtrl"/>，可通过 <see cref="ServiceProviderServiceExtensions.GetRequiredKeyedService{T}(IServiceProvider, object?)"/> 解析。
    /// </summary>
    [Fact]
    public void AddTagsProjectCtrl_RegistersKeyedCtrl()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKeyedTagsProjectCtrl("alpha");
        using var sp = services.BuildServiceProvider();

        var ctrl = sp.GetRequiredKeyedService<ITagsProjectCtrl>("alpha");
        Assert.NotNull(ctrl);
    }

    /// <summary>
    /// 不同 key 注册的 <see cref="ITagsProjectCtrl"/> 应为独立实例（singleton 按 key 隔离）。
    /// </summary>
    [Fact]
    public void AddTagsProjectCtrl_MultipleKeys_IndependentInstances()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKeyedTagsProjectCtrl("project1");
        services.AddKeyedTagsProjectCtrl("project2");


        using var sp = services.BuildServiceProvider();
        var ctrl1 = sp.GetRequiredKeyedService<ITagsProjectCtrl>("project1");
        var ctrl2 = sp.GetRequiredKeyedService<ITagsProjectCtrl>("project2");
        Assert.NotNull(ctrl1);
        Assert.NotNull(ctrl2);
        Assert.NotSame(ctrl1, ctrl2); // 不同 key 应返回不同的单例

        // 同一个 key 应返回同一个实例
        var ctrl1again = sp.GetRequiredKeyedService<ITagsProjectCtrl>("project1");
        Assert.Same(ctrl1, ctrl1again);
    }

    /// <summary>
    /// 默认（无 key）注册与 keyed 注册可以共存，互不干扰。
    /// 这是最常见的使用场景：大部分时候取默认实例，高级场景取 keyed 实例。
    /// </summary>
    [Fact]
    public void DefaultAndKeyed_CanCoexist()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITagsProjectFactory>(new MockTagsProjectFactory());
        services.AddTagsProjectServices(builder => { });
        services.AddKeyedTagsProjectCtrl("extra");

        using var sp = services.BuildServiceProvider();
        // 默认实例可无 key 解析
        var defaultCtrl = sp.GetRequiredService<ITagsProjectCtrl>();
        Assert.NotNull(defaultCtrl);

        // keyed 实例可用 key 解析
        var extraCtrl = sp.GetRequiredKeyedService<ITagsProjectCtrl>("extra");
        Assert.NotNull(extraCtrl);

        // 两个实例应是不同的对象
        Assert.NotSame(defaultCtrl, extraCtrl);
    }

    /// <summary>
    /// 未调用 key 的 keyed 实例不应被错误解析。
    /// </summary>
    [Fact]
    public void UnregisteredKey_Throws()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddKeyedTagsProjectCtrl("registered_key");

        using var sp = services.BuildServiceProvider();

        // 已注册的 key 应能解析
        var ctrl = sp.GetRequiredKeyedService<ITagsProjectCtrl>("registered_key");
        Assert.NotNull(ctrl);

        // 未注册的 key 应抛出异常
        Assert.Throws<InvalidOperationException>(() =>
            sp.GetRequiredKeyedService<ITagsProjectCtrl>("unregistered_key"));
    }
}
