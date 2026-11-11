using System;
using System.Threading.Tasks;
using Xunit;

namespace StdUnit.Tags.Tests.Core.TagGrpRunners;

/// <summary>
/// <see cref="DefaultTagGrpRunnerDisconnectStrategy"/>：有限超时等待，且"已经失败的断开"必须让调用方看见。
/// </summary>
public class DefaultTagGrpRunnerDisconnectStrategyTests
{
    private static DefaultTagGrpRunnerDisconnectStrategy NewStrategy(TimeSpan? timeout = null) => new()
    {
        Timeout = timeout ?? TimeSpan.FromSeconds(5)
    };

    /// <summary>无需断开时立即返回</summary>
    [Fact]
    public async Task WaitDisconnectAsync_WhenDisconnectIsNull_Completes()
    {
        var strategy = NewStrategy();

        await strategy.WaitDisconnectAsync(null, null);
    }

    /// <summary>断开正常完成时透传完成状态</summary>
    [Fact]
    public async Task WaitDisconnectAsync_WhenDisconnectSucceeds_Completes()
    {
        var strategy = NewStrategy();

        await strategy.WaitDisconnectAsync(null, Task.CompletedTask);
    }

    /// <summary>
    /// 断开失败必须外抛：否则故障 Task 永远没人 await（UnobservedTaskException），
    /// 运行器也不会记下"连接没断干净"。
    /// </summary>
    [Fact]
    public async Task WaitDisconnectAsync_WhenDisconnectFaulted_Propagates()
    {
        var strategy = NewStrategy();
        var faulted = Task.FromException(new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => strategy.WaitDisconnectAsync(null, faulted));
    }

    /// <summary>断开迟迟不完成时按超时返回，不无限阻塞清理路径</summary>
    [Fact]
    public async Task WaitDisconnectAsync_WhenDisconnectHangs_ReturnsAfterTimeout()
    {
        var strategy = NewStrategy(TimeSpan.FromMilliseconds(20));
        var neverCompletes = new TaskCompletionSource<object?>().Task;

        await strategy.WaitDisconnectAsync(null, neverCompletes);

        Assert.False(neverCompletes.IsCompleted);
    }
}
