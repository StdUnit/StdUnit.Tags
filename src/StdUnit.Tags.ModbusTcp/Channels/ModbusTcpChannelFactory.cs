using Microsoft.Extensions.Logging;

namespace StdUnit.Tags.ModbusTcp;



/// <summary>
/// 构建 <see cref="ModbusTcpChannel"/>
/// </summary>
public class ModbusTcpChannelFactory : ITagChannelFactory
{
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// c'tor
    /// </summary>
    public ModbusTcpChannelFactory(ILoggerFactory loggerFactory)
    {

        this._loggerFactory = loggerFactory;
    }

    private static IReadOnlyList<string> _drivers = new List<string>() { ModbusTcpNames.DriverName };

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
        var mbDescriptor = descriptor.ToModbusTcpTagChannelDescriptor();
        var logger = _loggerFactory.CreateLogger<ModbusTcpChannel>();
        return new ModbusTcpChannel(mbDescriptor, logger);
    }
}