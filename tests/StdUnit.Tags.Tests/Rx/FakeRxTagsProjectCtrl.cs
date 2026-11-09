using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace StdUnit.Tags.Tests.Rx;

/// <summary>
/// 用于 Rx 扩展测试的 Fake ITagsProjectCtrl，支持手动触发启停事件
/// </summary>
internal sealed class FakeRxTagsProjectCtrl : ITagsProjectCtrl
{
    public ITagsProject? Project { get; private set; }

    public TagsProjectState State => this.Project is null ? TagsProjectState.Stopped : TagsProjectState.Running;

    public long RunId { get; private set; }

    public bool TryGetRunning(out ITagsProject project, out long runId)
    {
        if (this.Project is { } running)
        {
            project = running;
            runId = this.RunId;
            return true;
        }

        project = null!;
        runId = 0;
        return false;
    }

    public Func<Exception, Task<bool>>? OnStartingException { get; set; }

    public event TagsProjectStartedOrStopped? StartedOrStopped;

    public Task StartPollAsync(string? dir, XElement? root, Func<ITagsProject, IServiceProvider, CancellationToken, Task> hook)
        => throw new NotSupportedException();

    public Task StopAsync() => Task.CompletedTask;

    public void FireStarted(ITagsProject project)
    {
        // 真实控制器派发"已启动"时 Project 必定非 null；这个 Fake 也保持同一不变量，
        // 否则会造出"State=Running 却没有项目"的畸形事件参数。
        Project = project;
        RunId++;
        var args = new TagsProjectEventArgs(TagsProjectState.Running, project, RunId, null);
        StartedOrStopped?.Invoke(this, args);
    }

    public void FireStopped()
    {
        Project = null;
        var args = new TagsProjectEventArgs(TagsProjectState.Stopped, null);
        StartedOrStopped?.Invoke(this, args);
    }
}
