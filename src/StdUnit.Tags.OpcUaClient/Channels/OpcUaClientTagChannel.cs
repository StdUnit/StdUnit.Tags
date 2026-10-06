using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;

namespace StdUnit.Tags.OpcUaClient;

/// <summary>
/// OpcUa 通道实现
/// </summary>
public class OpcUaClientTagChannel : ITagChannel
{
    private readonly ILogger<OpcUaClientTagChannel> _logger;
    private readonly ApplicationConfiguration _appConfig;

    #region 配置
    private readonly OpcUaClientTagChannelOpt _channelOpt;

    /// <summary>
    /// 通道名称
    /// </summary>
    public string ClientName => _channelOpt.ClientName;
    /// <summary>
    /// 服务器选项
    /// </summary>
    public OpcUaServerOpt ServerOpt => _channelOpt.ServerOpt;
    #endregion

    /// <summary>
    /// 通道描述符
    /// </summary>
    public TagChannelDescriptor Descriptor { get; }

    /// <summary>
    /// c'tor
    /// </summary>
    public OpcUaClientTagChannel(OpcUaClientTagChannelDescriptor descriptor, ILogger<OpcUaClientTagChannel> logger)
    {
        _channelOpt = descriptor.OpcUaTagChannelOpt;
        Descriptor = descriptor;
        _logger = logger;
        _appConfig = PrepareOpcUaAppConfig();
    }

