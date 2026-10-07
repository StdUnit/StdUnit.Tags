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
    /// <param name="descriptor"></param>
    /// <param name="logger"></param>
    /// <param name="checkIsFailed">
    /// 判定"某节点的读取是否算失败"的委托；不传则用内置口径 <see cref="OpcUaValueQuality.IsFailed"/>
    /// （状态码 <c>Bad</c> 才算失败，<c>Uncertain</c> 照原样采集）。<br/>
    /// 注册时可用 <c>AddOpcUaClientChannel(checkIsFailed: ...)</c> 替换它——例如"把 Uncertain 也算失败"，
    /// 或"任何状态都照原样采集"（那样读取永不因质量失败，但 <c>Value</c> 可能是 <c>null</c>，需自行承担）。
    /// </param>
    public OpcUaClientTagChannel(
        OpcUaClientTagChannelDescriptor descriptor,
        ILogger<OpcUaClientTagChannel> logger,
        Func<ServiceResult?, DataValue?, bool>? checkIsFailed = null)
    {
        _channelOpt = descriptor.OpcUaTagChannelOpt;
        Descriptor = descriptor;
        _logger = logger;
        _checkIsFailed = checkIsFailed ?? OpcUaValueQuality.IsFailed;
        _appConfig = PrepareOpcUaAppConfig();
    }

    /// <summary>
    /// 判定"某节点的读取是否算失败"，默认 <see cref="OpcUaValueQuality.IsFailed"/>
    /// </summary>
    private readonly Func<ServiceResult?, DataValue?, bool> _checkIsFailed;

    /// <summary>
    /// 准备OpcUa的 <see cref="ApplicationConfiguration"/>
    /// </summary>
    /// <returns></returns>
    protected virtual ApplicationConfiguration PrepareOpcUaAppConfig()
    {
        var securityOpt = _channelOpt.SecurityOpt ?? new OpcUaSecurityOpt();
        var storeRoot = GetCertificateStoreRoot(securityOpt);
        var config = new ApplicationConfiguration()
        {
            ApplicationName = "MyClient",
            ApplicationUri = Utils.Format(@"urn:{0}:MyClient", System.Net.Dns.GetHostName()),
            ApplicationType = ApplicationType.Client,
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier { StoreType = "Directory", StorePath = Path.Combine(storeRoot, "MachineDefault"), SubjectName = "MyClientSubjectName" },
                TrustedIssuerCertificates = new CertificateTrustList { StoreType = "Directory", StorePath = Path.Combine(storeRoot, "UA Certificate Authorities") },
                TrustedPeerCertificates = new CertificateTrustList { StoreType = "Directory", StorePath = Path.Combine(storeRoot, "UA Applications") },
                RejectedCertificateStore = new CertificateTrustList { StoreType = "Directory", StorePath = Path.Combine(storeRoot, "RejectedCertificates") },
                AutoAcceptUntrustedCertificates = securityOpt.AutoAcceptUntrustedCertificates,
                RejectSHA1SignedCertificates = securityOpt.RejectSHA1SignedCertificates,
                MinimumCertificateKeySize = securityOpt.MinimumCertificateKeySize,
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

        WarnOnLooseSecuritySettings(securityOpt);
        return config;
    }

    /// <summary>
    /// 证书库根目录：优先用 <see cref="OpcUaSecurityOpt.CertificateStoreRoot"/>，未配置时取
    /// <see cref="Environment.SpecialFolder.CommonApplicationData"/> 下的 <c>OPC Foundation/CertificateStores</c>。<br/>
    /// <br/>
    /// 为什么不再写 <c>@"%CommonApplicationData%\OPC Foundation\..."</c> 字面量：那是 OPC UA 栈的占位符，
    /// 而它的替换是**纯字符串拼接、不做分隔符规范化**（实测 1.5.374.126）。在 Linux 上会得到
    /// <c>/usr/share\OPC Foundation\CertificateStores\MachineDefault</c>——一个名字里带反斜杠的目录，
    /// 层级完全不对，随后 <c>Utils.GetAbsoluteFilePath</c> 直接抛 "File does not exist"。
    /// 这里用 <c>Path.Combine</c> 拼，两个平台都得到正确的目录层级。
    /// </summary>
    private static string GetCertificateStoreRoot(OpcUaSecurityOpt securityOpt)
    {
        // 注意：net472 的引用程序集没有可空标注，string.IsNullOrWhiteSpace 的后置条件在那边看不到（会误报 CS8603），
        // 所以这里用语言级判空。
        var configured = securityOpt.CertificateStoreRoot;
        if (configured is not null && configured.Trim().Length > 0)
        {
            return configured;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "OPC Foundation",
            "CertificateStores");
    }

    /// <summary>
    /// 宽松的证书校验设置（恰好也是默认值）会降低 TLS 信任强度，构造通道时提醒一次。
    /// </summary>
    private void WarnOnLooseSecuritySettings(OpcUaSecurityOpt securityOpt)
    {
        var loose = new List<string>();
        if (securityOpt.AutoAcceptUntrustedCertificates)
        {
            loose.Add($"{nameof(OpcUaSecurityOpt.AutoAcceptUntrustedCertificates)}=true");
        }
        if (!securityOpt.RejectSHA1SignedCertificates)
        {
            loose.Add($"{nameof(OpcUaSecurityOpt.RejectSHA1SignedCertificates)}=false");
        }
        if (securityOpt.MinimumCertificateKeySize < 2048)
        {
            loose.Add($"{nameof(OpcUaSecurityOpt.MinimumCertificateKeySize)}={securityOpt.MinimumCertificateKeySize}");
        }

        if (loose.Count == 0)
        {
            return;
        }

        _logger.LogWarning(
            "通道({ChannelName})使用偏宽松的证书校验设置：{settings}。这会降低对中间人攻击的防护，生产环境请在 <SecurityOpt> 中收紧（装入受信任证书、拒绝 SHA1、密钥长度不低于 2048）",
            this.ChannelName(),
            string.Join("；", loose));
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
        //    Timestamp = DateTime.UtcNow,
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
    /// <exception cref="InvalidOperationException">会话未创建/未连接，或某个节点返回 <c>Bad</c> 状态（任何坏点都会让这次读取失败）</exception>
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
        EnsureNoFailedValues("读取节点", nodeIds, values, errs);
        return (values, errs);
    }

    /// <summary>
    /// 只要有节点的读取失败就让这次读取失败（抛异常），不做"跳过坏点、保留上次好值"式的静默降级。<br/>
    /// 两条理由：(1) 坏点往往是通信/配置问题（<c>BadNotConnected</c>、<c>BadNodeIdUnknown</c>……），
    /// 只有抛出去才能走 <c>TagGrpRunner</c> 的"崩溃 → 断开全部通道 → 重连"恢复路径，静默跳过恰好把重连路径掐断了；
    /// (2) 静默跳过对下游毫无可见性（日志不是下游能用的接口），值会被无限期冻结而入口看起来还活着——
    /// 与 S7/Modbus 驱动"读到错误码就抛"的行为也不一致。<br/>
    /// "什么算失败"由 <see cref="_checkIsFailed"/> 决定，默认口径是不解释质量（只有 <c>Bad</c> 算失败，<c>Uncertain</c> 照原样采集）。
    /// </summary>
    private void EnsureNoFailedValues(string operation, IList<NodeId> nodeIds, DataValueCollection values, IList<ServiceResult> errs)
    {
        List<string>? bad = null;
        var count = Math.Min(nodeIds.Count, values.Count);
        for (int i = 0; i < count; i++)
        {
            var err = i < errs.Count ? errs[i] : null;
            if (_checkIsFailed(err, values[i]))
            {
                bad ??= new List<string>();
                bad.Add(OpcUaValueQuality.Describe(nodeIds[i], err, values[i]));
            }
        }

        if (bad is not null)
        {
            throw new InvalidOperationException(
                $"通道({this.ChannelName()}){operation}失败：{string.Join("；", bad)}");
        }
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
        var resp = await this.OpcSession.WriteAsync(null, writeValues, ct);
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
    /// <exception cref="InvalidOperationException">会话未创建/未连接，或该节点返回 <c>Bad</c> 状态</exception>
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
        if (_checkIsFailed(null, value))
        {
            throw new InvalidOperationException($"通道({this.ChannelName()})读取节点值失败：{OpcUaValueQuality.Describe(nodeId, null, value)}");
        }
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