using Opc.Ua;
using System.Xml.Linq;

namespace StdUnit.Tags.OpcUaClient;


/// <summary>
/// OpcUa Client Tag Channel Descriptor
/// </summary>
public class OpcUaClientTagChannelDescriptor : TagChannelDescriptor
{
    /// <summary>
    /// c'tor
    /// </summary>
    public OpcUaClientTagChannelDescriptor()
    {
        this.Driver = OpcUaClientNames.DriverName;
    }

    /// <summary>
    /// 通道选项
    /// </summary>
    public OpcUaClientTagChannelOpt OpcUaTagChannelOpt { get; set; } = new();



    /// <summary>
    /// 转成 <see cref="XElement"/> 对象
    /// </summary>
    /// <returns></returns>
    public override XElement ToXElement()
    {
        var ele = base.ToXElement();

        ele.SetOrAddChild(nameof(OpcUaTagChannelOpt.ClientName), this.OpcUaTagChannelOpt.ClientName);


        var existingSvrOpt = ele.Element(nameof(OpcUaTagChannelOpt.ServerOpt));
        if (existingSvrOpt != null)
        {
            existingSvrOpt.Remove();
        }

        var serverEle = new XElement(nameof(OpcUaTagChannelOpt.ServerOpt));
        serverEle.SetOrAddChild(nameof(OpcUaServerOpt.DiscoveryUrl), this.OpcUaTagChannelOpt.ServerOpt.DiscoveryUrl);
        serverEle.SetOrAddChild(nameof(OpcUaServerOpt.UsePassword), this.OpcUaTagChannelOpt.ServerOpt.UsePassword);
        serverEle.SetOrAddChild(nameof(OpcUaServerOpt.UserName), this.OpcUaTagChannelOpt.ServerOpt.UserName);
        serverEle.SetOrAddChild(nameof(OpcUaServerOpt.Password), this.OpcUaTagChannelOpt.ServerOpt.Password);

        ele.Add(serverEle);

        var existingSecurityOpt = ele.Element(nameof(OpcUaTagChannelOpt.SecurityOpt));
        if (existingSecurityOpt != null)
        {
            existingSecurityOpt.Remove();
        }

        var securityEle = new XElement(nameof(OpcUaTagChannelOpt.SecurityOpt));
        securityEle.SetOrAddChild(nameof(OpcUaSecurityOpt.AutoAcceptUntrustedCertificates), this.OpcUaTagChannelOpt.SecurityOpt.AutoAcceptUntrustedCertificates);
        securityEle.SetOrAddChild(nameof(OpcUaSecurityOpt.RejectSHA1SignedCertificates), this.OpcUaTagChannelOpt.SecurityOpt.RejectSHA1SignedCertificates);
        securityEle.SetOrAddChild(nameof(OpcUaSecurityOpt.MinimumCertificateKeySize), this.OpcUaTagChannelOpt.SecurityOpt.MinimumCertificateKeySize);
        // 同理：net472 上 string.IsNullOrWhiteSpace 的后置条件不可见（CS8604），用语言级判空
        var storeRootToWrite = this.OpcUaTagChannelOpt.SecurityOpt.CertificateStoreRoot;
        if (storeRootToWrite is not null && storeRootToWrite.Trim().Length > 0)
        {
            securityEle.SetOrAddChild(nameof(OpcUaSecurityOpt.CertificateStoreRoot), storeRootToWrite);
        }

        ele.Add(securityEle);
        return ele;
    }
}

/// <summary>
/// conversions between <see cref="TagChannelDescriptor"/> and <see cref="OpcUaClientTagChannelDescriptor"/>
/// </summary>
public static class TagChannelDescriptor_OpcUaClientExtensions
{
    const string DefaultClientName = "StdUnitTagsOpcUaClient";

    /// <summary>
    /// 解析 <see cref="XElement"/> 对象为 <see cref="OpcUaServerOpt"/>
    /// </summary>
    /// <param name="serverOptEle"></param>
    /// <param name="channelName">仅用于错误消息的定位上下文</param>
    /// <returns></returns>
    /// <exception cref="TagsProjectXmlException">属性/元素存在但取值无法解析</exception>
    private static OpcUaServerOpt ParseSreverOpt(XElement serverOptEle, string? channelName)
    {
        var discoveryUrl =
            serverOptEle.Attribute(nameof(OpcUaServerOpt.DiscoveryUrl))?.Value ??
            serverOptEle.Element(nameof(OpcUaServerOpt.DiscoveryUrl))?.Value ??
            "localhost";
        var usePasswordStr =
            serverOptEle.Attribute(nameof(OpcUaServerOpt.UsePassword))?.Value ??
            serverOptEle.Element(nameof(OpcUaServerOpt.UsePassword))?.Value ??
            "false";
        var username =
            serverOptEle.Attribute(nameof(OpcUaServerOpt.UserName))?.Value ??
            serverOptEle.Element(nameof(OpcUaServerOpt.UserName))?.Value ??
            string.Empty;
        var password =
            serverOptEle.Attribute(nameof(OpcUaServerOpt.Password))?.Value ??
            serverOptEle.Element(nameof(OpcUaServerOpt.Password))?.Value ??
            string.Empty;
        if (!bool.TryParse(usePasswordStr, out var usePassword))
        {
            // 静默降级为 false 会把"配错了"伪装成"配对了"，加载期直接报出来
            throw new TagsProjectXmlException(
                $"通道配置的 {nameof(OpcUaServerOpt.UsePassword)} 无法解析为布尔值：{usePasswordStr}",
                $"Channel({channelName})");
        }

        var serverOpt = new OpcUaServerOpt()
        {
            DiscoveryUrl = discoveryUrl,
            UsePassword = usePassword,
            UserName = username,
            Password = password,
        };
        return serverOpt;
    }

