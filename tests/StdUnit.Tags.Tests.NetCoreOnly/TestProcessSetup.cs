using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace StdUnit.Tags.Tests.NetCoreOnly;

/// <summary>
/// 进程级测试设置。<br/>
/// 本程序集里的 WebUi 测试同时跑着"真实轮询循环 + hub 的发送/心跳循环 + SSE 写循环 + 若干真实宿主"，
/// 一个用例就要占用 4~6 个线程池线程；而 CI runner 只有 4 个 vCPU，且运行 `dotnet test` 时可能还有别的 testhost 抢 CPU。
/// 线程池的<b>最小</b>线程数默认等于核数，超出的需求要按"爬坡"策略慢慢补线程——在 5s 级的等待窗口里就会表现为
/// "偶发超时"，与本库代码无关（2026-11 的 CI 上曾同时打挂 3 个互不相干的用例）。
/// <para>
/// 这里把最小线程数抬高：这些线程绝大多数时间在等待而不是占 CPU，不会拖慢 CI，
/// 但能消掉"续体排队"这类假失败。
/// </para>
/// </summary>
internal static class TestProcessSetup
{
    private const int MinThreads = 64;

    [ModuleInitializer]
    internal static void Initialize()
    {
        ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);
        if (workerThreads >= MinThreads && completionPortThreads >= MinThreads)
        {
            return;
        }

        ThreadPool.SetMinThreads(Math.Max(workerThreads, MinThreads), Math.Max(completionPortThreads, MinThreads));
    }
}
