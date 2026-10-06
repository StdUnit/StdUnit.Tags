namespace StdUnit.Tags.OpcUaClient;

/// <summary>
/// OpcUa 通道的证书校验选项。<br/>
/// 默认值与历史行为一致（既有现场升级后行为不变），但都偏宽松——启用宽松项时
/// <see cref="OpcUaClientTagChannel"/> 会在构造时输出警告。
/// </summary>
public class OpcUaSecurityOpt
{
    /// <summary>
    /// 是否自动接受不受信任的服务端证书。默认 <c>true</c>（宽松：不校验服务端证书是否受信任，无法防中间人）。<br/>
    /// 生产环境建议先把服务端证书装入受信任存储，再改为 <c>false</c>。
    /// </summary>
    public bool AutoAcceptUntrustedCertificates { get; set; } = true;

    /// <summary>
    /// 是否拒绝 SHA1 签名的证书。默认 <c>false</c>（宽松：SHA1 已被认为不安全）。
    /// </summary>
    public bool RejectSHA1SignedCertificates { get; set; } = false;

    /// <summary>
    /// 可接受的最小证书密钥长度。默认 <c>1024</c>（宽松：现代要求通常不低于 2048）。<br/>
    /// 类型与底层 <c>Opc.Ua.SecurityConfiguration.MinimumCertificateKeySize</c> 一致（<see cref="ushort"/>），避免隐式转换。
    /// </summary>
    public ushort MinimumCertificateKeySize { get; set; } = 1024;
}
