namespace StdUnit.Tags;

/// <summary>
/// 采集通道
/// </summary>
public interface ITagChannel : IDisposable
{
    /// <summary>
    /// 通道描述符
    /// </summary>
    TagChannelDescriptor Descriptor { get; }

    /// <summary>
    /// 连接
    /// </summary>
    /// <returns></returns>
    Task EnsureConnectedAsync(bool force, CancellationToken ct);

    /// <summary>
    /// 关闭
    /// </summary>
    /// <returns></returns>
    Task DisconnectAsync(CancellationToken ct);

}

/// <summary>
/// <see cref="ITagChannel"/> 扩展方法
/// </summary>
public static class ITagChannelExtensions
{

    /// <summary>
    /// 通道名称
    /// </summary>
    /// <param name="channel"></param>
    /// <returns></returns>
    public static string ChannelName(this ITagChannel channel) => channel.Descriptor.Name;

    /// <summary>
    /// 获取通道的驱动名称
    /// </summary>
    /// <param name="channel"></param>
    /// <returns></returns>
    public static string Driver(this ITagChannel channel) => channel.Descriptor.Driver;
}