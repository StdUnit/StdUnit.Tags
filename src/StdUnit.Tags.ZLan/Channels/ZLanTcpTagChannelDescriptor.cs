using StdUnit.Tags.ModbusTcp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace StdUnit.Tags.ZLan;


/// <summary>
/// ZLan 通道描述符：在 <see cref="ModbusTcpTagChannelDescriptor"/> 基础上把 <see cref="TagChannelDescriptor.Driver"/>
/// 固定为 <see cref="ZLanTcpNames.DriverName"/>。
/// </summary>
public class ZLanTcpTagChannelDescriptor : ModbusTcpTagChannelDescriptor
{
    /// <summary>
    /// c'tor：驱动名固定为 <see cref="ZLanTcpNames.DriverName"/>。
    /// </summary>
    public ZLanTcpTagChannelDescriptor()
    {
        this.Driver = ZLanTcpNames.DriverName;
    }
}


/// <summary>
/// <see cref="TagChannelDescriptor"/> → <see cref="ZLanTcpTagChannelDescriptor"/> 的转换。
/// </summary>
public static class TagChannelDescriptor_S7Extensions
{
    /// <exception cref="TagsProjectConfigurationException">当前描述符的驱动不是 <see cref="ZLanTcpNames.DriverName"/></exception>
    /// <exception cref="TagsProjectXmlException">Port / MaxWriteRegisters 不合法</exception>
    public static ZLanTcpTagChannelDescriptor ToZLanTcpTagChannelDescriptor(this TagChannelDescriptor descriptor)
    {
        if (descriptor.Driver != ZLanTcpNames.DriverName)
        {
            throw new TagsProjectConfigurationException(
                $"通道驱动错误：期望 {ZLanTcpNames.DriverName}，而当前为{descriptor.Driver}",
                $"Channel({descriptor.Name})");
        }
        if (descriptor is ZLanTcpTagChannelDescriptor d)
        {
            return d;
        }
        var location = $"Channel({descriptor.Name})";
        var res = new ZLanTcpTagChannelDescriptor
        {
            Name = descriptor.Name,
            Driver = descriptor.Driver,
            Extras = descriptor.Extras,
            IpAddr = !descriptor.Extras.TryGetValue(nameof(ZLanTcpTagChannelDescriptor.IpAddr), out var ipAddr) ?
                "localhost" :
                ipAddr.Value,
            Port = !descriptor.Extras.TryGetValue(nameof(ZLanTcpTagChannelDescriptor.Port), out var portEle) ?
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