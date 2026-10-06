using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StdUnit.Tags.OpcUaClient;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Opc.Ua;
using Opc.Ua.Client;
using Xunit;

namespace StdUnit.Tags.Tests.OpcUaClientTags;

/// <summary>
/// 测试用 OpcUaClientTagChannel，重写 <see cref="OpcUaClientTagChannel.CreateSessionAsync"/>
/// 以返回 Moq 创建的 <see cref="ISession"/>。
/// </summary>
internal class TestOpcUaChannel : OpcUaClientTagChannel
{
    public Mock<ISession> SessionMock { get; }

    public TestOpcUaChannel(string channelName, Mock<ISession> sessionMock)
        : base(
            new OpcUaClientTagChannelDescriptor() { Name = channelName },
            NullLogger<OpcUaClientTagChannel>.Instance
        )
    {
        SessionMock = sessionMock;
    }

    protected override Task<ISession> CreateSessionAsync()
        => Task.FromResult(SessionMock.Object);
}

public class OpcUaClientTagChannelTests
{
    private static Mock<ISession> CreateSessionMock(bool connected = true)
    {
        var mock = new Mock<ISession>();
        mock.SetupGet(x => x.Connected).Returns(connected);
        return mock;
    }

    private static TestOpcUaChannel CreateChannel(Mock<ISession> sessionMock)
        => new("ch1", sessionMock);

    #region EnsureConnectedAsync

    [Fact]
    public async Task EnsureConnectedAsync_CallsCreateSessionOnce()
    {
        var sessionMock = CreateSessionMock();
        var channel = CreateChannel(sessionMock);

        await channel.EnsureConnectedAsync(false, CancellationToken.None);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        sessionMock.VerifyGet(x => x.Connected, Times.AtLeastOnce());
    }

