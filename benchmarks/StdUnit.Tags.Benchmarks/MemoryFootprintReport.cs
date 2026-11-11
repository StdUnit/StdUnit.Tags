using System;
using StdUnit.Tags.ModbusTcp;

namespace StdUnit.Tags.Benchmarks;

/// <summary>
/// 常驻内存报告：点位数从 1k 增到 10k 时，<b>对象图</b>增长多少。<br/>
/// 这与 `[MemoryDiagnoser]` 的 `Allocated` 是两件事：后者是"每轮产生的垃圾"，前者是"进程里一直占着的东西"。
/// BenchmarkDotNet 不测后者的差值，所以这里单独跑一遍（<c>--footprint</c> 模式）。<br/>
/// 测量方式：构建对象图前后各做一次完整 GC + <see cref="GC.GetTotalMemory(bool)"/>，取差值。
/// </summary>
internal static class MemoryFootprintReport
{
    private static readonly int[] TagCounts = { 1_000, 10_000, 50_000 };

    public static void Run()
    {
        Console.WriteLine("常驻内存（对象图）报告 —— 构建前后各一次 full GC 后取 GC.GetTotalMemory(true) 的差值");
        Console.WriteLine();
        Console.WriteLine("| 形态 | 点位数 | 常驻增量 | 每点 |");
        Console.WriteLine("|---|---|---|---|");

        foreach (var count in TagCounts)
        {
            Report("树形（10 个子组）", count, () => BuildTree(count));
        }

        foreach (var count in TagCounts)
        {
            Report("组合式（1 个 Cbnt + N 子测点）", count, () => BuildCbnt(count));
        }
    }

    private static void Report(string shape, int count, Func<object> build)
    {
        // 先 GC 一次把前面迭代的残留清掉，再取基线。
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var before = GC.GetTotalMemory(forceFullCollection: true);

        var graph = build();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var after = GC.GetTotalMemory(forceFullCollection: true);

        GC.KeepAlive(graph);

        var delta = after - before;
        var perTag = (double)delta / count;
        Console.WriteLine($"| {shape} | {count:N0} | {ToMb(delta)} | {perTag:F0} B |");
    }

    private static string ToMb(long bytes) => $"{bytes / 1024.0 / 1024.0:F2} MB";

    private static object BuildTree(int tagCount)
    {
        var channel = new BenchChannel();
        var root = new TagGrp(new TagGrpDescriptor { Name = "root" }, channel);
        const int groupCount = 10;
        var perGroup = tagCount / groupCount;
        for (var g = 0; g < groupCount; g++)
        {
            var group = new TagGrp(new TagGrpDescriptor { Name = $"g{g}" }, channel);
            for (var i = 0; i < perGroup; i++)
            {
                group.AddTag(new BenchLeafTag(new TagDescriptor { TagName = $"t{i}" }, channel, group));
            }

            root.AddTag(group);
        }

        return root;
    }

    private static object BuildCbnt(int childCount)
    {
        var cbnt = new ModbusRegisterTagCbnt(new TagCbntDescriptor { Name = "bench", StartAddress = "40001" })
        {
            Channel = new NullModbusChannel(),
        };
        cbnt.ResizeCache(childCount * sizeof(ushort));

        for (var i = 0; i < childCount; i++)
        {
            var child = new ModbusRegisterInt16Cbntor(
                new TagDescriptor { TagName = $"t{i}" },
                cbnt,
                tagOffset: i * sizeof(ushort),
                isReadOnly: false);
            cbnt.Children.Add(child.TagName(), child);
        }

        return cbnt;
    }

    private sealed class NullModbusChannel : IModbusRegisterChannel
    {
        private static readonly ushort[] Buffer = new ushort[50_000];

        public TagChannelDescriptor Descriptor { get; } = new() { Name = "bench", Driver = "ModbusTcp" };

        public Task EnsureConnectedAsync(bool force, System.Threading.CancellationToken ct) => System.Threading.Tasks.Task.CompletedTask;

        public Task DisconnectAsync(System.Threading.CancellationToken ct) => System.Threading.Tasks.Task.CompletedTask;

        public void Dispose()
        {
        }

        public Task<ushort[]> ReadRegistersAsync(string address, int registerCount, System.Threading.CancellationToken ct)
            => System.Threading.Tasks.Task.FromResult(Buffer);

        public Task WriteRegistersAsync(string address, ReadOnlyMemory<ushort> registers, System.Threading.CancellationToken ct)
            => System.Threading.Tasks.Task.CompletedTask;
    }
}
