using StdUnit.Tags.ModbusTcp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace StdUnit.Tags.Hjzk;

/// <summary>
/// Hjzk 通道描述符
/// </summary>
public class HjzkTagChannelDescriptor : ModbusTcpTagChannelDescriptor
{
    /// <summary>
    /// c'tor
    /// </summary>
    public HjzkTagChannelDescriptor()
    {
        this.Driver = HjzkNames.DriverName;
    }
}

/// <summary>
/// conversions between <see cref="TagChannelDescriptor"/> and <see cref="HjzkTagChannelDescriptor"/>
/// </summary>
public static class TagChannelDescriptor_S7Extensions
{
    /// <summary>
    /// 转成 <see cref="HjzkTagChannelDescriptor"/>
    /// </summary>
    /// <param name="descriptor"></param>
    /// <returns></returns>
    /// <exception cref="TagsProjectConfigurationException">当前描述符的驱动不是 <see cref="HjzkNames.DriverName"/></exception>
    /// <exception cref="TagsProjectXmlException">Port / MaxWriteRegisters 不合法</exception>
    public static HjzkTagChannelDescriptor ToHjzkTagChannelDescriptor(this TagChannelDescriptor descriptor)
    {
        if (descriptor.Driver != HjzkNames.DriverName)
        {
            throw new TagsProjectConfigurationException(
                $"通道驱动错误：期望 {HjzkNames.DriverName}，而当前为{descriptor.Driver}",
                $"Channel({descriptor.Name})");
        }
        if (descriptor is HjzkTagChannelDescriptor d)
        {
            return d;
        }
        var location = $"Channel({descriptor.Name})";
        var res = new HjzkTagChannelDescriptor
        {
            Name = descriptor.Name,
            Driver = descriptor.Driver,
            Extras = descriptor.Extras,
            IpAddr = !descriptor.Extras.TryGetValue(nameof(HjzkTagChannelDescriptor.IpAddr), out var ipAddr) ?
                "localhost" :
                ipAddr.Value,
            Port = !descriptor.Extras.TryGetValue(nameof(HjzkTagChannelDescriptor.Port), out var portEle) ?
                502 :
                int.TryParse(portEle.Value, out var port) ?
                    port :
                    throw new TagsProjectXmlException($"配置的端口号不是整数：{portEle.Value}", location),
            MaxWriteRegisters = !descriptor.Extras.TryGetValue(nameof(ModbusTcpTagChannelDescriptor.MaxWriteRegisters), out var batchEle) ?
                null :
                !ushort.TryParse(batchEle.Value, out var batch) ?
                    throw new TagsProjectXmlException($"MaxWriteRegisters 配置不是整数：{batchEle.Value}", location) :
                    batch == 0 ?
                        throw new TagsProjectXmlException($"MaxWriteRegisters 必须大于 0", location) :
                        batch > ModbusTcpChannel.MaxWriteRegistersPerPdu ?
                            throw new TagsProjectXmlException($"MaxWriteRegisters 配置({batch})超过协议上限({ModbusTcpChannel.MaxWriteRegistersPerPdu})", location) :
                            batch,
        };
        return res;
    }

}