using System.Threading;
using System.Threading.Tasks;
using StdUnit.Tags.ModbusTcp;
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
        : base(descriptor, NullLogger<ModbusTcpChannel>.Instance)
    {
        MasterMock = masterMock;
    }

    protected override Task<IModbusMaster> CreateConnectionAsync(int timeout, CancellationToken ct)
        => Task.FromResult(MasterMock.Object);
}
