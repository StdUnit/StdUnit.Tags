using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace StdUnit.Tags.Benchmarks;

/// <summary>
/// 写遍历的框架成本，按"脏节点比例"分档（0% / 1% / 100%）。<br/>
/// 叶子是纯内存假测点：<c>WriteAsync</c> 不做任何设备 IO，所以量的是**框架的派发开销**
/// （遍历 + 每个节点的策略判断 + 调用写方法），不含真实设备的写入耗时。<br/>
/// 实现上不用 <c>[IterationSetup]</c> 重置脏标记（那会把运行策略切成 ColdStart，测出来的不是吞吐）：
/// 而是让"脏"叶子用一个永远保持脏的假测点，指标循环里状态恒定。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class TagTreeDirtyWriteBenchmarks
{
    /// <summary>叶子测点总数（均分到 10 个子组里）。</summary>
    [Params(1_000, 10_000)]
    public int TagCount { get; set; }

    /// <summary>"已脏"的叶子比例。</summary>
    [Params(0, 1, 100)]
    public int DirtyPercent { get; set; }

    private TagGrp _root = null!;

    [GlobalSetup]
    public void Setup()
    {
        var channel = new BenchChannel();
        _root = new TagGrp(new TagGrpDescriptor { Name = "root" }, channel);

        const int groupCount = 10;
        var perGroup = TagCount / groupCount;
        var dirtyPerGroup = perGroup * DirtyPercent / 100;

        for (var g = 0; g < groupCount; g++)
        {
            var group = new TagGrp(new TagGrpDescriptor { Name = $"g{g}" }, channel);
            for (var i = 0; i < perGroup; i++)
            {
                // 脏叶子：WriteAsync 不清脏，所以每轮都会真的被"写"一次；
                // 干净叶子：初始就干净，且不会变脏。
                group.AddTag(i < dirtyPerGroup
                    ? new AlwaysDirtyTag(new TagDescriptor { TagName = $"d{i}" }, channel, group)
                    : new BenchLeafTag(new TagDescriptor { TagName = $"c{i}" }, channel, group));
            }

            _root.AddTag(group);
        }
    }

    [Benchmark]
    public Task WriteAsync() => _root.WriteAsync(TraversalMode.IgnoreEnabled, CancellationToken.None);

    /// <summary>永远保持脏的叶子：写方法不改变脏标记，因此每轮都会走到"写"分支。</summary>
    private sealed class AlwaysDirtyTag : Tag<object, BenchChannel>
    {
        public AlwaysDirtyTag(TagDescriptor descriptor, BenchChannel channel, ITagGrp parent)
            : base(descriptor, channel, TagContainer.From(parent))
        {
            IsDirty = true;
        }

        public override Task ReadAsync(CancellationToken ct) => Task.CompletedTask;

        public override Task WriteAsync(CancellationToken ct) => Task.CompletedTask;
    }
}