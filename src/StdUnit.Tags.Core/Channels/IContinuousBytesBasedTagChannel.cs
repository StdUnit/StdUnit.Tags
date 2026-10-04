namespace StdUnit.Tags;

/// <summary>
/// 表示能以字节数组的方式连续读取的测点通道
/// </summary>
public interface IContinuousBytesBasedTagChannel : ITagChannel
{
    /// <summary>
    /// 读取底层硬件，返回一段字节数组表示所读取的结果。
    /// 并不是所有通道都支持这个接口，但是这里直接定义是为了简化测点组合的整体读取。
    /// </summary>
    /// <param name="address"></param>
    /// <param name="cbSize"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public Task<byte[]> ReadAsync(string address, int cbSize, CancellationToken ct);

    /// <summary>
    /// 写入字节数组
    /// </summary>
    /// <param name="address"></param>
    /// <param name="bytes"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public Task WriteAsync(string address, byte[] bytes, CancellationToken ct);
}
