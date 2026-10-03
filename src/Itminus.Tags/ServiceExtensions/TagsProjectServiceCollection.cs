using Microsoft.Extensions.DependencyInjection;
using System.Xml.Linq;

namespace Itminus.Tags;

/// <summary>
/// extensions for DependencyInjection
/// </summary>
public static class TagsProjectServiceCollection
{

    /// <summary>
    /// 注册测点项目服务，其中可以配置通道工厂、组合测点加载器
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configTagsLoader"></param>
    /// <returns></returns>
    public static IServiceCollection AddTagsProjectServices(this IServiceCollection services, Action<TagsProjectServiceBuilder> configTagsLoader)
    {
        var tpsb = new TagsProjectServiceBuilder(services);
        configTagsLoader?.Invoke(tpsb);
        tpsb.Build();

        services.AddSingleton<ITagsProjectCtrl, TagsProjectCtrl>();
        return services;
    }


    /// <summary>
    /// 以指定的key 注册 <see cref="ITagsProjectCtrl" />
    /// </summary>
    /// <param name="services"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public static IServiceCollection AddKeyedTagsProjectCtrl(this IServiceCollection services, string key)
    {
        services.AddKeyedSingleton<ITagsProjectCtrl, TagsProjectCtrl>(key);
        return services;
    }

    /// <summary>
    /// 构建测点项目实例
    /// </summary>
    /// <param name="sp"></param>
    /// <param name="dir">
    /// 项目根目录。为空时使用当前应用程序目录（<see cref="AppContext.BaseDirectory"/>）。<br/>
    /// 该值会被用作 <see cref="ITagsProject.ProjectRoot"/>，用于解析描述 XML 中的相对路径
    /// （如 SimpleFiles 的 BaseDir、&lt;Logicet&gt; 的 dll 路径）。
    /// </param>
    /// <param name="root">根元素，如果为空，则使用使用index.xml构建</param>
    /// <returns></returns>
    public static ITagsProject MakeProject(this IServiceProvider sp, string? dir = null, XElement? root = null)
    {
        var factory = sp.GetRequiredService<ITagsProjectFactory>();

        if (string.IsNullOrEmpty(dir))
        {
            // 默认取「应用程序目录」。
            // 不要用 Assembly.GetExecutingAssembly().Location —— 它返回的是**当前正在执行的程序集**
            // （在本库内即 Itminus.Tags.dll）所在目录，而不是宿主应用目录；
            // 二者在以下场景会分叉：测试宿主的影子拷贝、库不在应用目录（lib/、NuGet 缓存）、
            // .NET Framework 下经 Assembly.LoadFrom 加载的插件。
            // 本仓库的示例（samples/*）与测试约定（TestPaths）都锚定应用程序目录，此处保持一致。
            dir = AppContext.BaseDirectory;
        }
        if (string.IsNullOrEmpty(dir))
        {
            dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        var proj = factory.Create(dir!, root);
        return proj;
    }

}
