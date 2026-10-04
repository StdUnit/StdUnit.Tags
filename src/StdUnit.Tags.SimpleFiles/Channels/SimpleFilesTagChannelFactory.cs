using Microsoft.Extensions.Logging;

namespace StdUnit.Tags.SimpleFiles;


/// <summary>
/// 构建 <see cref="SimpleFilesTagChannel"/>
/// </summary>
internal class SimpleFilesTagChannelFactory : ITagChannelFactory
{
    private ILoggerFactory _loggerFactory;

    /// <summary>
    /// c'tor
    /// </summary>
    public SimpleFilesTagChannelFactory(ILoggerFactory loggerFactory)
    {

        this._loggerFactory = loggerFactory;
    }

    private static IReadOnlyList<string> _drivers = new List<string>() { SimpleFilesNames.DriverName };

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
        var sfDescriptor = descriptor.ToSimpleFilesTagChannelDescriptor();
        var logger = _loggerFactory.CreateLogger<SimpleFilesTagChannel>();
        return new SimpleFilesTagChannel(sfDescriptor, logger);
    }
}