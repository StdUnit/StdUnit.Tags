using Microsoft.Extensions.Logging;
using Opc.Ua;

namespace StdUnit.Tags.OpcUaClient;

/// <summary>
/// 工厂类，用于创建 <see cref="OpcUaClientTagChannel"/> 实例
/// </summary>
public class OpcUaClientTagChannelFactory : ITagChannelFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly Func<ServiceResult?, DataValue?, bool>? _checkIsFailed;

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="loggerFactory"></param>
    /// <param name="checkIsFailed">
    /// 判定"某节点的读取是否算失败"的委托；不传则由通道使用内置口径 <see cref="OpcUaValueQuality.IsFailed"/>。
    /// </param>
    public OpcUaClientTagChannelFactory(
        ILoggerFactory loggerFactory,
        Func<ServiceResult?, DataValue?, bool>? checkIsFailed = null)
    {

        this._loggerFactory = loggerFactory;
        this._checkIsFailed = checkIsFailed;
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
        return new OpcUaClientTagChannel(opcDescriptor, logger, _checkIsFailed);
    }
}
