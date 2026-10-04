using StdUnit.Tags.ModbusTcp;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace StdUnit.Tags.Tests.ModbusTags;

public class ModbusTcpProjTests
{
    private readonly ServiceProvider _root;

    public ModbusTcpProjTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddModbusTcpSupport();
        });

        this._root = services.BuildServiceProvider();
    }

    [Fact]
    public void TestLoad()
    {
        var scope = this._root.CreateScope();
        var sp = scope.ServiceProvider;
        var factory = sp.GetRequiredService<ITagsProjectFactory>();
        var dir = TestPaths.Fixture("ModbusTags");
        using var proj = factory.Create(dir!);

        // Test Channels
        Assert.Single(proj.Channels);
        Assert.IsType<ModbusTcpChannel>(proj.Channels[0]);

        // Test Tags
        var g3 = proj.Tags.SelectGrp("g3");
        Assert.NotNull(g3);


        #region input group
        // Verify Cache Size
        var input = g3.SelectCbnt("输入");
        Assert.Equal(2, ((TagCbnt<bool>)input).CacheSize);
        Assert.Equal(2, ((TagCbnt<bool>)input).Cache.Length);

        var btnLetGo = g3.SelectTag("输入/放行按钮闭合状态");
        Assert.Equal("放行按钮闭合状态", btnLetGo.TagName());
        Assert.Equal("10001", btnLetGo.RawAddress());
        Assert.Equal("10001", btnLetGo.NormalizedAddress());

        var btnManual = g3.SelectTag("输入/手动");
        Assert.Equal("手动", btnManual.TagName());
        Assert.Equal("10002", btnManual.RawAddress());
        Assert.Equal("10002", btnManual.NormalizedAddress());

        #endregion

        #region output group
        // Verify Cache Size
        var output = g3.SelectCbnt("输出");
        Assert.Equal(23, ((TagCbnt<bool>)output).CacheSize);
        Assert.Equal(23, ((TagCbnt<bool>)output).Cache.Length);

        var ledGreen = g3.SelectTag("输出/绿灯");
        Assert.Equal("绿灯", ledGreen.TagName());
        Assert.Equal("00020", ledGreen.RawAddress());
        Assert.Equal("00020", ledGreen.NormalizedAddress());

        var ledRed = g3.SelectTag("输出/红灯");
        Assert.Equal("红灯", ledRed.TagName());

        var ledYellow = g3.SelectTag("输出/黄灯");
        Assert.Equal("黄灯", ledYellow.TagName());

        var ledLetGo = g3.SelectTag("输出/放行灯");
        Assert.Equal("放行灯", ledLetGo.TagName());

        #endregion

        #region acquire group
        // Verify Cache Size
        var acq = g3.SelectCbnt("采集");
        Assert.Equal(24 * 2 + 2, ((TagCbnt<ushort>)acq).CacheSize);
        Assert.Equal(25, ((TagCbnt<ushort>)acq).Cache.Length); // 50 字节 = 25 个寄存器

        var acq1 = acq.SelectTag("byte");
        Assert.Equal("byte", acq1.TagName());
        Assert.Equal("40020", acq1.TagDescriptor.NormalizedAddress);
        Assert.Equal("40020", acq1.RawAddress());
        Assert.Equal(BuiltinTagKinds.BYTE, acq1.TagKind());

        var acq2 = acq.SelectTag("int16");
        Assert.Equal("int16", acq2.TagName());
        Assert.Equal("40021", acq2.TagDescriptor.NormalizedAddress);
        Assert.Equal(BuiltinTagKinds.INT16, acq2.TagKind());

        var acq3 = acq.SelectTag("int32");
        Assert.Equal("int32", acq3.TagName());
        Assert.Equal("40022", acq3.TagDescriptor.NormalizedAddress);
        Assert.Equal(BuiltinTagKinds.INT32, acq3.TagKind());

        var acq4 = acq.SelectTag("float");
        Assert.Equal("float", acq4.TagName());
        Assert.Equal("40024", acq4.TagDescriptor.NormalizedAddress);
        Assert.Equal(BuiltinTagKinds.FLOAT, acq4.TagKind());
        #endregion
    }
}
