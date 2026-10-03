using System;
using System.Threading.Tasks;

namespace Itminus.Tags.Tests;

/// <summary>
/// 测试基础设施：把 .NET Core+ 才有的异步等待便利 API 补齐到 net472。<br/>
/// <br/>
/// net472 的 <see cref="Task"/> 没有 <c>WaitAsync(TimeSpan)</c>（.NET 6+ 才加入）。
/// 这里提供同签名的扩展方法，使测试代码在两个目标框架下写法一致、无需铺 <c>#if</c>。<br/>
/// net8.0 下实例方法优先于扩展方法，因此本类型在该框架下自然不会生效——两个框架跑的是同一份调用点，
/// net472 走本扩展，net8.0 走 BCL（有标准库就用标准）。<br/>
/// <br/>
/// 命名空间刻意放在 <c>Itminus.Tags.Tests</c>：测试类都在它的子命名空间下，
/// 按 C# 的父命名空间查找规则可直接看到本类型，无需 using。
/// </summary>
internal static class TaskCompatExtensions
{
    /// <summary>
    /// 在指定超时内等待任务完成；超时则抛 <see cref="TimeoutException"/>。
    /// 语义与 .NET 6+ 的 <c>Task.WaitAsync(TimeSpan)</c> 一致。
    /// </summary>
    internal static async Task WaitAsync(this Task task, TimeSpan timeout)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
        if (!ReferenceEquals(completed, task))
        {
            throw new TimeoutException($"等待操作在 {timeout} 内未完成。");
        }
        await task.ConfigureAwait(false);
    }

    /// <summary>
    /// 泛型版本，语义同 <see cref="WaitAsync(Task, TimeSpan)"/>。
    /// </summary>
    internal static async Task<T> WaitAsync<T>(this Task<T> task, TimeSpan timeout)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
        if (!ReferenceEquals(completed, task))
        {
            throw new TimeoutException($"等待操作在 {timeout} 内未完成。");
        }
        return await task.ConfigureAwait(false);
    }
}
