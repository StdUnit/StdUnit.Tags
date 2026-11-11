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
    /// 关闭连接。<br/>
    /// 约定：<b>失败时抛出</b>——调用方是运行器的清理路径，会按通道名记 Warning，也不会阻断其它通道的清理；
    /// 而 <see cref="IDisposable.Dispose"/> <b>不抛</b>。即"显式方法报告失败，释放方法只做尽力清理并留痕"。
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