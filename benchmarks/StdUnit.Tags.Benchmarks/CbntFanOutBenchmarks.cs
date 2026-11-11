using System;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using StdUnit.Tags.ModbusTcp;

namespace StdUnit.Tags.Benchmarks;

/// <summary>
/// "数千点位"的真实形态：<b>一次</b>硬件读（组合式测点）之后，把值分发给 N 个子测点。<br/>
/// 这与"N 个独立测点各读一次"完全不同——那是 N 次设备往返，现实中不会这么用；
/// 框架真正花时间的地方在遍历、通知与脏标记，也正是容易被写成 O(n²) 的地方。<br/>
/// 通道返回复用缓冲区（不分配），所以测到的就是分发成本；硬件读本身由 <see cref="ModbusCbntReadBenchmarks"/> 量。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class CbntFanOutBenchmarks
{
    /// <summary>组合下的子测点数（每个 Int16 占 1 个寄存器）。</summary>
    [Params(1_000, 5_000)]
    public int ChildCount { get; set; }

    private ModbusRegisterTagCbnt _cbnt = null!;

    [GlobalSetup]
    public void Setup()
    {
        var channel = new ReusingStubChannel();
        _cbnt = new ModbusRegisterTagCbnt(new TagCbntDescriptor
        {
            Name = "bench",
            StartAddress = "40001",
        })
        {
            Channel = channel,
        };
        _cbnt.ResizeCache(ChildCount * sizeof(ushort));

        for (var i = 0; i < ChildCount; i++)
        {
            var child = new ModbusRegisterInt16Cbntor(
                new TagDescriptor { TagName = $"t{i}" },
                _cbnt,
                tagOffset: i * sizeof(ushort),
                isReadOnly: false);
            // 组合子不会自己挂进 Children，必须显式登记（与 OpcUa 等驱动的做法一致）。
            _cbnt.Children.Add(child.TagName(), child);
        }

        // 构造方式写错（子节点没挂上）会让基准变成"测一个空组合"，直接失败比输出假数据好。
        if (_cbnt.Children.Count != ChildCount)
        {
            throw new InvalidOperationException($"预期挂上 {ChildCount} 个子测点，实际 {_cbnt.Children.Count}");
        }
    }

    [Benchmark]
    public Task ReadAsync() => _cbnt.ReadAsync(CancellationToken.None);

    private sealed class ReusingStubChannel : IModbusRegisterChannel    {
        private readonly ushort[] _buffer = new ushort[5_000];

        public TagChannelDescriptor Descriptor { get; } = new() { Name = "bench", Driver = "ModbusTcp" };

        public Task EnsureConnectedAsync(bool force, CancellationToken ct) => Task.CompletedTask;

        public Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;

        public void Dispose()
        {
        }

        public Task<ushort[]> ReadRegistersAsync(string address, int registerCount, CancellationToken ct)
            => Task.FromResult(_buffer);

        public Task WriteRegistersAsync(string address, ReadOnlyMemory<ushort> registers, CancellationToken ct) => Task.CompletedTask;
    }
}
