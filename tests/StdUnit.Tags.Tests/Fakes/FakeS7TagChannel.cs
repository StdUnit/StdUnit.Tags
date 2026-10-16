using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StdUnit.Tags.S7;

namespace StdUnit.Tags.Tests;

/// <summary>
/// 测试用 S7 通道替身：读写都落在内存字节数组上，并记录读写足迹。<br/>
/// S7 的组合测点（<c>S7TagCbnt</c> / <c>S7TagCbntorBase</c>）只认 <see cref="S7TagChannel"/>，
/// 所以替身必须从它派生——这与 S7 直接测点测试里的做法一致。
/// </summary>
internal sealed class FakeS7TagChannel : S7TagChannel
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="name">通道名</param>
    public FakeS7TagChannel(string name = "fake-s7-channel")
        : base(
            new S7TagChannelDescriptor { Name = name },
            new LoggerFactory().CreateLogger<S7TagChannel>()
        )
    {
    }

    /// <summary>
    /// 读取时返回的数据，默认 4 字节（够各类 Cbntor 用例切片取用）
    /// </summary>
    public byte[] ReadData { get; set; } = new byte[] { 0x03, 0x00, 0x00, 0x00 };

    /// <summary>
    /// 上一次读取的地址
    /// </summary>
    public string? LastReadAddress { get; private set; }

    /// <summary>
    /// 上一次读取的字节数
    /// </summary>
    public int? LastReadCount { get; private set; }

    /// <summary>
    /// 上一次写入的地址
    /// </summary>
    public string? LastWrittenAddress { get; private set; }

    /// <summary>
    /// 上一次写入的字节
    /// </summary>
    public byte[]? LastWrittenBytes { get; private set; }

    /// <inheritdoc/>
    public override Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;

    /// <inheritdoc/>
    public override Task EnsureConnectedAsync(bool force, CancellationToken ct) => Task.CompletedTask;

    /// <inheritdoc/>
    public override void Read(string address, Span<byte> buffer, CancellationToken ct)
    {
        if (buffer.Length > this.ReadData.Length)
        {
            throw new InvalidOperationException($"Unexpected read length={buffer.Length}");
        }
        this.LastReadAddress = address;
        this.LastReadCount = buffer.Length;
        this.ReadData.AsSpan(0, buffer.Length).CopyTo(buffer);
    }

    /// <inheritdoc/>
    public override void Write(string address, Span<byte> buffer, CancellationToken ct)
    {
        this.LastWrittenAddress = address;
        this.LastWrittenBytes = buffer.ToArray();
    }
}
