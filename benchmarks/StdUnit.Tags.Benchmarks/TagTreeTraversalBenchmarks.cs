using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace StdUnit.Tags.Benchmarks;

/// <summary>
/// "框架自身"的每轮开销：一棵 N 个测点的树，叶子全是纯内存假测点、通道也是空实现，
/// 因此测到的只有遍历、使能/脏标记判断与派发——与硬件、网络无关。<br/>
/// 用途：给"每轮框架开销"一个数量级（例如"1 万点位的框架开销是几百 µs 还是几十 ms"），
/// 并作为回归基线；真实吞吐永远由设备与会话决定，不要用这组数字对外承诺端到端性能。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class TagTreeTraversalBenchmarks
{
    /// <summary>叶子测点总数（均分到 10 个子组里）。</summary>
    [Params(1_000, 10_000)]
    public int TagCount { get; set; }

    private TagGrp _root = null!;

    [GlobalSetup]
    public void Setup()
    {
        var channel = new BenchChannel();
        _root = new TagGrp(new TagGrpDescriptor { Name = "root" }, channel);

        const int groupCount = 10;
        var perGroup = TagCount / groupCount;
        for (var g = 0; g < groupCount; g++)
        {
            var group = new TagGrp(new TagGrpDescriptor { Name = $"g{g}" }, channel);
            for (var i = 0; i < perGroup; i++)
            {
                group.AddTag(new BenchLeafTag(new TagDescriptor { TagName = $"t{i}" }, channel, group));
            }

            _root.AddTag(group);
        }
    }

    /// <summary>
    /// 基准框架的自身开销基线：返回已完成 Task，不做任何事。<br/>
    /// `Allocated` 若与真实调用相同，说明那点分配来自"BDN 等待一个 Task 返回的测量装置"，不是被测代码。
    /// </summary>
    [Benchmark(Baseline = true)]
    public Task NoOp() => Task.CompletedTask;

    [Benchmark]
    public Task ReadAsync() => _root.ReadAsync(TraversalMode.IgnoreEnabled, CancellationToken.None);
}
