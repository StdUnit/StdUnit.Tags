using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Xml.Linq;
using StdUnit.Tags.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace StdUnit.Tags.Tests.Core.TagGrpRunners;

public class TagGrpRunnerTests
{
    /// <summary>
    /// 创建一个 TagGrpRunner + 其依赖，所有 mock 均为公开可访问状态。
    /// </summary>
    private (TagGrpRunner runner, MockTagGrp entry, MockProject project, FakedChannel channel) CreateRunner()
    {
        var channel = new FakedChannel(new TagChannelDescriptor { Name = "fake-channel" });
        var entry = new MockTagGrp(new TagGrpDescriptor { Name = "test-entry", ScanInterval = 10_000 }) { Channel = channel };
        var project = new MockProject();
        var runner = new TagGrpRunner(project, NullLogger<TagGrpRunner>.Instance);
        return (runner, entry, project, channel);
    }

    /// <summary>
    /// 调用 StartAsync 并在取消时吞掉 OperationCanceledException。
    /// TagGrpRunner 的外层 finally 中有 Task.Delay(ScanInterval, ct)，
    /// 取消后会抛出 OCE，这是其当前设计。
    /// </summary>
    private async Task RunUntilCancelled(TagGrpRunner runner, ITagGrp entry, CancellationToken ct)
    {
        try
        {
            await runner.StartAsync(entry, ct);
        }
        catch (OperationCanceledException)
        {
            // expected on cancellation
        }
    }

    [Fact]
    public async Task StartAsync_NormalLoop_CallsReadWriteAndEvents()
    {
        // Arrange
        var (runner, entry, _, _) = CreateRunner();
        entry.Descriptor!.ScanInterval = 20;
        entry.IsEnabled = true;

        using var cts = new CancellationTokenSource();
        var turnStartedCalled = false;
        var turnProcessCalled = false;

        runner.RunnerStarted += (grp, ch) =>
        {
            turnStartedCalled = true;
            return Task.CompletedTask;
        };
        runner.TurnProcess += (grp, ch) =>
        {
            turnProcessCalled = true;
            cts.Cancel();
            return Task.CompletedTask;
        };

        // Act
        await RunUntilCancelled(runner, entry, cts.Token);

        // Assert
        Assert.True(turnStartedCalled, "TurnStarted 应被触发");
        Assert.True(turnProcessCalled, "TurnProcess 应被触发");
        Assert.True(entry.ReadAsyncCallCount >= 1, "ReadAsync 应被调用至少1次");
        Assert.True(entry.WriteAsyncCallCount >= 1, "WriteAsync 应被调用至少1次");
    }

    [Fact]
    public async Task StartAsync_WhenDisabled_SkipsInnerLoop()
    {
        // Arrange
        var (runner, entry, _, _) = CreateRunner();
        entry.Descriptor!.ScanInterval = 500;
        entry.IsEnabled = false;

        using var cts = new CancellationTokenSource(800);

        // Act
        await RunUntilCancelled(runner, entry, cts.Token);

        // Assert
        Assert.Equal(0, entry.ReadAsyncCallCount);
        Assert.Equal(0, entry.WriteAsyncCallCount);
    }

    [Fact]
    public async Task StartAsync_TurnStartedEvent_Fires()
    {
        // Arrange
        var (runner, entry, _, _) = CreateRunner();
        entry.Descriptor!.ScanInterval = 20;
        entry.IsEnabled = true;

        using var cts = new CancellationTokenSource();
        ITagGrp? capturedEntry = null;
        ITagChannel? capturedChannel = null;

        runner.RunnerStarted += (grp, ch) =>
        {
            capturedEntry = grp;
            capturedChannel = ch;
            cts.Cancel();
            return Task.CompletedTask;
        };

        // Act
        await RunUntilCancelled(runner, entry, cts.Token);

        // Assert
        Assert.Same(entry, capturedEntry);
        Assert.Same(entry.Channel, capturedChannel);
    }

    [Fact]
    public async Task StartAsync_TurnProcessEvent_FiresAfterRead()
    {
        // Arrange
        var (runner, entry, _, _) = CreateRunner();
        entry.Descriptor!.ScanInterval = 20;
        entry.IsEnabled = true;

        using var cts = new CancellationTokenSource();
        var processOrder = new List<string>();

        entry.OnRead = () => processOrder.Add("Read");
        entry.OnWrite = () => processOrder.Add("Write");

        runner.TurnProcess += (grp, ch) =>
        {
            processOrder.Add("Process");
            cts.Cancel();
            return Task.CompletedTask;
        };

        // Act
        await RunUntilCancelled(runner, entry, cts.Token);

        // Assert
        Assert.Contains("Read", processOrder);
        Assert.Contains("Process", processOrder);
        Assert.Contains("Write", processOrder);
        var readIdx = processOrder.IndexOf("Read");
        var writeIdx = processOrder.IndexOf("Write");
        Assert.True(readIdx < writeIdx, "Read 应在 Write 之前");
    }

