using StdUnit.Tags.ModbusTcp;
using Microsoft.Extensions.Logging;

namespace StdUnit.Tags.Hjzk;



/// <summary>
/// 构建 <see cref="HjzkChannel"/>
/// </summary>
public class HjzkChannelFactory : ITagChannelFactory
{
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// c'tor
    /// </summary>
    public HjzkChannelFactory(ILoggerFactory loggerFactory)
    {

        this._loggerFactory = loggerFactory;
    }

    private static IReadOnlyList<string> _drivers = new List<string>() { HjzkNames.DriverName };

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
        var mbDescriptor = descriptor.ToHjzkTagChannelDescriptor();
        var logger = _loggerFactory.CreateLogger<HjzkChannel>();
        return new HjzkChannel(mbDescriptor, logger);
    }
}