using Itminus.Tags.ComScanner;
using Itminus.Tags.ComScanner.Channels;
using Itminus.Tags.ComScanner.Tags;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace Itminus.Tags.Tests.ComTags.ComScriptTests;

public class ComScriptProjTests
{
    private readonly ServiceProvider _root;

    public ComScriptProjTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddComScannerSupport();
        });

        this._root = services.BuildServiceProvider();
    }

    [Theory]
    [InlineData("ComScriptTags.xml")]
    public void TestLoadScriptBasedChannel(string xmlpath)
    {
        using var scope = this._root.CreateScope();
        var sp = scope.ServiceProvider;
        //var factory = sp.GetRequiredService<ITagsProjectFactory>();
        var dir = TestPaths.Fixture("ComTags", "ComScriptTests");
        xmlpath = Path.Combine(dir, xmlpath);
        var root = XElement.Load(xmlpath);
        using var proj = sp.MakeProject(dir!, root);

        Assert.Single(proj.Channels);

        var channel = Assert.IsType<ScriptBasedComChannel>(proj.Channels[0]);
        Assert.Equal("return \"script-value\";", channel.ReadScript);
        Assert.Equal("\r\n", channel.NewLine);

        var g = proj.Tags.SelectGrp("g");
        Assert.NotNull(g);

        var tag = g.SelectTag("脚本串口测点");
        Assert.IsType<ComReadOnlyTag<string>>(tag);
        Assert.Same(channel, tag.Channel);
    }

    [Fact]
    public void ToComChannelDescriptor_ShouldParseExtras()
    {
        var descriptor = new TagChannelDescriptor
        {
            Name = "COM-1",
            Driver = ComDriverNames.DriverName,
            Extras = new Dictionary<string, XElement>
            {
                [nameof(ComChannelDescriptor.Option.NewLine)] = new XElement(nameof(ComChannelDescriptor.Option.NewLine), "\\r\\n"),
                [nameof(ComChannelDescriptor.Option.ReadScript)] = new XElement(nameof(ComChannelDescriptor.Option.ReadScript), "return \"ok\";"),
                [nameof(ComChannelDescriptor.Option.Port)] = new XElement(nameof(ComChannelDescriptor.Option.Port), "COM3"),
                [nameof(ComChannelDescriptor.Option.BaudRate)] = new XElement(nameof(ComChannelDescriptor.Option.BaudRate), "115200"),
                [nameof(ComChannelDescriptor.Option.Parity)] = new XElement(nameof(ComChannelDescriptor.Option.Parity), nameof(Parity.Odd)),
                [nameof(ComChannelDescriptor.Option.DataBits)] = new XElement(nameof(ComChannelDescriptor.Option.DataBits), "7"),
                [nameof(ComChannelDescriptor.Option.StopBits)] = new XElement(nameof(ComChannelDescriptor.Option.StopBits), nameof(StopBits.One)),
                [nameof(ComChannelDescriptor.Option.ChannelCapacity)] = new XElement(nameof(ComChannelDescriptor.Option.ChannelCapacity), "5"),
            },
        };

        var res = descriptor.ToComChannelDescriptor();

        Assert.Equal("COM-1", res.Name);
        Assert.Equal(ComDriverNames.DriverName, res.Driver);
        Assert.Equal("\r\n", res.Option.NewLine);
        Assert.Equal("return \"ok\";", res.Option.ReadScript);
        Assert.Equal("COM3", res.Option.Port);
        Assert.Equal(115200, res.Option.BaudRate);
        Assert.Equal(Parity.Odd, res.Option.Parity);
        Assert.Equal(7, res.Option.DataBits);
        Assert.Equal(StopBits.One, res.Option.StopBits);
        Assert.Equal(5, res.Option.ChannelCapacity);
    }

    [Fact]
    public void ScriptBasedComChannel_DefaultScript_ShouldUseGlobalsName()
    {
        var descriptor = new ComChannelDescriptor
        {
            Name = "COM-9",
            Driver = ComDriverNames.DriverName,
            Option = new ComChannelOption
            {
                Port = "COM_TEST",
            },
        };
        using var channel = new ScriptBasedComChannel(
            descriptor,
            NullLogger<ComChannelBase<string>>.Instance
        );

        Assert.Contains("serial.ReadLine", channel.ReadScript);
    }

    [Fact]
    public void ScriptBasedComChannel_EmptyScript_ShouldFallBackToDefaultScript()
    {
        var descriptor = new ComChannelDescriptor
        {
            Name = "COM-9",
            Driver = ComDriverNames.DriverName,
            Option = new ComChannelOption
            {
                ReadScript = "   ",
            },
        };
        using var channel = new ScriptBasedComChannel(
            descriptor,
            NullLogger<ComChannelBase<string>>.Instance
        );

        Assert.Contains("serial.ReadLine", channel.ReadScript);
    }

    [Theory]
    [InlineData("ComScriptEmptyTags.xml")]
    public void EmptyReadScript_ShouldCreateLineChannel(string xmlpath)
    {
        using var scope = this._root.CreateScope();
        var sp = scope.ServiceProvider;
        var factory = sp.GetRequiredService<ITagsProjectFactory>();
        var dir = TestPaths.Fixture("ComTags", "ComScriptTests");
        xmlpath = Path.Combine(dir, xmlpath);
        var root = XElement.Load(xmlpath);
        using var proj = factory.Create(dir!, root);

        Assert.Single(proj.Channels);
        Assert.IsType<LineBasedComChannel>(proj.Channels[0]);
    }

    [Fact]
    public async Task FakeScriptChannel_ShouldExecuteSyncScript()
    {
        using var ch = new FakeScriptBasedComChannel("return \"sync-ok\";");

        var result = await ch.ExecuteScriptAsync();

        Assert.Equal("sync-ok", result);
    }

    [Fact]
    public async Task FakeScriptChannel_ShouldExecuteAsyncScript()
    {
        using var ch = new FakeScriptBasedComChannel(
            "return await System.Threading.Tasks.Task.FromResult(\"async-ok\");"
        );

        var result = await ch.ExecuteScriptAsync();

        Assert.Equal("async-ok", result);
    }

    [Fact]
    public async Task FakeScriptChannel_ShouldPropagateScriptException()
    {
        using var ch = new FakeScriptBasedComChannel(
            "throw new System.InvalidOperationException(\"script-failed\");"
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ch.ExecuteScriptAsync());

        Assert.Equal("script-failed", ex.Message);
    }

    [Fact]
    public async Task FakeScriptChannel_CanceledToken_ShouldCancelExecution()
    {
        using var ch = new FakeScriptBasedComChannel("return \"never\";");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ch.ExecuteScriptAsync(cts.Token));
    }

    [Fact]
    public async Task FakeScriptChannel_ShouldBindSerialPortGlobal()
    {
        using var ch = new FakeScriptBasedComChannel("var name= serial.GetType().Name; return name;");

        var result = await ch.ExecuteScriptAsync();

        Assert.Equal("SerialPort", result);
    }


    [Theory]
    [InlineData("ComScriptTags2.xml")]
    public void TestLoadComplexScriptBasedChannel(string xmlpath)
    {
        using var scope = this._root.CreateScope();
        var sp = scope.ServiceProvider;
        var factory = sp.GetRequiredService<ITagsProjectFactory>();
        var dir = TestPaths.Fixture("ComTags", "ComScriptTests");
        xmlpath = Path.Combine(dir, xmlpath);
        var root = XElement.Load(xmlpath);
        using var proj = factory.Create(dir!, root);

        Assert.Single(proj.Channels);

        var channel = Assert.IsType<ScriptBasedComChannel>(proj.Channels[0]);
        Assert.Equal(@"if(1 > 3){ return ""<script-value/>""; } else {return ""</script-value>"";}", channel.ReadScript?.Trim());
        Assert.Equal("\r\n", channel.NewLine);

        var g = proj.Tags.SelectGrp("g");
        Assert.NotNull(g);

        var tag = g.SelectTag("脚本串口测点");
        Assert.IsType<ComReadOnlyTag<string>>(tag);
        Assert.Same(channel, tag.Channel);
    }

    private sealed class FakeScriptBasedComChannel : ScriptBasedComChannel
    {
        public FakeScriptBasedComChannel(string script)
            : base(
                new ComChannelDescriptor
                {
                    Name = "FAKE-COM",
                    Driver = ComDriverNames.DriverName,
                    Option = new ComChannelOption
                    {
                        ReadScript = script,
                        ReadScriptDebugInformationEnabled = true
                    },
                },
                NullLogger<ComChannelBase<string>>.Instance
            )
        {
        }

        public Task<string?> ExecuteScriptAsync(CancellationToken ct = default)
        {
            // These tests focus on script runtime behavior, so an unopened SerialPortAdapter is sufficient.
            return base.ParseDataAsync(new SerialPortAdapter(new SerialPort()), ct);
        }
    }
}
