using Microsoft.Extensions.Logging;

namespace StdUnit.Tags.OpcUaClient;

/// <summary>
/// 工厂类，用于创建 <see cref="OpcUaClientTagChannel"/> 实例
/// </summary>
public class OpcUaClientTagChannelFactory : ITagChannelFactory
{
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// c'tor
    /// </summary>
    public OpcUaClientTagChannelFactory(ILoggerFactory loggerFactory)
    {

        this._loggerFactory = loggerFactory;
    }

    private static IReadOnlyList<string> _drivers = new List<string>() { OpcUaClientNames.DriverName };

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
        var opcDescriptor = descriptor.ToOpcUaClientTagChannelDescriptor();
        var logger = _loggerFactory.CreateLogger<OpcUaClientTagChannel>();
        return new OpcUaClientTagChannel(opcDescriptor, logger);
    }
}
