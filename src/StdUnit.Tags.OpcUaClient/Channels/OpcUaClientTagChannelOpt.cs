namespace StdUnit.Tags.OpcUaClient;

/// <summary>
/// OpcUa 通道选项
/// </summary>
public class OpcUaClientTagChannelOpt
{
    /// <summary>
    /// 客户端名称
    /// </summary>
    public string ClientName { get; set; } = "StdUnitTagsOpcUaClient";
    /// <summary>
    /// 服务端选项
    /// </summary>
    public OpcUaServerOpt ServerOpt { get; set; } = new();

    /// <summary>
    /// 证书校验选项
    /// </summary>
    public OpcUaSecurityOpt SecurityOpt { get; set; } = new();
}

/// <summary>
/// OpcUa 服务器选项
/// </summary>
public class OpcUaServerOpt
{
    /// <summary>
    /// DiscoveryUrl, e.g
    /// </summary>
    public string DiscoveryUrl { get; set; } = string.Empty;
    /// <summary>
    /// 使用密码？
    /// </summary>
    public bool UsePassword { get; set; } = true;
    /// <summary>
    /// 用户名
    /// </summary>
    public string UserName { get; set; } = string.Empty;
    /// <summary>
    /// 密码
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
