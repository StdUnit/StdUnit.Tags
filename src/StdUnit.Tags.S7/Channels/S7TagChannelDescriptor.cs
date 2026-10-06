using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace StdUnit.Tags.S7;

/// <summary>
/// S7通道描述符
/// </summary>
public class S7TagChannelDescriptor : TagChannelDescriptor
{
    /// <summary>
    /// c'tor
    /// </summary>
    public S7TagChannelDescriptor()
    {
        this.Driver = S7Names.DriverName;
    }

    /// <summary>
    /// IP 地址，默认 localhost
    /// </summary>
    public string IpAddr { get; set; } = "localhost";
    /// <summary>
    /// Rack
    /// </summary>
    public short Rack { get; set; } = 0;
    /// <summary>
    /// Slot
    /// </summary>
    public short Slot { get; set; } = 1;

    /// <summary>
    /// 连接类型
    /// </summary>
    public ushort ConnectionType { get; set; } = 3;


    /// <inheritdoc/>
    public override XElement ToXElement()
    {
        var ele = base.ToXElement();

        ele.SetOrAddChild(nameof(IpAddr), this.IpAddr);
        ele.SetOrAddChild(nameof(Rack), this.Rack);
        ele.SetOrAddChild(nameof(Slot), this.Slot);
        ele.SetOrAddChild(nameof(ConnectionType), this.ConnectionType);
        return ele;
    }

}

/// <summary>
/// extensions for <see cref="TagChannelDescriptor"/>
/// </summary>
public static class TagChannelDescriptor_S7Extensions
{
    /// <summary>
    /// 把 <see cref="TagChannelDescriptor"/> 转换为 <see cref="S7TagChannelDescriptor"/>
    /// </summary>
    /// <param name="descriptor"></param>
    /// <returns></returns>
    /// <exception cref="TagsProjectConfigurationException">当前描述符的驱动不是 <see cref="S7Names.DriverName"/></exception>
    /// <exception cref="TagsProjectXmlException">Rack / Slot / ConnectionType 无法解析成整数</exception>
    public static S7TagChannelDescriptor ToS7TagChannelDescriptor(this TagChannelDescriptor descriptor)
    {
        if (descriptor.Driver != S7Names.DriverName)
        {
            throw new TagsProjectConfigurationException(
                $"通道驱动错误：期望 {S7Names.DriverName}，而当前为{descriptor.Driver}",
                $"Channel({descriptor.Name})");
        }
        if (descriptor is S7TagChannelDescriptor d)
        {
            return d;
        }
        var location = $"Channel({descriptor.Name})";
        var res = new S7TagChannelDescriptor
        {
            Name = descriptor.Name,
            Driver = descriptor.Driver,
            Extras = descriptor.Extras,

            IpAddr = !descriptor.Extras.TryGetValue(nameof(S7TagChannelDescriptor.IpAddr), out var ipAddr) ?
                "localhost" :
                ipAddr.Value,
            Rack = !descriptor.Extras.TryGetValue(nameof(S7TagChannelDescriptor.Rack), out var rackEle) ?
                (short)0 :
                short.TryParse(rackEle.Value, out var rack) ?
                    rack :
                    throw new TagsProjectXmlException($"配置的 Rack 无法解析成 short：{rackEle.Value}", location),
            Slot = !descriptor.Extras.TryGetValue(nameof(S7TagChannelDescriptor.Slot), out var slotEle) ?
                (short)1 :
                short.TryParse(slotEle.Value, out var slot) ?
                    slot :
                    throw new TagsProjectXmlException($"配置的 Slot 无法解析成 short：{slotEle.Value}", location),
            ConnectionType = !descriptor.Extras.TryGetValue(nameof(S7TagChannelDescriptor.ConnectionType), out var connTypeEle) ?
                (ushort)3 :
                ushort.TryParse(connTypeEle.Value, out var connType) ?
                    connType :
                    throw new TagsProjectXmlException($"配置的 ConnectionType 无法解析成 ushort：{connTypeEle.Value}", location),
        };
        return res;
    }


}