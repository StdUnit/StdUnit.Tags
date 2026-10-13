using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace StdUnit.Tags.Tests.R3;

/// <summary>
/// 用于 R3 扩展测试的 Fake ITagsProjectCtrl，支持手动触发启停事件
/// </summary>
internal sealed class FakeR3TagsProjectCtrl : ITagsProjectCtrl
{
    public ITagsProject? Project { get; private set; }
    public Func<Exception, Task<bool>>? OnStartingException { get; set; }

    public event TagsProjectStartedOrStopped? StartedOrStopped;

    public Task StartPollAsync(string? dir, XElement? root, Func<ITagsProject, IServiceProvider, CancellationToken, Task> hook)
        => throw new NotSupportedException();

    public Task StopAsync() => Task.CompletedTask;

    public void FireStarted(ITagsProject? project = null)
    {
        Project = project;
        var args = new TagsProjectEventArgs(true, project);
        StartedOrStopped?.Invoke(this, args);
    }

    public void FireStopped()
    {
        Project = null;
        var args = new TagsProjectEventArgs(false, null);
        StartedOrStopped?.Invoke(this, args);
    }
}
