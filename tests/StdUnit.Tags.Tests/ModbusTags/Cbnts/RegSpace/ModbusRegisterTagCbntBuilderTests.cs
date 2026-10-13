using StdUnit.Tags.ModbusTcp;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.ModbusTags;

public class ModbusRegisterTagCbntBuilderTests
{
    [Fact]
    public void Test_TagCombinationCacheSize()
    {
        var cbntDescriptor = new TagCbntDescriptor { Name = "g1", StartAddress = "40001", IsEnabled = true };
        var builder = new ModbusRegisterTagCbntBuilder()
            .WithCbntDescriptor(cbntDescriptor)
            .WithChannel(null!);

        var channelFactory = new ModbusTcpChannelFactory(new LoggerFactory());
        var channel = channelFactory.Create(new TagChannelDescriptor()
        {
            Driver = ModbusTcpNames.DriverName,
            Name = "g1",
            Extras = new Dictionary<string, XElement>() {
                { "IpAddress", new XElement("IpAddress", "127.0.0.1") },
            }
        });
        var tagDescriptors = new List<TagDescriptor> {
            new TagDescriptor{
                TagName ="Float1",
                RawAddress = "40001",
                TagKind = BuiltinTagKinds.FLOAT,
                TagSize = 4,
            },
            new TagDescriptor{
                TagName ="Float2",
                RawAddress = "40003",
                TagKind = BuiltinTagKinds.FLOAT,
                TagSize = 4,
            },
            // leave 40005 empty
            new TagDescriptor{
                TagName ="Bit1",
                RawAddress = "40006.1",
                TagKind = BuiltinTagKinds.BIT,
                TagSize = 2,
            },
            new TagDescriptor{
                TagName ="Bit2",
                RawAddress = "40006.2",
                TagKind = BuiltinTagKinds.BIT,
                TagSize = 2,
            },
            new TagDescriptor{
                TagName ="Bit3",
                RawAddress = "40006.15",
                TagKind = BuiltinTagKinds.BIT,
                TagSize = 2,
            },
            new TagDescriptor{
                TagName ="UInt1",
                RawAddress = "40007",
                TagKind = BuiltinTagKinds.UINT16,
                TagSize = 2,
            },
            // leave 40008 empty
            new TagDescriptor{
                TagName ="UInt2",
                RawAddress = "40009",
                TagKind = BuiltinTagKinds.UINT16,
                TagSize = 2,
            },
            new TagDescriptor{
                TagName ="Bit4",
                RawAddress = "40010.15",
                TagKind = BuiltinTagKinds.BIT,
                TagSize = 2,
            },
        };
        builder.AddTags(tagDescriptors, channel);

        var cbnt = builder.Build(channel);
        Assert.Equal(20, ((TagCbnt<ushort>)cbnt).CacheSize);
    }



}