    /// <summary>
    /// 准备OpcUa的 <see cref="ApplicationConfiguration"/>
    /// </summary>
    /// <returns></returns>
    protected virtual ApplicationConfiguration PrepareOpcUaAppConfig()
    {
        var config = new ApplicationConfiguration()
        {
            ApplicationName = "MyClient",
            ApplicationUri = Utils.Format(@"urn:{0}:MyClient", System.Net.Dns.GetHostName()),
            ApplicationType = ApplicationType.Client,
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\MachineDefault", SubjectName = "MyClientSubjectName" },
                TrustedIssuerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Certificate Authorities" },
                TrustedPeerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Applications" },
                RejectedCertificateStore = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\RejectedCertificates" },
                AutoAcceptUntrustedCertificates = true,
                RejectSHA1SignedCertificates = false,
                MinimumCertificateKeySize = 1024,
                NonceLength = 32,
            },
            TransportConfigurations = new TransportConfigurationCollection(),
            TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
            ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 },
            TraceConfiguration = new TraceConfiguration()
        };


        // 设置证书验证事件，用于自动接受不受信任的证书
        if (config.SecurityConfiguration.AutoAcceptUntrustedCertificates)
        {
            config.CertificateValidator.CertificateValidation += (s, e) => { e.Accept = e.Error.StatusCode == StatusCodes.BadCertificateUntrusted; };
        }

        return config;
    }

    /// <summary>
    /// 创建一个新的 OPC UA 会话对象
    /// </summary>
    /// <returns></returns>
    protected virtual async Task<ISession> CreateSessionAsync()
    {
        // 验证应用配置对象
        await _appConfig.Validate(ApplicationType.Client);
        var _opcServerOpt = _channelOpt.ServerOpt;
        // 创建一个会话对象，用于连接到 OPC UA 服务器
        EndpointDescription epDescription = CoreClientUtils.SelectEndpoint(this._appConfig, _opcServerOpt.DiscoveryUrl, true);
        EndpointConfiguration epConfiguration = EndpointConfiguration.Create(_appConfig);
        ConfiguredEndpoint endpoint = new(null, epDescription, epConfiguration);
        var iden =
            _opcServerOpt.UsePassword ?
            new UserIdentity(_opcServerOpt.UserName, _opcServerOpt.Password) :
            new UserIdentity();
        Session session = await Session.Create(_appConfig, endpoint, false, false, "DataCollector", 60000, iden, null);
        this._logger.LogInformation("OPC UA 连接成功：{url}", _opcServerOpt.DiscoveryUrl);
        //await _mediator.Publish(new UILogNotification(new LogMessage()
        //{
        //    EventSource = "OpcUa",
        //    EventGroup = "OpcUa",
        //    Content = $"OPC UA 连接成功：{this._opcServerOpt.DiscoveryUrl}",
        //    Level = LogLevel.Information,
        //    Timestamp = DateTime.Now,
        //}));
        return session;
    }

    #region 连接
    /// <summary>
    /// 内部的 OPC UA 会话对象
    /// </summary>
    protected virtual ISession? OpcSession { get; set; }

    /// <summary>
    /// 确保已经建立连接
    /// </summary>
    /// <returns></returns>
    public async Task EnsureConnectedAsync(bool force, CancellationToken ct)
    {
        OpcSession ??= await CreateSessionAsync();

        if (!OpcSession.Connected)
        {
            await OpcSession.ReconnectAsync(ct);
        }
    }

    /// <summary>
    /// 断开连接
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public async Task DisconnectAsync(CancellationToken ct)
    {
        try
        {
            if (this.OpcSession != null)
            {
                await OpcSession.CloseAsync(ct);
            }
        }
        finally
        {
            this.OpcSession = null;
        }

    }
    #endregion


    #region

    /// <summary>
    /// 读取指定节点的值
    /// </summary>
    /// <param name="nodeIds"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public virtual async Task<(DataValueCollection values, IList<ServiceResult> errs)> ReadAsync(IList<NodeId> nodeIds, CancellationToken ct)
    {
        if (this.OpcSession is null)
        {
            throw new InvalidOperationException($"通道({this.ChannelName()}) 的OPC UA会话未创建，无法读取节点");
        }
        if (this.OpcSession.Connected == false)
        {
            throw new InvalidOperationException($"通道({this.ChannelName()}) 的OPC UA会话未连接，无法读取节点");
        }

        var (values, errs) = await this.OpcSession.ReadValuesAsync(nodeIds, ct);
        return (values, errs);
    }

    /// <summary>
    /// 写入指定节点的值
    /// </summary>
    /// <param name="toBeWritten"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">会话未创建/未连接，或 OPC UA 返回了坏状态码</exception>
    public virtual async Task WriteAsync(IDictionary<NodeId, DataValue> toBeWritten, CancellationToken ct)
    {
        if (this.OpcSession is null)
        {
            throw new InvalidOperationException($"通道({this.ChannelName()}) 的OPC UA会话未创建，无法写入节点");
        }
        if (this.OpcSession.Connected == false)
        {
            throw new InvalidOperationException($"通道({this.ChannelName()}) 的OPC UA会话未连接，无法写入节点");
        }

        var writeValues = new WriteValueCollection();
        foreach (var kvp in toBeWritten)
        {
            // 更新要批量写入的buffer
            var nv = kvp.Value;
            var wv = new WriteValue()
            {
                NodeId = kvp.Key,
                AttributeId = Attributes.Value,
                Value = nv,
            };
            writeValues.Add(wv);
        }

        // 远程写入
        var resp = await this.OpcSession.WriteAsync(null, writeValues, CancellationToken.None);
        ClientBase.ValidateResponse(resp.Results, writeValues);
        ClientBase.ValidateDiagnosticInfos(resp.DiagnosticInfos, writeValues);

        var isNotAllGood = resp.Results.Any(r => StatusCode.IsNotGood(r));
        if (isNotAllGood)
        {
            var notgoods = resp.Results.Zip(writeValues, (status, wv) => new { Status = status, WriteValue = wv })
                .Where(r => StatusCode.IsNotGood(r.Status))
                .Select(r => new WriteValueErr(r.WriteValue.NodeId, r.Status))
                .ToList();
            throw new InvalidOperationException($"通道写入失败:通道={this.ChannelName()}。异常={string.Join(";", notgoods)}。");
        }
    }

    /// <summary>
    /// 读取指定节点的值
    /// </summary>
    /// <param name="nodeId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public virtual async Task<DataValue> ReadValueAsync(NodeId nodeId, CancellationToken ct)
    {
        if (this.OpcSession is null)
        {
            throw new InvalidOperationException($"通道({this.ChannelName()}) 的OPC UA会话未创建，无法读取节点值：{nodeId}");
        }
        if (this.OpcSession.Connected == false)
        {
            throw new InvalidOperationException($"通道({this.ChannelName()}) 的OPC UA会话未连接，无法读取节点值：{nodeId}");
        }

        var value = await this.OpcSession.ReadValueAsync(nodeId, ct);
        return value;
    }

    /// <summary>
    /// 写入指定节点的值
    /// </summary>
    /// <param name="nodeId"></param>
    /// <param name="value"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public virtual async Task WriteValueAsync(NodeId nodeId, DataValue value, CancellationToken ct)
    {
        var toBeWritten = new Dictionary<NodeId, DataValue>
        {
            [nodeId] = value
        };
        await this.WriteAsync(toBeWritten, ct);
    }
    #endregion

    /// <inheritdoc/>
    public void Dispose()
    {
        if (OpcSession != null && OpcSession.Connected)
        {
            var channelName = this.ChannelName();
            try
            {
                _logger.LogInformation("通道={ChannelName} 正在断开连接...", channelName);
                OpcSession.Close();
                _logger.LogInformation("通道={ChannelName}  断开连接完成!", channelName);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "通道={ChannelName} 释放异常", channelName);
            }
            finally
            {

            }
        }
    }
}

/// <summary>
/// 写入值错误
/// </summary>
/// <param name="NodeId"></param>
/// <param name="StatusCode"></param>
public record WriteValueErr(NodeId NodeId, StatusCode StatusCode)
{
    /// <summary>
    /// 转成字符串表示
    /// </summary>
    /// <param name="erritems"></param>
    /// <returns></returns>
    public static string ErrsToMsg(IList<WriteValueErr> erritems)
    {
        return string.Join("。", erritems.Select(e => $"{e.NodeId}={e.StatusCode}"));
    }
}