using StdUnit.Tags;
using StdUnit.Tags.S7;
using Microsoft.Extensions.DependencyInjection;
using StdUnit.Tags.ComScanner;
using StdUnit.Tags.SimpleFiles;

namespace WpfDemo.Tags;

internal static class ServiceExtensions
{
    public static IServiceCollection AddWpfDemoTags(this IServiceCollection services)
    {
        services.AddTagsProjectServices(builder =>
        {
            builder.AddS7Support();
            builder.AddComScannerSupport();
            builder.AddSimpleFilesSupport();
        });

        return services;
    }

}
