using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.Core.TagsProjectCtrls;

/// <summary>
/// <see cref="TagsProjectCtrlAttachmentExtensions.AttachRunningProject"/>：
/// 把"订阅 + 晚挂补偿 + 按运行去重 + 停止时摘除"收敛成一个方法。
/// </summary>
public class AttachRunningProjectTests
{
    [Fact]
    public void AttachRunningProject_WhenProjectAlreadyRunning_AttachesImmediately()
    {
        var ctrl = new FakeCtrl();
        var project = new MockTagsProject();
        ctrl.Start(project);

        var attachCalls = 0;
        long attachedRunId = 0;
        var subscription = new CountingSubscription();

        using var handle = ctrl.AttachRunningProject((proj, runId) =>
        {
            attachCalls++;
            attachedRunId = runId;
            Assert.Same(project, proj);
            return subscription;
        });

        Assert.Equal(1, attachCalls);
        Assert.Equal(ctrl.RunId, attachedRunId);
    }

    [Fact]
    public void AttachRunningProject_WhenNotRunning_DoesNotAttachUntilStarted()
    {
        var ctrl = new FakeCtrl();
        var attachCalls = 0;

        using var handle = ctrl.AttachRunningProject((_, _) =>
        {
            attachCalls++;
            return new CountingSubscription();
        });

        Assert.Equal(0, attachCalls);

        ctrl.Start(new MockTagsProject());
        Assert.Equal(1, attachCalls);
    }

    [Fact]
    public void AttachRunningProject_SameRunNotifiedTwice_AttachesOnce()
    {
        var ctrl = new FakeCtrl();
        var project = new MockTagsProject();
        ctrl.Start(project);

        var attachCalls = 0;
        using var handle = ctrl.AttachRunningProject((_, _) =>
        {
            attachCalls++;
            return new CountingSubscription();
        });

        // 构造里的补查 + 之后重复的"已启动"（同一次运行）
        ctrl.RaiseStartedAgain(project);
        ctrl.RaiseStartedAgain(project);

        Assert.Equal(1, attachCalls);
    }

    [Fact]
    public void AttachRunningProject_WhenStopped_DisposesPreviousAttachment()
    {
        var ctrl = new FakeCtrl();
        ctrl.Start(new MockTagsProject());

        var subscriptions = new List<CountingSubscription>();
        using var handle = ctrl.AttachRunningProject((_, _) =>
        {
            var s = new CountingSubscription();
            subscriptions.Add(s);
            return s;
        });

        ctrl.Stop();

        Assert.Single(subscriptions);
        Assert.Equal(1, subscriptions[0].DisposeCount);
    }

    [Fact]
    public void AttachRunningProject_WhenRunChanges_DisposesOldAndAttachesNew()
    {
        var ctrl = new FakeCtrl();
        var first = new MockTagsProject();
        ctrl.Start(first);

        var subscriptions = new List<(long RunId, CountingSubscription Subscription)>();
        using var handle = ctrl.AttachRunningProject((_, runId) =>
        {
            var s = new CountingSubscription();
            subscriptions.Add((runId, s));
            return s;
        });

        ctrl.Stop();
        ctrl.Start(new MockTagsProject());

        Assert.Equal(2, subscriptions.Count);
        Assert.NotEqual(subscriptions[0].RunId, subscriptions[1].RunId);
        Assert.Equal(1, subscriptions[0].Subscription.DisposeCount);
        Assert.Equal(0, subscriptions[1].Subscription.DisposeCount);
    }

    [Fact]
    public void AttachRunningProject_Dispose_DetachesAndUnsubscribes()
    {
        var ctrl = new FakeCtrl();
        ctrl.Start(new MockTagsProject());

        var subscriptions = new List<CountingSubscription>();
        var handle = ctrl.AttachRunningProject((_, _) =>
        {
            var s = new CountingSubscription();
            subscriptions.Add(s);
            return s;
        });

        handle.Dispose();
        handle.Dispose(); // 幂等

        Assert.Equal(1, subscriptions[0].DisposeCount);

        // 已经解挂：后续启停不再触发 attach
        ctrl.Stop();
        ctrl.Start(new MockTagsProject());
        Assert.Single(subscriptions);
    }

    [Fact]
    public void AttachRunningProject_WhenAttachThrowsAtConstruction_PropagatesAndUnsubscribes()
    {
        var ctrl = new FakeCtrl();
        ctrl.Start(new MockTagsProject());

        Assert.Throws<InvalidOperationException>(() =>
            ctrl.AttachRunningProject((_, _) => throw new InvalidOperationException("attach-boom")));

        // 构造失败必须把事件订阅撤掉：再启停不应再次抛
        ctrl.Stop();
        ctrl.Start(new MockTagsProject());
    }

    [Fact]
    public void AttachRunningProject_WhenArgsAreNull_Throws()
    {
        ITagsProjectCtrl? ctrl = null;
        Assert.Throws<ArgumentNullException>(() => ctrl!.AttachRunningProject((_, _) => new CountingSubscription()));

        var real = new FakeCtrl();
        Assert.Throws<ArgumentNullException>(() => real.AttachRunningProject(null!));
    }

    private sealed class CountingSubscription : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose() => this.DisposeCount++;
    }

    private sealed class FakeCtrl : ITagsProjectCtrl
    {
        private long _runId;

        public ITagsProject? Project { get; private set; }

        public TagsProjectState State => this.Project is null ? TagsProjectState.Stopped : TagsProjectState.Running;

        public long RunId => Volatile.Read(ref this._runId);

        public Func<Exception, Task<bool>>? OnStartingException { get; set; }

        public event TagsProjectStartedOrStopped? StartedOrStopped;

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

        public void Start(ITagsProject project)
        {
            this.Project = project;
            var runId = Interlocked.Increment(ref this._runId);
            this.StartedOrStopped?.Invoke(this, new TagsProjectEventArgs(TagsProjectState.Running, project, runId));
        }

        /// <summary>模拟重复派发同一个运行的"已启动"。</summary>
        public void RaiseStartedAgain(ITagsProject project)
            => this.StartedOrStopped?.Invoke(this, new TagsProjectEventArgs(TagsProjectState.Running, project, this.RunId));

        public void Stop()
        {
            this.Project = null;
            this.StartedOrStopped?.Invoke(this, new TagsProjectEventArgs(TagsProjectState.Stopped, null, this.RunId));
        }

        public Task StartPollAsync(string? dir, XElement? root, Func<ITagsProject, IServiceProvider, CancellationToken, Task> hook)
            => throw new NotSupportedException();

        public Task StopAsync() => Task.CompletedTask;
    }
}