    [Fact]
    public async Task StartAsync_IsDirty_CallsWriteBeforeRead()
    {
        // Arrange
        var (runner, entry, _, _) = CreateRunner();
        entry.Descriptor!.ScanInterval = 20;
        entry.IsEnabled = true;
        entry.IsDirtyReturn = true;

        using var cts = new CancellationTokenSource();
        var callOrder = new List<string>();

        entry.OnWrite = () => callOrder.Add("Write");
        entry.OnRead = () => callOrder.Add("Read");

        runner.TurnProcess += (_, _) =>
        {
            cts.Cancel();
            return Task.CompletedTask;
        };

        // Act
        await RunUntilCancelled(runner, entry, cts.Token);

        // Assert
        var writeIdx = callOrder.IndexOf("Write");
        var readIdx = callOrder.IndexOf("Read");
        Assert.True(writeIdx >= 0, "至少有一次 Write");
        Assert.True(readIdx >= 0, "至少有一次 Read");
        Assert.True(writeIdx < readIdx, "IsDirty 时应在 Read 之前先 Write");
    }

    [Fact]
    public async Task StartAsync_Exception_FiresTurnCrashed()
    {
        // Arrange
        var (runner, entry, _, _) = CreateRunner();
        entry.Descriptor!.ScanInterval = 20;
        entry.IsEnabled = true;
        entry.ReadAsyncThrows = new InvalidOperationException("模拟读取异常");

        // 不用带截止期的 ct 来"到点停循环"：本用例的结束条件是碰撞回调里主动取消，
        // 截止期只是多余的时间猜测（负载高时会先到期，导致断言看到的是取消而不是碰撞）。
        using var cts = new CancellationTokenSource();
        Exception? capturedEx = null;
        var crashedFired = false;

        runner.RunnerCrashed += (grp, ch, ex) =>
        {
            crashedFired = true;
            capturedEx = ex;
            cts.Cancel();
            return Task.CompletedTask;
        };

        // Act（WaitAsync 只作兜底：卡住时快速失败，不参与流程控制）
        await RunUntilCancelled(runner, entry, cts.Token).WaitAsync(TimeSpan.FromSeconds(15));

        // Assert
        Assert.True(crashedFired, "TurnCrashed 应被触发");
        Assert.NotNull(capturedEx);
        Assert.IsType<InvalidOperationException>(capturedEx);
        Assert.Contains("模拟读取异常", capturedEx.Message);
    }

    [Fact]
    public async Task StartAsync_TurnCrashedHandlerThrows_ExceptionPropagates()
    {
        // Arrange
        var (runner, entry, _, _) = CreateRunner();
        entry.Descriptor!.ScanInterval = 20;
        entry.IsEnabled = true;
        entry.ReadAsyncThrows = new InvalidOperationException("模拟读取异常");

        // 不要给 runner 一个会到期的 ct：本用例断言"碰撞回调抛出的异常向外传播"，
        // 而一旦截止期先到期，外层 finally 的 Task.Delay(delay, ct) 会把结果变成 TaskCanceledException，
        // 掩盖真实断言（曾因此在负载高时偶发失败）。兜底改用 WaitAsync —— 超时抛 TimeoutException，
        // 不参与 runner 的取消语义。
        runner.RunnerCrashed += (grp, ch, ex) =>
            throw new InvalidOperationException("错误处理也抛异常");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.StartAsync(entry, CancellationToken.None)).WaitAsync(TimeSpan.FromSeconds(15));

