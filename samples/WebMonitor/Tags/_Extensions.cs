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

            builder.AddTagsStream(o =>
            {
                o.ReconcileInterval = TimeSpan.FromMilliseconds(20);
                o.HeartbeatInterval = TimeSpan.FromSeconds(10);
            });


            // 注册具名的测点项目控制器（LineB）
            builder.AddKeyedTagsProjectCtrl(TagsPathDefines.LineBKey);
            builder.AddKeyedTagsStream(TagsPathDefines.LineBKey, o =>
            {
                o.ReconcileInterval = TimeSpan.FromMilliseconds(20);
                o.HeartbeatInterval = TimeSpan.FromSeconds(10);
            });
        });

        // 第二路控制器（具名注册）：项目配置在 Tags/line-b/index.xml。


        // 后台任务：启动两路轮询 + 演示用的值模拟器。
        services.AddHostedService<TagBackgroundService>();
        return services;
    }
}
