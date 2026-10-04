using StdUnit.Tags.ModbusTcp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace StdUnit.Tags.ZLan;


public class ZLanTcpTagChannelDescriptor : ModbusTcpTagChannelDescriptor
{
    public ZLanTcpTagChannelDescriptor()
    {
        this.Driver = ZLanTcpNames.DriverName;
    }
}


public static class TagChannelDescriptor_S7Extensions
{
    public static ZLanTcpTagChannelDescriptor ToZLanTcpTagChannelDescriptor(this TagChannelDescriptor descriptor)
    {
        if (descriptor.Driver != ZLanTcpNames.DriverName)
        {
            throw new InvalidOperationException($"通道驱动错误：期望 {ZLanTcpNames.DriverName}，而当前为{descriptor.Driver}");
        }
        if (descriptor is ZLanTcpTagChannelDescriptor d)
        {
            return d;
        }
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
                    throw new ArgumentException($"配置的端口号不是整数"),
            MaxWriteRegisters = !descriptor.Extras.TryGetValue(nameof(ModbusTcpTagChannelDescriptor.MaxWriteRegisters), out var batchEle) ?
                null :
                !ushort.TryParse(batchEle.Value, out var batch) ?
                    throw new ArgumentException($"MaxWriteRegisters 配置不是整数") :
                    batch == 0 ?
                        throw new ArgumentException($"MaxWriteRegisters 必须大于 0") :
                        batch > ModbusTcpChannel.MaxWriteRegistersPerPdu ?
                            throw new ArgumentException($"MaxWriteRegisters 配置({batch})超过协议上限({ModbusTcpChannel.MaxWriteRegistersPerPdu})") :
                            batch,
        };
        return res;
    }

}