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

    /// <summary>
    /// 证书库根目录，默认 <c>null</c> ⇒ 使用 <see cref="Environment.SpecialFolder.CommonApplicationData"/>
    /// 下的 <c>OPC Foundation/CertificateStores</c>（Windows：<c>%ProgramData%\OPC Foundation\CertificateStores</c>；
    /// Linux：<c>/usr/share/OPC Foundation/CertificateStores</c>）。<br/>
    /// 四个库（<c>MachineDefault</c> / <c>UA Certificate Authorities</c> / <c>UA Applications</c> / <c>RejectedCertificates</c>）
    /// 都是本目录的子目录。<br/>
    /// <b>Linux 上注意</b>：默认位置通常需要 root 才能写入；以普通用户运行时请在此显式指定一个可写目录
    /// （例如 <c>/home/you/.local/share/OPC Foundation/CertificateStores</c>）。<br/>
    /// 取值按原样使用（不做分隔符或占位符替换），写相对路径时会相对进程当前目录。
    /// </summary>
    public string? CertificateStoreRoot { get; set; }
}
