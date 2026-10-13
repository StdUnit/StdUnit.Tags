using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.Core.Channels;

public class TagChannelDescriptorCopyTests
{
    [Fact]
    public void Copy_RoundtripsNameAndDriver()
    {
        var original = new TagChannelDescriptor
        {
            Name = "S7-1",
            Driver = "S7",
        };

        var copy = original.Copy();

        Assert.Equal("S7-1", copy.Name);
        Assert.Equal("S7", copy.Driver);
    }

    [Fact]
    public void Copy_CreatesNewInstance()
    {
        var original = new TagChannelDescriptor
        {
            Name = "S7-1",
            Driver = "S7",
        };

        var copy = original.Copy();

        Assert.NotSame(original, copy);
    }

    [Fact]
    public void Copy_ModifyingOriginalDoesNotAffectCopy()
    {
        var original = new TagChannelDescriptor
        {
            Name = "original",
            Driver = "S7",
        };

        var copy = original.Copy();
        original.Name = "modified";
        original.Driver = "MODIFIED";

        Assert.Equal("original", copy.Name);
        Assert.Equal("S7", copy.Driver);
    }

    [Fact]
    public void Copy_RoundtripsExtras()
    {
        var original = new TagChannelDescriptor
        {
            Name = "S7-1",
            Driver = "S7",
        };
        original.Extras["IpAddr"] = new XElement("IpAddr", "192.168.1.1");
        original.Extras["Rack"] = new XElement("Rack", "1");
        original.Extras["Slot"] = new XElement("Slot", "2");

        var copy = original.Copy();

        Assert.Equal(3, copy.Extras.Count);
        Assert.Equal("192.168.1.1", copy.Extras["IpAddr"].Value);
        Assert.Equal("1", copy.Extras["Rack"].Value);
        Assert.Equal("2", copy.Extras["Slot"].Value);
    }

    [Fact]
    public void Copy_ExtrasDictionaryIsNewInstance()
    {
        var original = new TagChannelDescriptor
        {
            Name = "S7-1",
            Driver = "S7",
        };
        original.Extras["IpAddr"] = new XElement("IpAddr", "192.168.1.1");

        var copy = original.Copy();

        Assert.NotSame(original.Extras, copy.Extras);
        Assert.Equal("192.168.1.1", copy.Extras["IpAddr"].Value);
    }

    [Fact]
    public void Copy_ModifyingOriginalExtrasDoesNotAffectCopy()
    {
        var original = new TagChannelDescriptor
        {
            Name = "S7-1",
            Driver = "S7",
        };
        original.Extras["IpAddr"] = new XElement("IpAddr", "192.168.1.1");

        var copy = original.Copy();
        original.Extras["IpAddr"].SetValue("10.0.0.1");

        Assert.Equal("192.168.1.1", copy.Extras["IpAddr"].Value);
    }
}
