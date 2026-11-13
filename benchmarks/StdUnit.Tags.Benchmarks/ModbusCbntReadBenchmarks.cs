using System;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using StdUnit.Tags.ModbusTcp;

namespace StdUnit.Tags.Benchmarks;

/// <summary>
/// Modbus 组合式测点（<c>TagCbnt&lt;ushort&gt;</c>）每轮读的分配与耗时。<br/>
/// 它回答 backlog 里"cache 复用值不值得改公开接口"：对比"通道每轮返回新数组"（现状）与
/// "通道写入调用方预分配数组"（需要改 <see cref="IModbusRegisterChannel"/>）。<br/>
/// 用桩通道而不是真 socket：网络往返的噪声与分配量无关，而结论要的正是分配量。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class ModbusCbntReadBenchmarks
{
    /// <summary>每次读的寄存器数（125 是 FC03/FC04 的单帧上限）。</summary>
    [Params(32, 125)]
    public int RegisterCount { get; set; }

    /// <summary>true = 模拟"通道写入调用方数组"（cache 复用后的形态）。</summary>
    [Params(false, true)]
    public bool ReuseCache { get; set; }

    private ModbusRegisterTagCbnt _cbnt = null!;

    [GlobalSetup]
    public void Setup()
    {
        var channel = new StubChannel(ReuseCache);
        _cbnt = new ModbusRegisterTagCbnt(new TagCbntDescriptor
        {
            Name = "bench",
            StartAddress = "40001",
        })
        {
            Channel = channel,
        };
        _cbnt.ResizeCache(RegisterCount * sizeof(ushort));
    }

    [Benchmark]
    public Task ReadAsync() => _cbnt.ReadAsync(CancellationToken.None);

    private sealed class StubChannel : IModbusRegisterChannel
    {
        private readonly bool _reuse;
        private readonly ushort[] _buffer = new ushort[128];

        public StubChannel(bool reuse) => _reuse = reuse;

        public TagChannelDescriptor Descriptor { get; } = new() { Name = "bench", Driver = "ModbusTcp" };

        public Task EnsureConnectedAsync(bool force, CancellationToken ct) => Task.CompletedTask;

        public Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;

        public void Dispose()
        {
        }

        public Task<ushort[]> ReadRegistersAsync(string address, int registerCount, CancellationToken ct)
        {
            // 现状：每轮一个新数组（NModbus 的 ReadHoldingRegistersAsync 就是这个形状）。
            // 复用：把值写进同一块缓冲区再返回——这正是"改接口"之后能省下的那块分配。
            if (!_reuse)
            {
                return Task.FromResult(new ushort[registerCount]);
            }

            Array.Clear(_buffer, 0, registerCount);
            return Task.FromResult(_buffer);
        }

        public Task WriteRegistersAsync(string address, ReadOnlyMemory<ushort> registers, CancellationToken ct) => Task.CompletedTask;
    }
}
