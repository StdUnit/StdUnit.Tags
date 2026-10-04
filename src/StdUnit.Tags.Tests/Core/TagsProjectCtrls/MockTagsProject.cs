using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace StdUnit.Tags.Tests.Core.TagsProjectCtrls;

/// <summary>
/// 模拟的测点项目，用于 TagsProjectCtrl 单元测试。
/// </summary>
internal class MockTagsProject : ITagsProject
{
    private readonly List<ITagChannel> _channels = new();

    public IReadOnlyList<ITagChannel> Channels => _channels;
    public IList<ILogicet> Logicets { get; } = new List<ILogicet>();
    public ITagGrp Tags { get; set; } = null!;
    public string? ProjectRoot { get; set; }

    public int DisposeCallCount { get; private set; }
    public int RunAsyncCallCount { get; private set; }
    public int InitializeCallCount { get; private set; }
    public string? CapturedProjRoot { get; private set; }
    public XElement? CapturedRoot { get; private set; }

    /// <summary>
    /// 如果为 true，RunAsync 会立即抛出 InvalidOperationException。
    /// </summary>
    public bool RunAsyncThrows { get; set; }

    /// <summary>
    /// 添加模拟通道以便验证 DisconnectAsync 调用。
    /// </summary>
    public void AddChannel(ITagChannel channel) => _channels.Add(channel);

    public event RunnerStarted? RunnerStarted;
    public event RunnerCrashed? RunnerCrashed;
    public void Initialize(string projRoot, XElement? root = null)
    {
        InitializeCallCount++;
        CapturedProjRoot = projRoot;
        CapturedRoot = root;
        ProjectRoot = projRoot;
    }

    public Task RunAsync(CancellationToken ct)
    {
        RunAsyncCallCount++;

        if (RunAsyncThrows)
        {
            throw new InvalidOperationException("模拟的 RunAsync 异常");
        }

        // 阻塞直到被取消，模拟正在运行的测点项目
        var tcs = new TaskCompletionSource<bool>();
        ct.Register(() => tcs.TrySetResult(true));
        return tcs.Task;
    }

    public void Dispose()
    {
        DisposeCallCount++;
    }

    public XElement? GetRootElement() => null;

    public int IntentCapacity { get; set; }

    public bool WriteIntent(string entry, TagGrpWriteIntent intent, out Task task)
    {
        task = Task.CompletedTask;
        return true;
    }

#pragma warning disable CS0618 // 实现过时的接口成员
    public bool WriteIntent(string entry, TagGrpWriteIntent intent) => true;
#pragma warning restore CS0618

    public ChannelReader<IntentCompletion>? GetIntentReader(string entry) => null;
}
