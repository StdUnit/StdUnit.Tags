using StdUnit.Tags.ModbusTcp;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StdUnit.Tags.ZLan;

public class ZLanTcpChannel : ModbusTcpChannel
{
    public ZLanTcpChannel(ZLanTcpTagChannelDescriptor descriptor, ILogger<ModbusTcpChannel> logger)
        : base(descriptor, logger)
    {
    }
}
