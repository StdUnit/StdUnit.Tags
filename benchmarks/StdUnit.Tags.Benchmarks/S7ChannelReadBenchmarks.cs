using System;
using System.Threading;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using StdUnit.Sharp7;
using StdUnit.Tags.S7;

namespace StdUnit.Tags.Benchmarks;

/// <summary>
/// S7 通道层读一块 DB 的开销（20 000 字节 ≈ 现场常见的整块采集）。<br/>
/// 用桩 <see cref="S7Client"/>（只把数据搬进缓冲区、立即返回 0），所以测到的是<b>我们这层</b>的
/// 地址解析、互斥体、Span 传递与错误码检查；真实耗时里 PLC 的响应时间占绝对多数，
/// 这组数字的用途是确认"我们不是瓶颈"，以及防止将来在这条路径上引入拷贝/分配。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class S7ChannelReadBenchmarks
{
    /// <summary>每次读的字节数。</summary>
    [Params(1_000, 20_000)]
    public int ByteCount { get; set; }

    private S7TagChannel _channel = null!;
    private byte[] _buffer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _channel = new S7TagChannel(
            new S7TagChannelDescriptor { Name = "bench", IpAddr = "127.0.0.1" },
            NullLogger<S7TagChannel>.Instance)
        {
            Client = new CopyingS7Client(),
        };
        _buffer = new byte[ByteCount];
    }

    [Benchmark]
    public void Read() => _channel.Read("DB1.100", _buffer, CancellationToken.None);

    /// <summary>真实 S7Client 的替代品：只做一次内存拷贝，用来隔离"我们这层"的成本。</summary>
    private sealed class CopyingS7Client : S7Client
    {
        public override int DBRead(int dbNumber, int start, int size, Span<byte> buffer) => 0;

        public override int MBRead(int start, int size, Span<byte> buffer) => 0;
    }
}
