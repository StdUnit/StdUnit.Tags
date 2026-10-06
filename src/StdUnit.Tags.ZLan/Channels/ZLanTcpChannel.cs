using StdUnit.Tags.ModbusTcp;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StdUnit.Tags.ZLan;

/// <summary>
/// ZLan 通道：与 ZLan 远程 IO 模块的 Modbus-TCP 连接。
/// </summary>
public class ZLanTcpChannel : ModbusTcpChannel
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="descriptor">通道描述符（IP、端口等）</param>
    /// <param name="logger">日志</param>
    public ZLanTcpChannel(ZLanTcpTagChannelDescriptor descriptor, ILogger<ModbusTcpChannel> logger)
        : base(descriptor, logger)
    {
    }
}