        Assert.Contains("错误处理也抛异常", ex.Message);
    }

    [Fact]
    public async Task StartAsync_Cancellation_StopsLoop()
    {
        // Arrange
        var (runner, entry, _, _) = CreateRunner();
        entry.Descriptor!.ScanInterval = 10;
        entry.IsEnabled = true;

        using var cts = new CancellationTokenSource();

        // 第一次读之后立刻取消：不靠"100ms 内必须跑完一轮"这种固定延时（负载高时会偶发失败）
        entry.OnRead = () => cts.Cancel();

        // Act（WaitAsync 只作兜底）
        await RunUntilCancelled(runner, entry, cts.Token).WaitAsync(TimeSpan.FromSeconds(15));

        // Assert
        Assert.True(entry.ReadAsyncCallCount >= 1, "取消前应至少执行了一次 ReadAsync");
    }

    [Fact]
    public async Task StartAsync_Cancellation_StillDisconnectsChannel()
    {
        // 回归测试：取消轮询时，清理路径必须断开通道。
        // 历史 bug：catch 的 finally 里用已取消的 ct 调 DisconnectAsync(ct)——
        // 通道内部（如 S7 的 _rw.WaitAsync(ct)）会立即抛 OperationCanceledException，
        // 导致连接未断开、资源泄漏。修复后应改用 CancellationToken.None。
        // Arrange
        var channel = new RecordingChannel(new TagChannelDescriptor { Name = "fake-channel" });
        var entry = new MockTagGrp(new TagGrpDescriptor { Name = "test-entry", ScanInterval = 10 })
        {
            Channel = channel,
            IsEnabled = true,
        };
        var project = new MockProject();
        var runner = new TagGrpRunner(project, NullLogger<TagGrpRunner>.Instance);

        using var cts = new CancellationTokenSource();
        // 在 TurnProcess 中取消（模拟正常轮询中用户停止）
        runner.TurnProcess += (_, _) =>
        {
            cts.Cancel();
            return Task.CompletedTask;
        };

        // Act
        await RunUntilCancelled(runner, entry, cts.Token);

        // Assert
        Assert.True(channel.DisconnectAsyncCallCount >= 1, "取消时清理路径应调用 DisconnectAsync");
        Assert.False(channel.LastDisconnectTokenWasCancelled,
            "DisconnectAsync 不应收到已取消的 token——否则通道内部会立刻抛 OCE 导致连接不断开");
    }

    [Fact]
    public async Task StartAsync_WhenDisconnectThrows_SwallowsButLogs()
    {
        // 清理路径有意吞掉 DisconnectAsync 的异常（不阻断清理、不让 StartAsync 失败），
        // 但必须留痕——否则"连接没断干净"无人知晓。
        // Arrange
        var logs = new CapturingLoggerProvider();
        var channel = new RecordingChannel(new TagChannelDescriptor { Name = "fake-channel" })
        {
            DisconnectThrows = new InvalidOperationException("发起断开-boom"),
        };
        var entry = new MockTagGrp(new TagGrpDescriptor { Name = "test-entry", ScanInterval = 10 })
        {
            Channel = channel,
            IsEnabled = true,
        };
        using var loggerFactory = new LoggerFactory();
        loggerFactory.AddProvider(logs);
        var runner = new TagGrpRunner(new MockProject(), loggerFactory.CreateLogger<TagGrpRunner>());

        using var cts = new CancellationTokenSource();
        runner.TurnProcess += (_, _) =>
        {
            cts.Cancel();
            return Task.CompletedTask;
        };

        // Act——不应抛出
        await RunUntilCancelled(runner, entry, cts.Token).WaitAsync(TimeSpan.FromSeconds(15));

        // Assert
        Assert.True(channel.DisconnectAsyncCallCount >= 1);
        var warning = Assert.Single(logs.Entries, e => e.Exception?.Message == "发起断开-boom");
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains("fake-channel", warning.Message); // 消息里带上通道名
    }

    [Fact]
    public async Task StartAsync_WhenDisconnectStrategyThrows_SwallowsButLogs()
    {
        // 等待断开失败同样只吞掉 + 留痕（连接可能没断干净）。
        // Arrange
        var logs = new CapturingLoggerProvider();
        var channel = new RecordingChannel(new TagChannelDescriptor { Name = "fake-channel" });
        var entry = new MockTagGrp(new TagGrpDescriptor { Name = "test-entry", ScanInterval = 10 })
        {
            Channel = channel,
            IsEnabled = true,
        };
        using var loggerFactory = new LoggerFactory();
        loggerFactory.AddProvider(logs);
        var strategy = new RecordingDisconnectStrategy((_, _) => throw new InvalidOperationException("等待断开-boom"));
        var runner = new TagGrpRunner(
            new MockProject(),
            loggerFactory.CreateLogger<TagGrpRunner>(),
            disconnectStrategy: strategy);

        using var cts = new CancellationTokenSource();
        runner.TurnProcess += (_, _) =>
        {
            cts.Cancel();
            return Task.CompletedTask;
        };

        // Act——不应抛出
        await RunUntilCancelled(runner, entry, cts.Token).WaitAsync(TimeSpan.FromSeconds(15));

        // Assert
        Assert.True(strategy.CallCount >= 1);
        var warning = Assert.Single(logs.Entries, e => e.Exception?.Message == "等待断开-boom");
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains("fake-channel", warning.Message);
    }

    [Fact]
    public async Task StartAsync_Cancellation_SlowDisconnect_TimesOut_AndStillExits()
    {
        // 回归测试：断开动作卡住时（模拟底层读持锁），清理路径应在有限超时后放弃等待并退出，
        // 而不是无限阻塞 StartAsync。
        // Arrange
        var slowDisconnect = new TaskCompletionSource<bool>();
        var channel = new RecordingChannel(new TagChannelDescriptor { Name = "fake-channel" })
        {
            SlowDisconnect = slowDisconnect,
        };
        var entry = new MockTagGrp(new TagGrpDescriptor { Name = "test-entry", ScanInterval = 10 })
        {
            Channel = channel,
            IsEnabled = true,
        };
        var project = new MockProject();
        var runner = new TagGrpRunner(project, NullLogger<TagGrpRunner>.Instance);

        using var cts = new CancellationTokenSource();
        runner.TurnProcess += (_, _) =>
        {
            cts.Cancel();
            return Task.CompletedTask;
        };

        // Act——若清理路径无限等待，此调用会超时失败；断开超时默认 5s，这里给 15s 余量
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await RunUntilCancelled(runner, entry, cts.Token).WaitAsync(TimeSpan.FromSeconds(15));
        sw.Stop();

        // Assert：应在断开超时（5s）附近退出，而不是无限等待
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(12),
            $"清理路径应在有限时间内退出，实际耗时 {sw.Elapsed}");
        Assert.True(channel.DisconnectAsyncCallCount >= 1);

        // 清理：让挂起的断开完成，避免测试泄漏
        slowDisconnect.TrySetResult(true);
    }

    [Fact]
    public async Task StartAsync_Cancellation_CustomDisconnectStrategy_IsInvoked()
    {
        // 策略化验证：清理路径应把"断开 Task + 通道"交给注入的断开策略，
        // 而不是在 TagGrpRunner 内部硬编码等待逻辑。
        // Arrange
        var channel = new RecordingChannel(new TagChannelDescriptor { Name = "fake-channel" });
        var entry = new MockTagGrp(new TagGrpDescriptor { Name = "test-entry", ScanInterval = 10 })
        {
            Channel = channel,
            IsEnabled = true,
        };
        var project = new MockProject();

        ITagChannel? receivedChannel = null;
        Task? receivedDisconnect = null;
        var strategy = new RecordingDisconnectStrategy((c, t) => { receivedChannel = c; receivedDisconnect = t; });
        var runner = new TagGrpRunner(project, NullLogger<TagGrpRunner>.Instance, disconnectStrategy: strategy);

        using var cts = new CancellationTokenSource();
        runner.TurnProcess += (_, _) =>
        {
            cts.Cancel();
            return Task.CompletedTask;
        };

        // Act
        await RunUntilCancelled(runner, entry, cts.Token);

        // Assert
        Assert.Equal(channel, receivedChannel);
        Assert.NotNull(receivedDisconnect);
        Assert.True(strategy.CallCount >= 1);
        Assert.True(channel.DisconnectAsyncCallCount >= 1);
    }

    [Fact]
    public async Task StartAsync_MultiChannelEntry_EnsuresAllSubtreeChannelsConnected()
    {
        // 单入口多通道：入口下某个子组使用了自己的通道（ch-2）时，
        // 该轴通道必须在轮询开始前就被建连，否则读写到该子组时才失败。
        // Arrange
        var mainChannel = new RecordingChannel(new TagChannelDescriptor { Name = "S7-1" });
        var auxChannel = new RecordingChannel(new TagChannelDescriptor { Name = "ch-2" });
        var entry = new MockTagGrp(new TagGrpDescriptor { Name = "multi-entry", ScanInterval = 10 })
        {
            Channel = mainChannel,
            IsEnabled = true,
        };
        var auxGrp = new MockTagGrp(new TagGrpDescriptor { Name = "grp2" }) { Channel = auxChannel };
        entry.Children.Add("grp2", new TagUnion.TagGrp(auxGrp));
        var project = new MockProject();
        var runner = new TagGrpRunner(project, NullLogger<TagGrpRunner>.Instance);

        using var cts = new CancellationTokenSource();
        runner.TurnProcess += (_, _) =>
        {
            cts.Cancel();
            return Task.CompletedTask;
        };

        // Act
        await RunUntilCancelled(runner, entry, cts.Token);

        // Assert
        Assert.True(mainChannel.EnsureConnectedCallCount >= 1, "入口主通道应被建连");
        Assert.True(auxChannel.EnsureConnectedCallCount >= 1, "子树辅通道也应被建连");
    }

    [Fact]
    public async Task StartAsync_MultiChannelEntry_DisconnectsAllSubtreeChannels()
    {
        // 清理路径必须断开入口相关的全部通道，而不只是主通道——
        // 否则辅通道连接会残留、占用设备连接数。
        // Arrange
        var mainChannel = new RecordingChannel(new TagChannelDescriptor { Name = "S7-1" });
        var auxChannel = new RecordingChannel(new TagChannelDescriptor { Name = "ch-2" });
        var entry = new MockTagGrp(new TagGrpDescriptor { Name = "multi-entry", ScanInterval = 10 })
        {
            Channel = mainChannel,
            IsEnabled = true,
        };
        var auxGrp = new MockTagGrp(new TagGrpDescriptor { Name = "grp2" }) { Channel = auxChannel };
        entry.Children.Add("grp2", new TagUnion.TagGrp(auxGrp));
        var project = new MockProject();
        var runner = new TagGrpRunner(project, NullLogger<TagGrpRunner>.Instance);

        using var cts = new CancellationTokenSource();
        runner.TurnProcess += (_, _) =>
        {
            cts.Cancel();
            return Task.CompletedTask;
        };

        // Act
        await RunUntilCancelled(runner, entry, cts.Token);

        // Assert
        Assert.True(mainChannel.DisconnectAsyncCallCount >= 1, "清理路径应断开入口主通道");
        Assert.True(auxChannel.DisconnectAsyncCallCount >= 1, "清理路径应断开子树辅通道");
        Assert.False(auxChannel.LastDisconnectTokenWasCancelled, "断开不应收到已取消的 token");
    }

    [Fact]
    public async Task StartAsync_ProcessIntents()
    {
        // Arrange
        var entry = new MockTagGrp(new TagGrpDescriptor { Name = "intent-entry", ScanInterval = 20 })
        {
            Channel = new FakedChannel(new TagChannelDescriptor { Name = "fake-channel" }),
            IsEnabled = true
        };
        var project = new MockProject();
        var runner = new TagGrpRunner(project, NullLogger<TagGrpRunner>.Instance);

        using var cts = new CancellationTokenSource();
        var intentExecuted = false;

        project.WriteIntent("intent-entry", (grp, ct) =>
        {
            intentExecuted = true;
            return default;
        }, out var intentTask);

        runner.TurnProcess += (_, _) =>
        {
            cts.Cancel();
            return Task.CompletedTask;
        };

        // Act
        await RunUntilCancelled(runner, entry, cts.Token);

        // Assert — 意图已被 DrainWriteIntentsAsync 处理，intentTask 应完成
        Assert.True(intentExecuted, "意图应被执行");
        Assert.True(intentTask.Status == TaskStatus.RanToCompletion, "intentTask 应成功完成");
    }

    [Fact]
    public async Task StartAsync_ProcessMultipleIntents_CompletesAllWithSequentialIndex()
    {
        // Arrange
        var entry = new MockTagGrp(new TagGrpDescriptor { Name = "intent-entry", ScanInterval = 20 })
        {
            Channel = new FakedChannel(new TagChannelDescriptor { Name = "fake-channel" }),
            IsEnabled = true
        };
        var project = new MockProject();
        var runner = new TagGrpRunner(project, NullLogger<TagGrpRunner>.Instance);

        using var cts = new CancellationTokenSource();
        var executed = new List<int>();

        project.WriteIntent("intent-entry", (grp, ct) =>
        {
            executed.Add(1);
            return default;
        }, out var first);
        project.WriteIntent("intent-entry", (grp, ct) =>
        {
            executed.Add(2);
            return default;
        }, out var second);

        runner.TurnProcess += (_, _) =>
        {
            cts.Cancel();
            return Task.CompletedTask;
        };

        // Act
        await RunUntilCancelled(runner, entry, cts.Token);

        // Assert — 同一轮次排空的多个意图都应执行且按 FIFO 顺序完成；
        // 完成值是在本次排空批次中的 0 基序号（IntentCompletion.Completion 为 TaskCompletionSource<int>）
        Assert.Equal(new[] { 1, 2 }, executed);
        Assert.True(first.Status == TaskStatus.RanToCompletion, "第1个 intentTask 应成功完成");
        Assert.True(second.Status == TaskStatus.RanToCompletion, "第2个 intentTask 应成功完成");
        Assert.Equal(0, await (Task<int>)first);
        Assert.Equal(1, await (Task<int>)second);
    }

    [Fact]
    public async Task StartAsync_ConsecutiveFailures_GrowDelay()
    {
        // Arrange — 使用一个记录调用次数的假策略
        var mockStrategy = new MockRetryStrategy(delay: TimeSpan.FromMilliseconds(100));
        var entry = new MockTagGrp(new TagGrpDescriptor { Name = "test-entry", ScanInterval = 20 })
        {
            Channel = new FakedChannel(new TagChannelDescriptor { Name = "fake-channel" }),
            IsEnabled = true,
            ReadAsyncThrows = new InvalidOperationException("模拟读取异常")
        };
        var project = new MockProject();
        var runner = new TagGrpRunner(project, NullLogger<TagGrpRunner>.Instance, mockStrategy);

        using var cts = new CancellationTokenSource();
        var crashCount = 0;

        runner.RunnerCrashed += (_, _, _) =>
        {
            crashCount++;

            // 攒够两次崩溃就停：不靠"2000ms 内必须崩够两次"这种固定延时（负载高时会偶发失败）
            if (crashCount >= 2)
            {
                cts.Cancel();
            }
            return Task.CompletedTask;
        };

        // Act（WaitAsync 只作兜底）
        await RunUntilCancelled(runner, entry, cts.Token).WaitAsync(TimeSpan.FromSeconds(15));

        // Assert — 应发生多次崩溃且每次传入的 consecutiveFailureCount 递增
        Assert.True(crashCount >= 2, $"至少应发生2次崩溃，实际={crashCount}");
        Assert.Equal(crashCount, mockStrategy.CallCount);
        for (int i = 1; i < mockStrategy.CallCount; i++)
        {
            Assert.True(mockStrategy.ReceivedCounts[i] > mockStrategy.ReceivedCounts[i - 1],
                $"consecutiveFailureCount 应递增: {mockStrategy.ReceivedCounts[i - 1]} -> {mockStrategy.ReceivedCounts[i]}");
        }
    }

    [Fact]
    public async Task StartAsync_FailResetThenAccumulateAgain()
    {
        // Arrange — 验证：失败→成功复位→再连续失败，计数器从 1 重新累计（1,2,3... 而非 4,5,6...）
        var mockStrategy = new MockRetryStrategy(delay: TimeSpan.FromMilliseconds(10));
        var entry = new MockTagGrp(new TagGrpDescriptor { Name = "test-entry", ScanInterval = 10 })
        {
            Channel = new FakedChannel(new TagChannelDescriptor { Name = "fake-channel" }),
            IsEnabled = true,
        };
        var project = new MockProject();
        var runner = new TagGrpRunner(project, NullLogger<TagGrpRunner>.Instance, mockStrategy);

        using var cts = new CancellationTokenSource();

        // phase:
        //   0 → 连续失败（累计 1,2,3）
        //   1 → 在 crash handler 中禁用 entry，让外循环复位（!IsEnabled → continue → finally 复位）
        //   2 → 重新启用，再次连续失败（从 1 重新累计 1,2,3...）
        //   3 → 停止
        var phase = 0;
        var totalCrashes = 0;
        var disabledReadCount = 0;

        entry.OnRead = () =>
        {
            if (phase == 0 || phase == 2)
            {
                throw new InvalidOperationException("模拟读取异常");
            }
        };

        // 复位在轮询循环内部完成，外部观察不到它的结束时刻；但 IsEnabled 每轮外循环只读一次，
        // 而复位就在"读到 false"那一轮的末尾。因此用读取次数做确定性同步：
        // 第 2 次读到 false 时才重新启用——此时复位必然已完成（同一线程串行）。
        // 不要用 Task.Delay 猜这个时刻：负载高时定时器会先于复位触发，断言随机会挂。
        entry.OnIsEnabledRead = () =>
        {
            if (phase != 1)
            {
                return;
            }

            disabledReadCount++;
            if (disabledReadCount >= 2)
            {
                entry.IsEnabled = true;
                phase = 2;
            }
        };

        runner.RunnerCrashed += (_, _, _) =>
        {
            totalCrashes++;

            if (phase == 0 && totalCrashes >= 3)
            {
                // 已累计 3 次失败，禁用 entry 以触发复位（何时重新启用由 OnIsEnabledRead 决定）
                phase = 1;
                entry.IsEnabled = false;
            }
            else if (phase == 2 && totalCrashes >= 6)
            {
                // 复位后又累计了 3 次（totalCrashes 6 = 前 3 + 后 3）
                phase = 3;
                cts.Cancel();
            }
            return Task.CompletedTask;
        };

        // Act（WaitAsync 只作兜底：本用例无截止期，卡住时至少能快速失败而不是挂住）
        await RunUntilCancelled(runner, entry, cts.Token).WaitAsync(TimeSpan.FromSeconds(15));

        // Assert
        Assert.True(mockStrategy.CallCount >= 6);

        // 前 3 次：连续累计 1, 2, 3
        Assert.Equal(1, mockStrategy.ReceivedCounts[0]);
        Assert.Equal(2, mockStrategy.ReceivedCounts[1]);
        Assert.Equal(3, mockStrategy.ReceivedCounts[2]);
        // 复位后再连续失败：重新从 1 累计 → 1, 2, 3...
        Assert.Equal(1, mockStrategy.ReceivedCounts[3]);
        Assert.Equal(2, mockStrategy.ReceivedCounts[4]);
        Assert.Equal(3, mockStrategy.ReceivedCounts[5]);
    }

    [Fact]
    public async Task StartAsync_WithRealTagGrp_RespectsIsEnabledOfChildren()
    {
        // 自动轮询按 IsEnabled 自顶向下短路：被禁用的组合既不采集也不刷写（脏标记保留），
        // 同层里使能的兄弟节点照常读写。
        // Arrange
        var channel = new FakedChannel(new TagChannelDescriptor { Name = "fake-channel" });
        var entry = new TagGrp(new TagGrpDescriptor { Name = "entry", IsEntry = true, ScanInterval = 10 }, channel);
        var disabled = new CountingCbnt("disabled", isEnabled: false) { IsDirty = true };
        var enabled = new CountingCbnt("enabled", isEnabled: true) { IsDirty = true };
        entry.AddTag(disabled);
        entry.AddTag(enabled);

        var project = new MockProject();
        var runner = new TagGrpRunner(project, NullLogger<TagGrpRunner>.Instance);
        using var cts = new CancellationTokenSource();

        runner.TurnProcess += (_, _) =>
        {
            cts.Cancel();
            return Task.CompletedTask;
        };

        // Act（WaitAsync 只作兜底）
        await RunUntilCancelled(runner, entry, cts.Token).WaitAsync(TimeSpan.FromSeconds(15));

        // Assert
        Assert.Equal(0, disabled.ReadCount);
        Assert.Equal(0, disabled.WriteCount);
        Assert.True(disabled.IsDirty, "被跳过的组合应保留脏标记，重新使能后再刷写");
        Assert.Equal(1, enabled.ReadCount);
        Assert.Equal(1, enabled.WriteCount);
    }

    #region Mocks

    /// <summary>
    /// 计数用的 <see cref="ITagCbnt"/>：不做任何 IO，只记录读/写次数，用于验证使能门控。
    /// </summary>
    private sealed class CountingCbnt : ITagCbnt
    {
        public CountingCbnt(string name, bool isEnabled)
        {
            Descriptor = new TagCbntDescriptor { Name = name, IsEnabled = isEnabled };
            IsEnabled = isEnabled;
        }

        public TagCbntDescriptor Descriptor { get; set; }
        public ITagGrp? Parent { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsScanned { get; set; }
        public ITagChannel? Channel { get; set; }
        public string StartAddress { get; set; } = string.Empty;
        public bool IsDirty { get; set; }

        private readonly Dictionary<string, ITagCbntor> _children = new();
        public IDictionary<string, ITagCbntor> Children => _children;
        public ITagCbntor this[string tagName] => _children[tagName];

        public int ReadCount { get; private set; }
        public int WriteCount { get; private set; }

        public Task ReadAsync(CancellationToken ct)
        {
            ReadCount++;
            IsScanned = true;
            return Task.CompletedTask;
        }

        public Task WriteAsync(CancellationToken ct)
        {
            WriteCount++;
            IsDirty = false;
            return Task.CompletedTask;
        }
    }

    private class MockTagGrp : ITagGrp
    {
        public MockTagGrp(TagGrpDescriptor descriptor)
        {
            Descriptor = descriptor;
        }

        public ITagGrp? Parent { get; set; }
        public bool IsEntry { get; } = true;
        public IDictionary<string, TagUnion> Children { get; } = new Dictionary<string, TagUnion>();
        public TagUnion this[string tagName] => throw new NotImplementedException();
        public TagUnion Descendant(string path) => throw new NotImplementedException();
        public ITagGrp AddTag(ITag tag) => this;
        public ITagGrp AddTag(ITagCbnt tagCbnt) => this;
        public ITagGrp AddTag(ITagGrp tagGrp) => this;
        private bool _isEnabled = true;

        /// <summary>
        /// 是否使能。轮询循环每轮外循环只读一次，且复位（<c>_consecutiveFailures = 0</c>）发生在
        /// "读到 false"那一轮的末尾——因此可以用 <see cref="OnIsEnabledRead"/> 做确定性同步，
        /// 替代"等固定时间猜复位已完成"。
        /// </summary>
        public bool IsEnabled
        {
            get
            {
                // 回调先于取值生效：回调里改写 IsEnabled 后，本次读取就返回新值。
                OnIsEnabledRead?.Invoke();
                return _isEnabled;
            }
            set => _isEnabled = value;
        }

        /// <summary>
        /// 读取 <see cref="IsEnabled"/> 时的回调（在取值前调用）。
        /// </summary>
        public Action? OnIsEnabledRead { get; set; }

        public TagGrpDescriptor Descriptor { get; set; }
        public ITagChannel? Channel { get; set; }

        public int ReadAsyncCallCount { get; private set; }
        public int WriteAsyncCallCount { get; private set; }

        /// <summary>
        /// 如果不为 null，则 ReadAsync 会抛出此异常。
        /// </summary>
        public Exception? ReadAsyncThrows { get; set; }
        public bool IsDirtyReturn { get; set; }
        public Action? OnRead { get; set; }
        public Action? OnWrite { get; set; }

        // 本 mock 不模拟子树，也不做门控（入口使能由 TagGrpRunner 外层循环把关），
        // 因此忽略 mode，只记录调用次数。
        public Task ReadAsync(TraversalMode mode, CancellationToken ct)
        {
            ReadAsyncCallCount++;
            OnRead?.Invoke();
            if (ReadAsyncThrows is not null)
                throw ReadAsyncThrows;
            return Task.CompletedTask;
        }

        public Task WriteAsync(TraversalMode mode, CancellationToken ct)
        {
            WriteAsyncCallCount++;
            OnWrite?.Invoke();
            return Task.CompletedTask;
        }

        public bool IsDirty() => IsDirtyReturn;
    }

    private class MockProject : ITagsProject
    {
        public IReadOnlyList<ITagChannel> Channels => Array.Empty<ITagChannel>();
        public IList<ILogicet> Logicets => new List<ILogicet>();
        public ITagGrp Tags => null!;
        public string? ProjectRoot { get; set; }
        public int IntentCapacity { get; set; } = 10;

        private readonly ConcurrentDictionary<string, Channel<IntentCompletion>> _channels = new();

        public ChannelReader<IntentCompletion>? GetIntentReader(string entry) =>
            _channels.TryGetValue(entry, out var ch) ? ch.Reader : null;

        public bool WriteIntent(string entry, TagGrpWriteIntent intent, out Task task)
        {
            var ch = _channels.GetOrAdd(entry, _ => CreateIntentChannel());
            var writer = ch.Writer;
            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var item = new IntentCompletion(intent, tcs);
            task = tcs.Task;
            var written = writer.TryWrite(item);
            if (!written)
                tcs.TrySetException(new Exception($"写入意图失败: {entry}"));
            return written;
        }

        public bool WriteIntent(string entry, TagGrpWriteIntent intent) =>
            WriteIntent(entry, intent, out _);

        private Channel<IntentCompletion> CreateIntentChannel()
        {
            var capacity = IntentCapacity > 0 ? IntentCapacity : 10;
            return Channel.CreateBounded<IntentCompletion>(new BoundedChannelOptions(capacity)
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait,
            });
        }

        public XElement? GetRootElement() => null;
        public void Initialize(string projRoot, XElement? root = null) { }
        public void Dispose() { }
        public Task RunAsync(CancellationToken ct) => Task.CompletedTask;


        public event RunnerStarted? RunnerStarted;
        public event RunnerCrashed? RunnerCrashed;
    }

    /// <summary>
    /// 模拟的 <see cref="ITagGrpRunnerRetryStrategy"/>，记录每次调用时传入的
    /// <c>consecutiveFailureCount</c> 并返回固定延迟。
    /// </summary>
    private class MockRetryStrategy : ITagGrpRunnerRetryStrategy
    {
        private readonly TimeSpan _delay;
        private readonly List<int> _receivedCounts = new();

        /// <summary> 
        /// c'tor。
        /// 指定返回的固定延迟值。
        /// </summary>
        public MockRetryStrategy(TimeSpan delay) => _delay = delay;

        public IReadOnlyList<int> ReceivedCounts => _receivedCounts;
        public int CallCount => _receivedCounts.Count;

        /// <summary>最近一次返回的延迟值，仅供断言辅助使用。</summary>
        public TimeSpan LastDelay { get; private set; }

        public TimeSpan GetDelay(int consecutiveFailureCount)
        {
            _receivedCounts.Add(consecutiveFailureCount);
            LastDelay = _delay;
            return _delay;
        }
    }

    /// <summary>
    /// 记录 <see cref="DisconnectAsync"/> / <see cref="EnsureConnectedAsync"/> 调用次数与收到的 token 是否已取消，
    /// 用于验证取消轮询时清理路径仍会断开通道，以及多通道下每个通道都会被建连。
    /// </summary>
    private sealed class RecordingChannel : ITagChannel
    {
        public RecordingChannel(TagChannelDescriptor descriptor) => Descriptor = descriptor;

        public TagChannelDescriptor Descriptor { get; }

        public int DisconnectAsyncCallCount { get; private set; }
        public int EnsureConnectedCallCount { get; private set; }
        public bool LastDisconnectTokenWasCancelled { get; private set; }

        /// <summary>
        /// 若不为 null，DisconnectAsync 会等待该 TCS——用于模拟"断开卡住"的场景，
        /// 验证清理路径会超时放弃等待而不是无限阻塞。
        /// </summary>
        public TaskCompletionSource<bool>? SlowDisconnect { get; set; }

        /// <summary>
        /// 若不为 null，DisconnectAsync 会抛出该异常——用于验证清理路径吞掉异常但会留痕。
        /// </summary>
        public Exception? DisconnectThrows { get; set; }

        public Task DisconnectAsync(CancellationToken ct)
        {
            DisconnectAsyncCallCount++;
            LastDisconnectTokenWasCancelled = ct.IsCancellationRequested;

            if (DisconnectThrows is not null)
            {
                throw DisconnectThrows;
            }

            return SlowDisconnect is not null ? SlowDisconnect.Task : Task.CompletedTask;
        }

        public void Dispose() { }

        public Task EnsureConnectedAsync(bool force, CancellationToken ct)
        {
            EnsureConnectedCallCount++;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 记录 <see cref="ITagGrpRunnerDisconnectStrategy.WaitDisconnectAsync"/> 的调用，
    /// 用于验证清理路径把断开等待委托给了注入的策略。
    /// </summary>
    private sealed class RecordingDisconnectStrategy : ITagGrpRunnerDisconnectStrategy
    {
        private readonly Action<ITagChannel?, Task?> _onInvoke;

        public RecordingDisconnectStrategy(Action<ITagChannel?, Task?> onInvoke) => _onInvoke = onInvoke;

        public int CallCount { get; private set; }

        public Task WaitDisconnectAsync(ITagChannel? channel, Task? disconnect)
        {
            CallCount++;
            _onInvoke(channel, disconnect);
            return Task.CompletedTask;
        }
    }

    #endregion
}
