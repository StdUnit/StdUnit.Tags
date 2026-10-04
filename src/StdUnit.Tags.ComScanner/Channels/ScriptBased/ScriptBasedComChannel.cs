using Microsoft.Extensions.Logging;

namespace StdUnit.Tags.ComScanner.Channels;


/// <summary>
/// 非泛型的基于脚本的串口通信通道，默认返回字符串类型的数据。
/// </summary>
public class ScriptBasedComChannel : ScriptBasedComChannel<string>
{
    /// <summary>
    /// c'tor
    /// </summary>
    public ScriptBasedComChannel(ComChannelDescriptor descriptor, ILogger<ComChannelBase<string>> logger)
        : base(descriptor, logger)
    {
    }
}