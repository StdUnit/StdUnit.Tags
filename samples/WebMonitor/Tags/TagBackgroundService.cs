using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using StdUnit.Tags;
using WebMonitor.Tags.Logicets;

namespace WebMonitor.Tags;

/// <summary>
/// 启停两路测点项目，并跑一个只用于演示的值模拟器。<br/>
/// 与模板 <c>tags.web</c> 的 <c>TagBackgroundService</c> 同一形状：轮询必须在这里（宿主启动之后）开始，
/// 观测流的 hub 才能先拿到"项目已启动"事件并挂上"轮次完成"回调。
/// </summary>
internal sealed class TagBackgroundService : BackgroundService
{
    private readonly ITagsProjectCtrl _ctrl;
    private readonly ITagsProjectCtrl _lineBCtrl;
    private readonly ILogger<TagBackgroundService> _logger;

    public TagBackgroundService(
        ITagsProjectCtrl ctrl,
        [FromKeyedServices(TagsPathDefines.LineBKey)] ITagsProjectCtrl lineBCtrl,
        ILogger<TagBackgroundService> logger)
    {
        this._ctrl = ctrl;
        this._lineBCtrl = lineBCtrl;
        this._logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tagsRoot = Path.Combine(AppContext.BaseDirectory, "Tags");
        var mainDir = Path.Combine(tagsRoot, "main");
        var lineBDir = Path.Combine(tagsRoot, "line-b");

        var main = this._ctrl.StartPollAsync(mainDir, LoadProjectXml(mainDir), this.Hook1);
        var lineB = this._lineBCtrl.StartPollAsync(lineBDir, LoadProjectXml(lineBDir), this.Hook2);
        var simulator = this.SimulateAsync(stoppingToken);

        try
        {
            await Task.WhenAll(main, lineB, simulator);
        }
        catch (OperationCanceledException)
        {
            // 正常停机：StartPollAsync / 模拟器都以 OCE 结束
        }
        finally
        {
            await StopQuietlyAsync(this._ctrl);
            await StopQuietlyAsync(this._lineBCtrl);
        }
    }

    /// <summary>
    /// 读 <c>index.xml</c>，并把通道里的**相对** <c>BaseDir</c> 焊到这个文件所在的目录。<br/>
    /// SimpleFiles 的 <c>BaseDir</c> 是按<b>进程的工作目录</b>解析的（<c>Path.Combine(BaseDir, address)</c>），
    /// 直接用 <c>.</c> 会让两路项目抢同一个工作目录下的同名文件、`dotnet run` 时还会写进源码目录。
    /// </summary>
    private static XElement LoadProjectXml(string dir)
    {
        var root = XElement.Load(Path.Combine(dir, "index.xml"));
        foreach (var channel in root.Elements("Channel"))
        {
            var baseDir = channel.Element("BaseDir");
            if (baseDir is not null && baseDir.Value.Length > 0 && !Path.IsPathRooted(baseDir.Value))
            {
                baseDir.Value = Path.GetFullPath(Path.Combine(dir, baseDir.Value));
            }
        }

        return root;
    }

    private Task Hook1(ITagsProject project, IServiceProvider services, CancellationToken ct)    {
        project.RunnerStarted += (grp, ch, ct) =>
        {
            this._logger.LogInformation("Tags处理开始,grp={grpName}", grp.TagName());
            return Task.CompletedTask;
        };
        project.RunnerCrashed += (grp, ch, ex, ct) =>
        {
            this._logger.LogError(ex, "Tags处理异常,grp={grpName}", grp.TagName());
            return Task.CompletedTask;
        };

        project.Logicets.Add(new HeartBeatLogicet(
            project,
            services.GetRequiredService<ILogger<HeartBeatLogicet>>()
        ));
        return Task.CompletedTask;
    }

    private Task Hook2(ITagsProject project, IServiceProvider services, CancellationToken ct)
    {
        project.RunnerStarted += (grp, ch, ct) =>
        {
            this._logger.LogInformation("Tags处理开始,grp={grpName}", grp.TagName());
            return Task.CompletedTask;
        };
        project.RunnerCrashed += (grp, ch, ex, ct) =>
        {
            this._logger.LogError(ex, "Tags处理异常,grp={grpName}", grp.TagName());
            return Task.CompletedTask;
        };

        return Task.CompletedTask;
    }

    /// <summary>
    /// 演示用模拟器：每秒把值"投递"到对应入口的轮询线程上改（<see cref="ITagsProject.WriteIntent"/>），
    /// 由 runner 刷写回文件，下一轮再读回来——页面上就能看到值实时跳动。
    /// </summary>
    private async Task SimulateAsync(CancellationToken ct)
    {
        var tick = 0;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(ct))
        {
            tick++;
            await TrySetAsync(this._ctrl, TagsPathDefines.MainEntry, TagsPathDefines.Counter, tick, ct);
            await TrySetAsync(this._ctrl, TagsPathDefines.MainEntry, TagsPathDefines.Level, 50 + (tick % 10), ct);
            await TrySetAsync(this._lineBCtrl, TagsPathDefines.LineBEntry, TagsPathDefines.Counter, tick * 2, ct);
        }
    }

    private async Task TrySetAsync(ITagsProjectCtrl ctrl, string entry, string tagName, int value, CancellationToken ct)
    {
        var project = ctrl.Project;
        if (project is null)
        {
            return;
        }

        Task completion;
        try
        {
            if (!project.WriteIntent(
                entry,
                (grp, _) =>
                {
                    grp.Descendant(tagName).AsTag().Value = value;
                    return ValueTask.CompletedTask;
                },
                out completion))
            {
                return; // 意图队列满：这一拍丢掉，下一拍再来
            }
        }
        catch (Exception)
        {
            return; // 项目正在停止/切换
        }

        var finished = await Task.WhenAny(completion, Task.Delay(TimeSpan.FromSeconds(2), ct));
        if (!ReferenceEquals(finished, completion))
        {
            return; // 入口未轮询（被禁用/已停止）
        }

        try
        {
            await completion;
        }
        catch (Exception)
        {
            // 写入失败不影响 demo 继续跑（下一拍重试）
        }
    }

    private static async Task StopQuietlyAsync(ITagsProjectCtrl ctrl)
    {
        try
        {
            await ctrl.StopAsync();
        }
        catch (Exception)
        {
            // 停机清理失败不影响退出
        }
    }
}
