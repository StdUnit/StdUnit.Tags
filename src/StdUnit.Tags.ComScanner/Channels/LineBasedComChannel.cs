using Microsoft.Extensions.Logging;
using System.IO.Ports;
using System.Net;

namespace StdUnit.Tags.ComScanner.Channels;

/// <summary>
/// 基于行的串口通道。每一次读取一行
/// </summary>
public class LineBasedComChannel : ComChannelBase<string>
{

    /// <summary>
    /// c'tor
    /// </summary>
    public LineBasedComChannel(ComChannelDescriptor descriptor, ILogger<LineBasedComChannel> logger)
        : base(descriptor, logger)
    {
        this.ReadEntireLine = descriptor.Option.ReadEntireLine;
    }

    /// <summary>
    /// 读取整行？
    /// </summary>
    public bool ReadEntireLine { get; set; } = true;

    /// <inheritdoc/>
    protected override Task<string?> ParseDataAsync(ISerialPortHandle sport, CancellationToken ct)
    {
        var str = this.ReadEntireLine ?
            sport.ReadLine() :
            sport.ReadExisting();
        return Task.FromResult(string.IsNullOrEmpty(str) ? null : str);
    }
}