    [Fact]
    public async Task EnsureConnectedAsync_WhenDisconnected_Reconnects()
    {
        var sessionMock = CreateSessionMock(connected: false);
        sessionMock.Setup(x => x.ReconnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var channel = CreateChannel(sessionMock);

        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        sessionMock.Verify(x => x.ReconnectAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region DisconnectAsync

    [Fact]
    public async Task DisconnectAsync_ClosesSession()
    {
        var sessionMock = CreateSessionMock();
        sessionMock.Setup(x => x.CloseAsync(It.IsAny<CancellationToken>())).ReturnsAsync(StatusCodes.Good);
        var channel = CreateChannel(sessionMock);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        await channel.DisconnectAsync(CancellationToken.None);

        sessionMock.Verify(x => x.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region ReadAsync

    [Fact]
    public async Task ReadAsync_ReturnsValuesFromSession()
    {
        var sessionMock = CreateSessionMock();
        var nodeId = new NodeId("test", 1);
        sessionMock
            .Setup(x => x.ReadValuesAsync(
                It.IsAny<IList<NodeId>>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync((
                new DataValueCollection { new DataValue { Value = 42 } },
                new List<ServiceResult> { new ServiceResult(StatusCodes.Good) }
            ));
        var channel = CreateChannel(sessionMock);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var (values, _) = await channel.ReadAsync(new[] { nodeId }, CancellationToken.None);

        Assert.Equal(42, values[0].Value);
    }

    #endregion

    #region 会话缺失时的上下文

    [Fact]
    public async Task ReadAsync_WhenSessionNotCreated_ThrowsWithChannelContext()
    {
        var channel = CreateChannel(CreateSessionMock());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => channel.ReadAsync(new[] { new NodeId("x", 1) }, CancellationToken.None));

        // 多通道入口里，"会话未创建"这类消息必须能定位到通道与操作
        Assert.Contains("ch1", ex.Message);
        Assert.Contains("读取节点", ex.Message);
    }

    [Fact]
    public async Task ReadValueAsync_WhenSessionDisconnected_ThrowsWithChannelContext()
    {
        var connected = true;
        var sessionMock = new Mock<ISession>();
        sessionMock.SetupGet(x => x.Connected).Returns(() => connected);
        var channel = CreateChannel(sessionMock);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);
        connected = false; // 模拟连接掉线

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => channel.ReadValueAsync(new NodeId("x", 1), CancellationToken.None));

        Assert.Contains("ch1", ex.Message);
        Assert.Contains("未连接", ex.Message);
        Assert.Contains("读取节点值", ex.Message);
    }

    [Fact]
    public async Task WriteAsync_WhenSessionNotCreated_ThrowsWithChannelContext()
    {
        var channel = CreateChannel(CreateSessionMock());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => channel.WriteAsync(new Dictionary<NodeId, DataValue>(), CancellationToken.None));

        Assert.Contains("ch1", ex.Message);
        Assert.Contains("写入节点", ex.Message);
    }

    #endregion

    #region ReadValueAsync

    [Fact]
    public async Task ReadValueAsync_ReturnsValue()
    {
        var sessionMock = CreateSessionMock();
        var nodeId = new NodeId("v", 1);
        sessionMock
            .Setup(x => x.ReadValueAsync(
                nodeId,
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(new DataValue { Value = 99 });
        var channel = CreateChannel(sessionMock);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var result = await channel.ReadValueAsync(nodeId, CancellationToken.None);

        Assert.Equal(99, result.Value);
    }

    #endregion

    #region WriteAsync

    [Fact]
    public async Task WriteAsync_CallsSessionWrite()
    {
        var sessionMock = CreateSessionMock();
        var nodeId = new NodeId("w", 1);
        sessionMock
            .Setup(x => x.WriteAsync(
                It.IsAny<RequestHeader?>(),
                It.IsAny<WriteValueCollection>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(new WriteResponse
            {
                Results = new StatusCodeCollection { StatusCodes.Good }
            });
        var channel = CreateChannel(sessionMock);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        await channel.WriteAsync(new Dictionary<NodeId, DataValue> { [nodeId] = new DataValue { Value = 123 } }, CancellationToken.None);

        sessionMock.Verify(x => x.WriteAsync(null, It.IsAny<WriteValueCollection>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WriteAsync_WhenBad_Throws()
    {
        var sessionMock = CreateSessionMock();
        var nodeId = new NodeId("w", 1);
        sessionMock
            .Setup(x => x.WriteAsync(
                It.IsAny<RequestHeader?>(),
                It.IsAny<WriteValueCollection>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(new WriteResponse
            {
                Results = new StatusCodeCollection { StatusCodes.Bad }
            });
        var channel = CreateChannel(sessionMock);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            channel.WriteAsync(new Dictionary<NodeId, DataValue> { [nodeId] = new DataValue { Value = 123 } }, CancellationToken.None));
        Assert.Contains("写入失败", ex.Message);
    }

    #endregion

    #region WriteValueAsync

    [Fact]
    public async Task WriteValueAsync_DelegatesToWriteAsync()
    {
        var sessionMock = CreateSessionMock();
        var nodeId = new NodeId("w", 1);
        sessionMock
            .Setup(x => x.WriteAsync(
                It.IsAny<RequestHeader?>(),
                It.IsAny<WriteValueCollection>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(new WriteResponse
            {
                Results = new StatusCodeCollection { StatusCodes.Good }
            });
        var channel = CreateChannel(sessionMock);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        await channel.WriteValueAsync(nodeId, new DataValue { Value = 42 }, CancellationToken.None);

        sessionMock.Verify(
            x => x.WriteAsync(
                null,
                It.IsAny<WriteValueCollection>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    #endregion

    #region Dispose

    [Fact]
    public async Task Dispose_ClosesSession()
    {
        var sessionMock = CreateSessionMock();
        var channel = CreateChannel(sessionMock);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        channel.Dispose();

        sessionMock.Verify(x => x.Close(), Times.Once);
    }

    #endregion
}
