using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StdUnit.Tags.Tests.Core.Channels;

public class CompositeTagChannelFactoryTests
{
    private class FakeChannel : ITagChannel
    {
        public FakeChannel(string from, TagChannelDescriptor descriptor)
        {
            this.From = from;
            this.Descriptor = descriptor;
        }

        /// <summary>
        /// 由哪个工厂创建的
        /// </summary>
        public string From { get; }

        public TagChannelDescriptor Descriptor { get; set; }
        public Task EnsureConnectedAsync(bool force, CancellationToken ct) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;
        public void Dispose() { }
    }

    private class FakeFactory : ITagChannelFactory
    {
        private readonly string[] _drivers;
        public FakeFactory(params string[] drivers) { _drivers = drivers; }

        public string Name { get; set; } = "FakeFactory";
        public ITagChannel Create(TagChannelDescriptor descriptor) => new FakeChannel(Name, descriptor);
        public IReadOnlyList<string> GetAvailableDrivers() => _drivers;
    }

    [Fact]
    public void AddFactory_ReturnsSelf()
    {
        var composite = new CompositeTagChannelFactory();

        var result = composite.AddFactory(new FakeFactory("S7"));

        Assert.Same(composite, result);
    }

    [Fact]
    public void FactoryList_WhenEmpty_ReturnsEmpty()
    {
        var composite = new CompositeTagChannelFactory();
        Assert.Empty(composite.FactoryList);
    }

    [Fact]
    public void FactoryList_WhenAddFactory_ReturnsNonEmpty()
    {
        var composite = new CompositeTagChannelFactory();
        composite.AddFactory(new FakeFactory("S7"));
        Assert.NotEmpty(composite.FactoryList);
        Assert.Single(composite.FactoryList);
    }


    [Fact]
    public void GetAvailableDrivers_WhenEmpty_ReturnsEmpty()
    {
        var composite = new CompositeTagChannelFactory();

        var drivers = composite.GetAvailableDrivers();
        Assert.Empty(drivers);
        Assert.Empty(composite.FactoryList);
    }

    [Fact]
    public void GetAvailableDrivers_AggregatesAllFactories()
    {
        var composite = new CompositeTagChannelFactory();
        composite.AddFactory(new FakeFactory("S7", "ModbusTCP"));
        composite.AddFactory(new FakeFactory("OPCUA"));

        var drivers = composite.GetAvailableDrivers();

        Assert.Equal(3, drivers.Count);
        Assert.Contains("S7", drivers);
        Assert.Contains("ModbusTCP", drivers);
        Assert.Contains("OPCUA", drivers);
    }

    [Fact]
    public void Create_WithMatchingDriver_ReturnsChannel()
    {
        var composite = new CompositeTagChannelFactory();
        composite.AddFactory(new FakeFactory("S7"));

        var channel = composite.Create(new TagChannelDescriptor { Driver = "S7", Name = "S7-1" });

        Assert.NotNull(channel);
        Assert.Equal("S7-1", channel.ChannelName());
        Assert.Equal("S7", channel.Driver());
    }

    [Fact]
    public void Create_WithNoMatchingFactory_Throws()
    {
        var composite = new CompositeTagChannelFactory();
        composite.AddFactory(new FakeFactory("S7"));

        var ex = Assert.Throws<Exception>(() =>
            composite.Create(new TagChannelDescriptor { Driver = "MODBUS", Name = "m1" }));
        Assert.Contains("MODBUS", ex.Message);
    }

    [Fact]
    public void Create_WithNoFactories_Throws()
    {
        var composite = new CompositeTagChannelFactory();

        var ex = Assert.Throws<Exception>(() =>
            composite.Create(new TagChannelDescriptor { Driver = "S7", Name = "s7-1" }));
        Assert.Contains("S7", ex.Message);
    }

    [Fact]
    public void Create_PicksFirstMatchingFactory()
    {
        var composite = new CompositeTagChannelFactory();
        var first = new FakeFactory("S7");
        var second = new FakeFactory("S7");
        composite.AddFactory(first);
        composite.AddFactory(second);

        // 两个工厂都支持 S7，应选中第一个命中的
        var channel = composite.Create(new TagChannelDescriptor { Driver = "S7", Name = "s7-1" });

        Assert.NotNull(channel);
        Assert.IsType<FakeChannel>(channel);
        var ch = channel as FakeChannel;
        Assert.NotNull(ch);
        Assert.Equal(first.Name, ch.From);
    }
}
