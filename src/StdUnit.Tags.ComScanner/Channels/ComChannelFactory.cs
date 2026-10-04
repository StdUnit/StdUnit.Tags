using Microsoft.Extensions.Logging;

namespace StdUnit.Tags.ComScanner.Channels;

internal class ComChannelFactory : ITagChannelFactory
{
    private readonly ILoggerFactory _loggerFactory;


    /// <summary>
    /// c'tor
    /// </summary>
    public ComChannelFactory(ILoggerFactory loggerFactory)
    {
        this._loggerFactory = loggerFactory;
    }

    /// <summary>
    /// 创建通道实例
    /// </summary>
    /// <param name="chDescriptor"></param>
    /// <returns></returns>
    public ITagChannel Create(TagChannelDescriptor chDescriptor)
    {
        var descriptor = chDescriptor.ToComChannelDescriptor();

        if (!string.IsNullOrWhiteSpace(descriptor.Option.ReadScript))
        {
            var scriptLogger = _loggerFactory.CreateLogger<ComChannelBase<string>>();
            return new ScriptBasedComChannel(descriptor, scriptLogger);
        }

        // 回退到默认的基于行的串口扫描器
        var lineLogger = _loggerFactory.CreateLogger<LineBasedComChannel>();
        var channel = new LineBasedComChannel(descriptor, lineLogger);
        return channel;
    }


    private static IReadOnlyList<string> _drivers = new List<string>() {
        ComDriverNames.DriverName,
    };


    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public IReadOnlyList<string> GetAvailableDrivers() => _drivers;
}