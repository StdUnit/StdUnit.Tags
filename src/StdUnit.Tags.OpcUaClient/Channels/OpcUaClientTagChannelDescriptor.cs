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
    /// <returns></returns>
    private static OpcUaServerOpt ParseSreverOpt(XElement serverOptEle)
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
        var serverOpt = new OpcUaServerOpt()
        {
            DiscoveryUrl = discoveryUrl,
            UsePassword =
                bool.TryParse(usePasswordStr, out var usePassword) ?
                usePassword :
                false,
            UserName = username,
            Password = password,
        };
        return serverOpt;
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
            ParseSreverOpt(serverEle) :
            new OpcUaServerOpt();

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
            },
        };
        return res;
    }


}