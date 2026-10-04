using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StdUnit.Tags.Hjzk;

/// <summary>
/// Extension methods for TagsProjectServiceBuilder to add Hjzk support.
/// </summary>
public static class TagsProject_Extensions
{
    /// <summary>
    /// 增加Hjzk支持
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static TagsProjectServiceBuilder AddHjzkSupport(this TagsProjectServiceBuilder builder)
    {

        builder.Services.AddSingleton<ITagsProjectSchemaProvider, HjzkSchemaProvider>();

        // register channel factory
        builder.Services.AddKeyedSingleton<ITagChannelFactory, HjzkChannelFactory>(HjzkNames.DriverName);
        builder.ConfigChannelsFactory((sp, composite) =>
        {
            var factory = sp.GetRequiredKeyedService<ITagChannelFactory>(HjzkNames.DriverName);
            composite.AddFactory(factory);
        });

        // register tags loader
        builder.ConfigTagsLoader((sp, composite) =>
        {
            composite.AddTagsCbntBuilder<HjzkDICbntBuilder>(HjzkNames.DriverName, predicate: b => b.Area == "DI");
            composite.AddTagsCbntBuilder<HjzkDOCbntBuilder>(HjzkNames.DriverName, predicate: b => b.Area == "DO");
        });

        return builder;
    }
}
