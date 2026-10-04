using Microsoft.Extensions.Logging;

namespace StdUnit.Tags.S7;


/// <summary>
/// 构建 <see cref="S7TagChannel"/>
/// </summary>
public class S7TagChannelFactory : ITagChannelFactory
{
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// c'tor
    /// </summary>
    public S7TagChannelFactory(ILoggerFactory loggerFactory)
    {
        this._loggerFactory = loggerFactory;
    }

    private static IReadOnlyList<string> _drivers = new List<string>() { S7Names.DriverName };

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public IReadOnlyList<string> GetAvailableDrivers() => _drivers;



    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    /// <param name="descriptor"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public ITagChannel Create(TagChannelDescriptor descriptor)
    {
        var s7ChannelDescriptor = descriptor.ToS7TagChannelDescriptor();
        var logger = _loggerFactory.CreateLogger<S7TagChannel>();
        return new S7TagChannel(s7ChannelDescriptor, logger);
    }
}