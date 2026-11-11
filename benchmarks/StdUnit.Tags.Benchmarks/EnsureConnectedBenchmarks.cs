using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using StdUnit.Tags.ModbusTcp;

namespace StdUnit.Tags.Benchmarks;

/// <summary>
/// 每轮“我还活着吗”的成本：<see cref="ModbusTcpChannel.EnsureConnectedAsync"/> 在已连接时只做一次存活探测，
/// 而它的实现里有 <c>Socket.Poll(1000, SelectRead)</c>——链路空闲时会**阻塞满 1 ms** 才返回。<br/>
/// 这组基准把这条路径与“读 `Socket.Connected` 标志”“Poll(0) 快检”放在一起对比，
/// 用来判断是否值得改探测策略。真实网络里 PLC 的响应时间与此无关，这里只量框架自己的开销。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class EnsureConnectedBenchmarks
{
    private TcpListener _listener = null!;
    private Socket _serverSide = null!;
    private Socket _rawClient = null!;
    private ModbusTcpChannel _channel = null!;

    [GlobalSetup]
    public void Setup()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        // 一对裸 socket：用于对比 Poll(1000) 与 Connected 标志
        var accept1 = _listener.AcceptSocketAsync();
        _rawClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _rawClient.Connect(IPAddress.Loopback, port);
        _serverSide = accept1.GetAwaiter().GetResult();

        // 通道连到另一个监听端口（复用同一个 listener 再接受一次连接）
        var accept2 = _listener.AcceptSocketAsync();
        _channel = new ModbusTcpChannel(
            new ModbusTcpTagChannelDescriptor { Name = "bench", IpAddr = "127.0.0.1", Port = port },
            NullLogger<ModbusTcpChannel>.Instance);
        _channel.EnsureConnectedAsync(force: false, CancellationToken.None).GetAwaiter().GetResult();
        _ = accept2.GetAwaiter().GetResult();   // 保住服务端 socket，避免被 GC 关掉

        // 预热一次，确认 Connected 判定稳定
        _channel.EnsureConnectedAsync(force: false, CancellationToken.None).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _channel.Dispose();
        _rawClient.Dispose();
        _serverSide.Dispose();
        _listener.Stop();
    }

    /// <summary>热路径现状：每轮每通道调用一次（已连接时应为“无操作”）。</summary>
    [Benchmark(Baseline = true)]
    public Task EnsureConnected_AlreadyConnected()
        => _channel.EnsureConnectedAsync(force: false, CancellationToken.None);

    /// <summary>现状里真正花时间的那一步：空闲连接上最多阻塞 1000 微秒。</summary>
    [Benchmark]
    public bool Socket_Poll1000_SelectRead() => _rawClient.Poll(1000, SelectMode.SelectRead);

    /// <summary>替代方案 A：只读标志位（不产生系统调用级等待）。</summary>
    [Benchmark]
    public bool Socket_ConnectedFlag() => _rawClient.Connected;

    /// <summary>替代方案 B：非阻塞快检（Poll 0 + 有无数据）。</summary>
    [Benchmark]
    public bool Socket_Poll0_SelectRead() => _rawClient.Poll(0, SelectMode.SelectRead);
}