    /// <summary>
    /// 解析 <see cref="XElement"/> 对象为 <see cref="OpcUaSecurityOpt"/>
    /// </summary>
    /// <param name="securityOptEle"></param>
    /// <param name="channelName">仅用于错误消息的定位上下文</param>
    /// <returns></returns>
    /// <exception cref="TagsProjectXmlException">属性/元素存在但取值无法解析</exception>
    private static OpcUaSecurityOpt ParseSecurityOpt(XElement securityOptEle, string? channelName)
    {
        var securityOpt = new OpcUaSecurityOpt();

        var autoAcceptStr =
            securityOptEle.Attribute(nameof(OpcUaSecurityOpt.AutoAcceptUntrustedCertificates))?.Value ??
            securityOptEle.Element(nameof(OpcUaSecurityOpt.AutoAcceptUntrustedCertificates))?.Value;
        if (autoAcceptStr is not null)
        {
            if (!bool.TryParse(autoAcceptStr, out var autoAccept))
            {
                throw new TagsProjectXmlException(
                    $"通道配置的 {nameof(OpcUaSecurityOpt.AutoAcceptUntrustedCertificates)} 无法解析为布尔值：{autoAcceptStr}",
                    $"Channel({channelName})");
            }
            securityOpt.AutoAcceptUntrustedCertificates = autoAccept;
        }

        var rejectSha1Str =
            securityOptEle.Attribute(nameof(OpcUaSecurityOpt.RejectSHA1SignedCertificates))?.Value ??
            securityOptEle.Element(nameof(OpcUaSecurityOpt.RejectSHA1SignedCertificates))?.Value;
        if (rejectSha1Str is not null)
        {
            if (!bool.TryParse(rejectSha1Str, out var rejectSha1))
            {
                throw new TagsProjectXmlException(
                    $"通道配置的 {nameof(OpcUaSecurityOpt.RejectSHA1SignedCertificates)} 无法解析为布尔值：{rejectSha1Str}",
                    $"Channel({channelName})");
            }
            securityOpt.RejectSHA1SignedCertificates = rejectSha1;
        }

        var minKeySizeStr =
            securityOptEle.Attribute(nameof(OpcUaSecurityOpt.MinimumCertificateKeySize))?.Value ??
            securityOptEle.Element(nameof(OpcUaSecurityOpt.MinimumCertificateKeySize))?.Value;
        if (minKeySizeStr is not null)
        {
            if (!ushort.TryParse(minKeySizeStr, out var minKeySize) || minKeySize == 0)
            {
                throw new TagsProjectXmlException(
                    $"通道配置的 {nameof(OpcUaSecurityOpt.MinimumCertificateKeySize)} 必须是 1~65535 的整数，当前为：{minKeySizeStr}",
                    $"Channel({channelName})");
            }
            securityOpt.MinimumCertificateKeySize = minKeySize;
        }

        var storeRoot =
            securityOptEle.Attribute(nameof(OpcUaSecurityOpt.CertificateStoreRoot))?.Value ??
            securityOptEle.Element(nameof(OpcUaSecurityOpt.CertificateStoreRoot))?.Value;
        // 空/空白视为未配置（走内置默认根），不算错误：XML 里常见的“占位但留空”不该让项目加载失败
        securityOpt.CertificateStoreRoot = string.IsNullOrWhiteSpace(storeRoot) ? null : storeRoot;

        return securityOpt;
    }

    /// <summary>
    /// 转成 <see cref="OpcUaClientTagChannelDescriptor"/> 对象
    /// </summary>
    /// <param name="descriptor"></param>
    /// <returns></returns>
    /// <exception cref="TagsProjectConfigurationException">当前描述符的驱动不是 <see cref="OpcUaClientNames.DriverName"/></exception>
    public static OpcUaClientTagChannelDescriptor ToOpcUaClientTagChannelDescriptor(this TagChannelDescriptor descriptor)
    {
        if (descriptor.Driver != OpcUaClientNames.DriverName)
        {
            throw new TagsProjectConfigurationException(
                $"通道驱动错误：期望 {OpcUaClientNames.DriverName}，而当前为{descriptor.Driver}",
                $"Channel({descriptor.Name})");
        }
        if (descriptor is OpcUaClientTagChannelDescriptor d)
        {
            return d;
        }

        var serverOpt = descriptor.Extras.TryGetValue(nameof(OpcUaClientTagChannelDescriptor.OpcUaTagChannelOpt.ServerOpt), out var serverEle) ?
            ParseSreverOpt(serverEle, descriptor.Name) :
            new OpcUaServerOpt();

        var securityOpt = descriptor.Extras.TryGetValue(nameof(OpcUaClientTagChannelDescriptor.OpcUaTagChannelOpt.SecurityOpt), out var securityEle) ?
            ParseSecurityOpt(securityEle, descriptor.Name) :
            new OpcUaSecurityOpt();

        var res = new OpcUaClientTagChannelDescriptor
        {
            Name = descriptor.Name,
            Driver = descriptor.Driver,
            Extras = descriptor.Extras,

            OpcUaTagChannelOpt = new OpcUaClientTagChannelOpt()
            {
                ClientName = !descriptor.Extras.TryGetValue(nameof(OpcUaClientTagChannelDescriptor.OpcUaTagChannelOpt.ClientName), out var clientName) ?
                        DefaultClientName :
                        clientName.Value ?? DefaultClientName,
                ServerOpt = serverOpt,
                SecurityOpt = securityOpt,
            },
        };
        return res;
    }


}