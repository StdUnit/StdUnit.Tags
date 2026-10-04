using StdUnit.Tags;

namespace NixMonitor.Tags;

class NixMonitorBackgroundService : BackgroundService
{
    private readonly ILogger<NixMonitorBackgroundService> _logger;

    private readonly ITagsProjectCtrl _ctrl;

    public NixMonitorBackgroundService(ITagsProjectCtrl ctrl, ILogger<NixMonitorBackgroundService> logger)
    {
        this._ctrl = ctrl;
        this._logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 项目根目录：用应用程序目录（与 StdUnit.Tags 在 dir 为空时的默认约定一致）。
        // 不要用 Assembly.GetExecutingAssembly().Location —— 那是当前程序集所在目录，
        // 与应用程序目录在影子拷贝/插件加载等场景下会分叉。
        var dir = AppContext.BaseDirectory;
        stoppingToken.Register(async () =>
        {
            _logger.LogInformation("Tags处理停止");
            await _ctrl.StopAsync();
        });
        await _ctrl.StartPollAsync(Path.Combine(dir, "Tags"), null, (proj, sp, ct) =>
        {
            proj.RunnerStarted += (grp, ch) =>
            {
                _logger.LogInformation("Tags处理开始,grp={grpName}", grp.TagName());
                return Task.CompletedTask;
            };
            proj.RunnerCrashed += (grp, ch, ex) =>
            {
                _logger.LogError(ex, "Tags处理异常,grp={grpName}", grp.TagName());
                return Task.CompletedTask;
            };

            return Task.CompletedTask;
        });
    }
}
