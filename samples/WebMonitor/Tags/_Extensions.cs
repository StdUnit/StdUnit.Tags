using StdUnit.Tags;
using StdUnit.Tags.SimpleFiles;
using StdUnit.Tags.WebUi;

namespace WebMonitor.Tags;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTags(this IServiceCollection services)
    {
        services.AddTagsProjectServices(builder =>
        {
            // 本示例只用 SimpleFiles（数据是磁盘上的文本文件），不依赖任何硬件/网络。
            builder.AddSimpleFilesChannel()
                .AddSimpleFilesDirectTagBuilder();
        });

        // 第二路控制器（具名注册）：项目配置在 Tags/line-b/index.xml。
        services.AddKeyedTagsProjectCtrl(TagsPathDefines.LineBKey);

        // WebUi：不具名流 + 一路具名流（多流各有一套端点与监控页）。
        services.AddTagsStream(o =>
        {
            o.ReconcileInterval = TimeSpan.FromMilliseconds(20);
            o.HeartbeatInterval = TimeSpan.FromSeconds(10);
        });
        services.AddKeyedTagsStream(TagsPathDefines.LineBKey, o =>
        {
            o.ReconcileInterval = TimeSpan.FromMilliseconds(20);
            o.HeartbeatInterval = TimeSpan.FromSeconds(10);
        });

        // 后台任务：启动两路轮询 + 演示用的值模拟器。
        services.AddHostedService<TagBackgroundService>();
        return services;
    }
}
