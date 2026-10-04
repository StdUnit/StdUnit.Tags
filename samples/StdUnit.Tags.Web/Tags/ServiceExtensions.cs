
using System.Reflection;
using StdUnit.Tags.S7;
using StdUnit.Tags.ZLan;
using StdUnit.Tags.ModbusTcp;
using StdUnit.Tags.OpcUaClient;
using StdUnit.Tags.ComScanner;
using StdUnit.Tags.Hjzk;
using System.Xml.Linq;

namespace StdUnit.Tags.Web.Tags;

public static class ServiceExtensions
{
    public static void AddTags(this IServiceCollection services)
    {
        services.AddTagsProjectServices(b =>
        {
            b.AddS7Support();
            b.AddModbusTcpSupport();
            b.AddZLanTcpSupport();
            b.AddOpcUaClientSupport();
            b.AddHjzkSupport();
            b.AddComScannerSupport();
        });
    }

}
