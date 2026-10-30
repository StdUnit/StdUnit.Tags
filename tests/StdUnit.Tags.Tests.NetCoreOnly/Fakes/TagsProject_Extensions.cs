using Microsoft.Extensions.DependencyInjection;

namespace StdUnit.Tags.Tests.Fakes;

// 本文件是主测试项目 Fakes/TagsProject_Extensions.cs 的副本：
// 本项目所需的夹具都在本项目下自带一份，不引用主测试项目的源码（见 csproj 顶部注释）。

internal static class TagsProject_Extensions
{
    internal static TagsProjectServiceBuilder AddFakedSupport(this TagsProjectServiceBuilder builder)
    {
        builder.Services.AddKeyedSingleton<ITagChannelFactory, FakedChannelFactory>("fake");
        builder.ConfigChannelsFactory((sp, composite) =>
        {
            var factory = sp.GetRequiredKeyedService<ITagChannelFactory>("fake");
            composite.AddFactory(factory);
        });

        return builder;
    }

    /// <summary>
    /// 注册 faked 测点构建器，使 faked 通道能解析 &lt;Tag&gt; 元素。
    /// </summary>
    internal static TagsProjectServiceBuilder AddFakedTagSupport(this TagsProjectServiceBuilder builder)
    {
        builder.ConfigTagsLoader((_, composite) =>
        {
            composite.AddDirectTagBuilder((channel, descriptor) =>
                channel is FakedChannel ? new FakedTagBuilder(descriptor) : null);
            composite.AddTagsCbntBuilder((channel, descriptor) =>
                channel is FakedChannel ? new FakedTagCbntBuilder().WithCbntDescriptor(descriptor) : null);
        });
        return builder;
    }
}
