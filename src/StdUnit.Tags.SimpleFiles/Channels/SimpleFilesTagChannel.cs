
using Microsoft.Extensions.Logging;

namespace StdUnit.Tags.SimpleFiles;

/// <summary>
/// SimpleFiles通道实现
/// </summary>
public class SimpleFilesTagChannel : ITagChannel
{
    /// <summary>
    /// c'tor
    /// </summary>
    public SimpleFilesTagChannel(SimpleFilesTagChannelDescriptor descriptor, ILogger<SimpleFilesTagChannel> logger)
    {
        this.Descriptor = descriptor;
        this._logger = logger;
        this.Settings = new SimpleFilesSettings(descriptor.BaseDir);
    }

    private readonly ILogger<SimpleFilesTagChannel> _logger;

    /// <inheritdoc/>
    public TagChannelDescriptor Descriptor { get; set; }



    /// <summary>
    /// 通道设置
    /// </summary>
    public SimpleFilesSettings Settings { get; }


    /// <inheritdoc/>
    public virtual Task DisconnectAsync(CancellationToken ct)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task EnsureConnectedAsync(bool force, CancellationToken ct)
        => Task.CompletedTask;

    /// <summary>
    /// 获取文件路径
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    public string MakePath(string address)
    {
        var path = address;
        if (!string.IsNullOrEmpty(this.Settings.BaseDir))
        {
            path = Path.Combine(this.Settings.BaseDir, address);
        }
        return path;
    }

    /// <inheritdoc/>
    public virtual void Dispose()
    {
    }
}