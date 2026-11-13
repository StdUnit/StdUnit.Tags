using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using StdUnit.Tags.ModbusTcp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NModbus;

namespace StdUnit.Tags.Tests.ModbusTags;

/// <summary>
/// 测试用 ModbusTcpChannel，重写 <see cref="ModbusTcpChannel.CreateConnectionAsync"/>
/// 以返回 Moq 创建的 <see cref="IModbusMaster"/>。<br/>
/// <br/>
/// 通道测试、直接测点测试、组合读写测试都建立在它之上：绕过真实 socket，
/// 但地址解析、分批、字节序转换这些<b>上面的</b>逻辑全部真实执行。
/// </summary>
internal class TestModbusTcpChannel : ModbusTcpChannel
{
    public Mock<IModbusMaster> MasterMock { get; }

    public TestModbusTcpChannel(ModbusTcpTagChannelDescriptor descriptor, Mock<IModbusMaster> masterMock)
        : this(descriptor, masterMock, NullLogger<ModbusTcpChannel>.Instance)
    {
    }

    public TestModbusTcpChannel(ModbusTcpTagChannelDescriptor descriptor, Mock<IModbusMaster> masterMock, ILogger<ModbusTcpChannel> logger)
        : base(descriptor, logger)
    {
        MasterMock = masterMock;
    }

    /// <summary>
    /// 越过 <see cref="ModbusTcpChannel.CreateConnectionAsync"/> 直接塞一个底层 TCP 客户端，
    /// 用来构造"关闭连接时抛异常"这种真实 socket 上不好复现的现场。
    /// </summary>
    public TcpClient? TcpClient
    {
        get => _tcpClient;
        set => _tcpClient = value;
    }

    protected override Task<IModbusMaster> CreateConnectionAsync(int timeout, CancellationToken ct)
        => Task.FromResult(MasterMock.Object);
}
