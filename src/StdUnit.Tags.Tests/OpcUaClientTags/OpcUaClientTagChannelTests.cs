using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StdUnit.Tags.OpcUaClient;
using StdUnit.Tags.Tests.Fakes;
using Microsoft.Extensions.Logging;
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
        : this(new OpcUaClientTagChannelDescriptor() { Name = channelName }, sessionMock, NullLogger<OpcUaClientTagChannel>.Instance)
    {
    }

    public TestOpcUaChannel(string channelName, Mock<ISession> sessionMock, ILogger<OpcUaClientTagChannel> logger)
        : this(new OpcUaClientTagChannelDescriptor() { Name = channelName }, sessionMock, logger)
    {
    }

    public TestOpcUaChannel(
        OpcUaClientTagChannelDescriptor descriptor,
        Mock<ISession> sessionMock,
        ILogger<OpcUaClientTagChannel> logger)
        : base(descriptor, logger)
    {
        SessionMock = sessionMock;
    }

    public TestOpcUaChannel(
        OpcUaClientTagChannelDescriptor descriptor,
        Mock<ISession> sessionMock,
        ILogger<OpcUaClientTagChannel> logger,
        Func<ServiceResult?, DataValue?, bool> checkIsFailed)
        : base(descriptor, logger, checkIsFailed)
    {
        SessionMock = sessionMock;
    }

    protected override Task<ISession> CreateSessionAsync()
        => Task.FromResult(SessionMock.Object);

    /// <summary>
    /// 暴露受保护的 <c>PrepareOpcUaAppConfig()</c>，用于校验证书等选项真的被写进了底层配置。
    /// </summary>
    public ApplicationConfiguration ExposeAppConfig() => this.PrepareOpcUaAppConfig();
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

    #region 调用方的 CancellationToken

    [Fact]
    public async Task WriteAsync_ShouldPassCallerCancellationToken()
    {
        var sessionMock = CreateSessionMock();
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

        using var cts = new CancellationTokenSource();
        await channel.WriteAsync(new Dictionary<NodeId, DataValue> { [new NodeId("w", 1)] = new DataValue { Value = 1 } }, cts.Token);

        // 曾经写死 CancellationToken.None，取消信号传不到底层 → 写入会无视取消一直阻塞
        sessionMock.Verify(
            x => x.WriteAsync(null, It.IsAny<WriteValueCollection>(), cts.Token),
            Times.Once);
    }

    #endregion

    #region 证书选项与坏值留痕

    [Fact]
    public void SecurityOpt_DefaultsAreLooseAndWarn()
    {
        var logs = new CapturingLoggerProvider();
        using var factory = LoggerFactory.Create(b => b.AddProvider(logs));
        var channel = new TestOpcUaChannel("ch-loose", CreateSessionMock(), factory.CreateLogger<OpcUaClientTagChannel>());

        // 构造通道时就该提醒一次（ExposeAppConfig 会再跑一遍 PrepareOpcUaAppConfig，故先取快照）
        var warning = Assert.Single(logs.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("ch-loose", warning.Message);
        Assert.Contains("AutoAcceptUntrustedCertificates", warning.Message);
        Assert.Contains("RejectSHA1SignedCertificates", warning.Message);
        Assert.Contains("MinimumCertificateKeySize", warning.Message);

        var config = channel.ExposeAppConfig();

        // 默认值与旧版本的硬编码行为一致
        Assert.True(config.SecurityConfiguration.AutoAcceptUntrustedCertificates);
        Assert.False(config.SecurityConfiguration.RejectSHA1SignedCertificates);
        Assert.Equal((ushort)1024, config.SecurityConfiguration.MinimumCertificateKeySize);
    }

    [Fact]
    public void SecurityOpt_WhenTightened_AppliesAndDoesNotWarn()
    {
        var logs = new CapturingLoggerProvider();
        using var factory = LoggerFactory.Create(b => b.AddProvider(logs));
        var descriptor = new OpcUaClientTagChannelDescriptor
        {
            Name = "ch-tight",
            OpcUaTagChannelOpt = new OpcUaClientTagChannelOpt
            {
                SecurityOpt = new OpcUaSecurityOpt
                {
                    AutoAcceptUntrustedCertificates = false,
                    RejectSHA1SignedCertificates = true,
                    MinimumCertificateKeySize = 2048,
                },
            },
        };
        var channel = new TestOpcUaChannel(descriptor, CreateSessionMock(), factory.CreateLogger<OpcUaClientTagChannel>());

        var config = channel.ExposeAppConfig();

        Assert.False(config.SecurityConfiguration.AutoAcceptUntrustedCertificates);
        Assert.True(config.SecurityConfiguration.RejectSHA1SignedCertificates);
        Assert.Equal((ushort)2048, config.SecurityConfiguration.MinimumCertificateKeySize);
        Assert.Empty(logs.Entries);
    }

    [Fact]
    public async Task ReadAsync_WhenAnyNodeIsBad_ThrowsWithChannelNodeAndStatus()
    {
        var logs = new CapturingLoggerProvider();
        using var factory = LoggerFactory.Create(b => b.AddProvider(logs));
        var badNodeId = new NodeId("bad", 1);
        var goodNodeId = new NodeId("good", 1);
        var sessionMock = CreateSessionMock();
        sessionMock
            .Setup(x => x.ReadValuesAsync(It.IsAny<IList<NodeId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                new DataValueCollection
                {
                    new DataValue { Value = 1 },
                    new DataValue { StatusCode = StatusCodes.BadNodeIdUnknown },
                },
                new List<ServiceResult> { new ServiceResult(StatusCodes.Good), new ServiceResult(StatusCodes.BadNodeIdUnknown) }
            ));
        var channel = new TestOpcUaChannel("ch-bad", sessionMock, factory.CreateLogger<OpcUaClientTagChannel>());
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        // 坏点不是"跳过并打日志"，而是让这次读取整体失败：只有抛出去才能走
        // TagGrpRunner 的"崩溃 → 断开全部通道 → 重连"恢复路径，下游也才看得见
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => channel.ReadAsync(new[] { goodNodeId, badNodeId }, CancellationToken.None));

        Assert.Contains("ch-bad", ex.Message);
        Assert.Contains(badNodeId.ToString(), ex.Message);
        Assert.Contains(nameof(StatusCodes.BadNodeIdUnknown), ex.Message);
        // 好节点不该出现在失败消息里
        Assert.DoesNotContain(goodNodeId.ToString(), ex.Message);
    }

    [Fact]
    public async Task ReadAsync_WhenValueIsUncertain_DoesNotThrow()
    {
        var sessionMock = CreateSessionMock();
        sessionMock
            .Setup(x => x.ReadValuesAsync(It.IsAny<IList<NodeId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                new DataValueCollection { new DataValue { Value = 2.5f, StatusCode = StatusCodes.UncertainLastUsableValue } },
                new List<ServiceResult> { null! }
            ));
        var channel = new TestOpcUaChannel("ch-uncertain", sessionMock, NullLogger<OpcUaClientTagChannel>.Instance);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        // Uncertain 不是失败：值照原样采集，可信度由业务判断（要表达就另加一个测点）
        var (values, _) = await channel.ReadAsync(new[] { new NodeId("u", 1) }, CancellationToken.None);

        Assert.Equal(2.5f, values[0].Value);
    }

    [Fact]
    public async Task ReadValueAsync_WhenValueIsBad_ThrowsWithChannelNodeAndStatus()
    {
        var nodeId = new NodeId("bad", 1);
        var sessionMock = CreateSessionMock();
        sessionMock
            .Setup(x => x.ReadValueAsync(It.IsAny<NodeId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DataValue { StatusCode = StatusCodes.BadNotConnected });
        var channel = new TestOpcUaChannel("ch-single", sessionMock, NullLogger<OpcUaClientTagChannel>.Instance);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => channel.ReadValueAsync(nodeId, CancellationToken.None));

        Assert.Contains("ch-single", ex.Message);
        Assert.Contains(nodeId.ToString(), ex.Message);
        Assert.Contains(nameof(StatusCodes.BadNotConnected), ex.Message);
    }

    #endregion

    #region checkIsFailed 可替换（默认 = OpcUaValueQuality.IsFailed）

    private static Mock<ISession> CreateBadValueSessionMock()
    {
        var sessionMock = CreateSessionMock();
        sessionMock
            .Setup(x => x.ReadValuesAsync(It.IsAny<IList<NodeId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                new DataValueCollection { new DataValue { StatusCode = StatusCodes.BadNodeIdUnknown } },
                new List<ServiceResult> { new ServiceResult(StatusCodes.BadNodeIdUnknown) }
            ));
        return sessionMock;
    }

    [Fact]
    public async Task CheckIsFailed_WhenAlwaysFalse_DoesNotThrowEvenForBadValues()
    {
        var sessionMock = CreateBadValueSessionMock();
        var descriptor = new OpcUaClientTagChannelDescriptor { Name = "ch-permissive" };
        // "任何状态都照原样采集"：读取不再因质量失败（代价是 Bad 时 Value 可能为 null，由使用方承担）
        var channel = new TestOpcUaChannel(descriptor, sessionMock, NullLogger<OpcUaClientTagChannel>.Instance, (_, _) => false);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var (values, _) = await channel.ReadAsync(new[] { new NodeId("bad", 1) }, CancellationToken.None);

        Assert.Single(values);
    }

    [Fact]
    public async Task CheckIsFailed_WhenAlwaysTrue_ThrowsEvenForGoodValues()
    {
        var sessionMock = CreateSessionMock();
        sessionMock
            .Setup(x => x.ReadValuesAsync(It.IsAny<IList<NodeId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                new DataValueCollection { new DataValue { Value = 1 } },
                new List<ServiceResult> { new ServiceResult(StatusCodes.Good) }
            ));
        var descriptor = new OpcUaClientTagChannelDescriptor { Name = "ch-strict" };
        var channel = new TestOpcUaChannel(descriptor, sessionMock, NullLogger<OpcUaClientTagChannel>.Instance, (_, _) => true);
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => channel.ReadAsync(new[] { new NodeId("good", 1) }, CancellationToken.None));

        Assert.Contains("ch-strict", ex.Message);
    }

    [Fact]
    public async Task CheckIsFailed_CanTreatUncertainAsFailed()
    {
        var sessionMock = CreateSessionMock();
        sessionMock
            .Setup(x => x.ReadValuesAsync(It.IsAny<IList<NodeId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                new DataValueCollection { new DataValue { Value = 2.5f, StatusCode = StatusCodes.UncertainLastUsableValue } },
                new List<ServiceResult> { null! }
            ));
        var descriptor = new OpcUaClientTagChannelDescriptor { Name = "ch-uncertain-strict" };
        // 例：把 Uncertain 也算失败（自定义口径完全由调用方决定）
        var channel = new TestOpcUaChannel(
            descriptor,
            sessionMock,
            NullLogger<OpcUaClientTagChannel>.Instance,
            (err, value) => (err is not null && ServiceResult.IsBad(err))
                || (value is not null && (StatusCode.IsBad(value.StatusCode) || value.StatusCode.Code == StatusCodes.UncertainLastUsableValue)));
        await channel.EnsureConnectedAsync(false, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => channel.ReadAsync(new[] { new NodeId("u", 1) }, CancellationToken.None));

        Assert.Contains("ch-uncertain-strict", ex.Message);
        Assert.Contains(nameof(StatusCodes.UncertainLastUsableValue), ex.Message);
    }

    #endregion
}
