using BenchmarkDotNet.Running;

namespace StdUnit.Tags.Benchmarks;

/// <summary>
/// 基准入口。<br/>
/// 本地跑：<c>dotnet run -c Release --project benchmarks/StdUnit.Tags.Benchmarks -- --filter *</c><br/>
/// 只做冒烟（每个基准执行一次，秒级）：追加 <c>--job dry</c>。<br/>
/// 常驻内存报告（对象图大小，BDN 不测这个）：<c>-- --footprint</c>。<br/>
/// 基准<b>不</b>进 CI 的测试步骤：完整测量需要几分钟且依赖机器状态，只作为改动前后的对比工具。
/// </summary>
public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--footprint")
        {
            MemoryFootprintReport.Run();
            return;
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
