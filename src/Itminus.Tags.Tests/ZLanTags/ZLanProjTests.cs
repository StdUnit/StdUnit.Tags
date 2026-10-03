using Itminus.Tags.ZLan;
using Itminus.Tags.ModbusTcp;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace Itminus.Tags.Tests.ZLanTags;

public class ZLanProjTests
{
    private readonly ServiceProvider _root;

    public ZLanProjTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddZLanTcpSupport();
        });

        this._root = services.BuildServiceProvider();
    }

    [Fact]
    public void TestLoad()
    {
        var scope = this._root.CreateScope();
        var sp = scope.ServiceProvider;
        var factory = sp.GetRequiredService<ITagsProjectFactory>();
        var dir = TestPaths.Fixture("ZLanTags");
        using var proj = factory.Create(dir!);

        // Test Channels
        Assert.Single(proj.Channels);
        Assert.IsType<ZLanTcpChannel>(proj.Channels[0]);

        // Test Tags
        Assert.NotNull(proj.Tags);
        Assert.Equal("__main__", proj.Tags.TagName());
        var g2 = proj.Tags.SelectGrp("g2");
        Assert.NotNull(g2);


        #region input group
        var btnLetGo = g2.SelectTag("输入/放行按钮闭合状态");
        Assert.Equal("放行按钮闭合状态", btnLetGo.TagName());
        Assert.Equal("DI1", btnLetGo.RawAddress());
        Assert.NotEqual(btnLetGo.RawAddress(), btnLetGo.NormalizedAddress());
        Assert.Equal("1~10001", btnLetGo.NormalizedAddress());
        var normalizedDi = ModBusTcpAddressParser.Parse(btnLetGo.NormalizedAddress());
        Assert.Equal(RegisterKinds.InputContacts, normalizedDi.Area);

        var btnManual = g2.SelectTag("输入/手动");
        Assert.Equal("手动", btnManual.TagName());
        Assert.Equal("DI2", btnManual.RawAddress());
        Assert.NotEqual(btnManual.RawAddress(), btnManual.NormalizedAddress());
        Assert.Equal("1~10002", btnManual.NormalizedAddress());
        var normalizedDi2 = ModBusTcpAddressParser.Parse(btnManual.NormalizedAddress());
        Assert.Equal(RegisterKinds.InputContacts, normalizedDi2.Area);

        // Verify Cache Size
        var input = g2.SelectCbnt("输入");
        Assert.Equal(2, ((TagCbnt<bool>)input).CacheSize);
        Assert.Equal(2, ((TagCbnt<bool>)input).Cache.Length);
        #endregion

        #region output group
        var ledGreen = g2.SelectTag("输出/绿灯");
        Assert.Equal("绿灯", ledGreen.TagName());
        Assert.Equal("DO1", ledGreen.RawAddress());
        Assert.NotEqual(ledGreen.RawAddress(), ledGreen.NormalizedAddress());
        Assert.Equal("1~00017", ledGreen.NormalizedAddress());
        var normalizedDo = ModBusTcpAddressParser.Parse(ledGreen.NormalizedAddress());
        Assert.Equal(RegisterKinds.OutputCoils, normalizedDo.Area);

        var ledRed = g2.SelectTag("输出/红灯");
        Assert.Equal("红灯", ledRed.TagName());

        var ledYellow = g2.SelectTag("输出/黄灯");
        Assert.Equal("黄灯", ledYellow.TagName());

        var ledLetGo = g2.SelectTag("输出/放行灯");
        Assert.Equal("放行灯", ledLetGo.TagName());

        // Verify Cache Size
        var output = g2.SelectCbnt("输出");
        Assert.Equal(4, ((TagCbnt<bool>)output).CacheSize);
        Assert.Equal(4, ((TagCbnt<bool>)output).Cache.Length);
        #endregion
    }

    [Fact]
    public void ZLanCbntBuilder_StartAddress_DoesNotPolluteDescriptor()
    {
        var descriptor = new TagCbntDescriptor
        {
            Name = "test-cbnt",
            StartAddress = "40001",
        };
        descriptor.Extras["slave"] = new XAttribute("slave", "2");

        var builder = new ZLanDICbntBuilder();
        builder.WithCbntDescriptor(descriptor);

        // Descriptor 的 StartAddress 应保持不变
        Assert.Equal("40001", descriptor.StartAddress);

        // TagCbnt 的 StartAddress 应是解析后的合成地址
        var resolved = builder.TagCbnt.StartAddress;
        Assert.StartsWith("2~", resolved);
        Assert.NotEqual("40001", resolved);
    }
}
