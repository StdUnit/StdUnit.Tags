using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace StdUnit.Tags.Benchmarks;

/// <summary>
/// 把一个"每点位 ~38 ns"拆开：纯循环 / 访问模式查询 / 同步派发 / 全部组合 / 真实的 TagUnion 异步遍历。<br/>
/// 目的是回答"瓶颈在哪"——不是猜，而是逐层加回成本，看每层增加多少 ns。<br/>
/// 全部用扁平数组（10 000 个叶子），避免树形与字典开销混进来；叶子是纯内存假测点。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class TraversalBreakdownBenchmarks
{
    private const int NodeCount = 10_000;

    private ITag[] _tags = null!;
    private TagUnion[] _unions = null!;

    [GlobalSetup]
    public void Setup()
    {
        var channel = new BenchChannel();
        var root = new TagGrp(new TagGrpDescriptor { Name = "root" }, channel);
        _tags = new ITag[NodeCount];
        _unions = new TagUnion[NodeCount];
        for (var i = 0; i < NodeCount; i++)
        {
            var tag = new BenchLeafTag(new TagDescriptor { TagName = $"t{i}" }, channel, root);
            _tags[i] = tag;
            _unions[i] = new TagUnion.TagUnit(tag);
        }
    }

    /// <summary>基线：数组遍历本身（+ 一次空判断），不含任何框架调用。</summary>
    [Benchmark(Baseline = true)]
    public int Loop_Only()
    {
        var n = 0;
        foreach (var t in _tags)
        {
            if (t is not null)
            {
                n++;
            }
        }

        return n;
    }

    /// <summary>只加"访问模式解析"（每次读要做两次：R1W 判定 + IsWriteOnly）。</summary>
    [Benchmark]
    public int SearchAccessMode_Only()
    {
        var n = 0;
        foreach (var t in _tags)
        {
            n += (int)t.SearchAccessMode();
        }

        return n;
    }

    /// <summary>只加"派发 + 完成检查"（理想快路径：同步完成时不进 async 机制）。</summary>
    [Benchmark]
    public int SyncDispatch_Only()
    {
        var n = 0;
        foreach (var t in _tags)
        {
            if (t.ReadAsync(CancellationToken.None).IsCompletedSuccessfully)
            {
                n++;
            }
        }

        return n;
    }

    /// <summary>把上面几层按真实顺序组合起来——这就是"最优实现"能到的上界。</summary>
    [Benchmark]
    public int SyncDispatch_WithPolicy()
    {
        var n = 0;
        foreach (var t in _tags)
        {
            var mode = t.SearchAccessMode();
            if (mode == TagAccessMode.R1W && t.IsScanned)
            {
                continue;
            }

            if (mode == TagAccessMode.WO)
            {
                continue;
            }

            if (t.ReadAsync(CancellationToken.None).IsCompletedSuccessfully)
            {
                t.IsScanned = true;
                n++;
            }
        }

        return n;
    }

    /// <summary>现状：走 <see cref="TagUnionExtensions"/> 的异步遍历（每个节点一次 async 包装 + switch）。</summary>
    [Benchmark]
    public async Task UnionReadAsync()
    {
        foreach (var union in _unions)
        {
            await union.ReadAsync(TraversalMode.IgnoreEnabled, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 每轮开头运行器还会调一次 <c>entry.IsDirty()</c>：全树干净时要走完所有子节点。
    /// 这是"每轮固定成本"里除读之外的另一大块，所以单独量。
    /// </summary>
    [Benchmark]
    public int IsDirty_All()
    {
        var n = 0;
        foreach (var union in _unions)
        {
            if (union.IsDirty())
            {
                n++;
            }
        }

        return n;
    }
}
