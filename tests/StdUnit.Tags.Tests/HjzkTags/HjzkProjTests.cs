using Microsoft.Extensions.DependencyInjection;
using StdUnit.Tags.ModbusTcp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

using StdUnit.Tags.Hjzk;

namespace StdUnit.Tags.Tests.HjzkTags;

public class HjzkProjTests
{
    private readonly ServiceProvider _root;

    public HjzkProjTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddHjzkSupport();
        });

        this._root = services.BuildServiceProvider();
    }

    [Fact]
    public void TestLoad()
    {
        var scope = this._root.CreateScope();
        var sp = scope.ServiceProvider;
        var dir = TestPaths.Fixture("HjzkTags");
        using var proj = sp.MakeProject(dir);

        // Test Channels
        Assert.Single(proj.Channels);
        Assert.IsType<HjzkChannel>(proj.Channels[0]);

        // Test Tags
        Assert.NotNull(proj.Tags);
        Assert.Equal("__main__", proj.Tags.TagName());
        var g2 = proj.Tags.SelectGrp("g2");
        Assert.NotNull(g2);


        #region input group
        var btnLetGo = g2.SelectTag("输入/放行按钮闭合状态");
        Assert.Equal("放行按钮闭合状态", btnLetGo.TagName());
        Assert.Equal("DI1", btnLetGo.RawAddress());
        Assert.Equal("1~11000", btnLetGo.NormalizedAddress());

        var btnManual = g2.SelectTag("输入/手动");
        Assert.Equal("手动", btnManual.TagName());
        Assert.Equal("DI2", btnManual.RawAddress());
        Assert.Equal("1~11001", btnManual.NormalizedAddress());

        // Verify Cache Size
        var input = g2.SelectCbnt("输入");
        Assert.Equal(2, ((TagCbnt<bool>)input).CacheSize);
        Assert.Equal(2, ((TagCbnt<bool>)input).Cache.Length);
        #endregion

        #region output group
        var ledGreen = g2.SelectTag("输出/绿灯");
        Assert.Equal("绿灯", ledGreen.TagName());
        Assert.Equal("DO1", ledGreen.RawAddress());
        Assert.Equal("1~00000", ledGreen.NormalizedAddress());

        var ledRed = g2.SelectTag("输出/红灯");
        Assert.Equal("红灯", ledRed.TagName());
        Assert.Equal("DO2", ledRed.RawAddress());
        Assert.Equal("1~00001", ledRed.NormalizedAddress());

        var ledYellow = g2.SelectTag("输出/黄灯");
        Assert.Equal("黄灯", ledYellow.TagName());
        Assert.Equal("DO3", ledYellow.RawAddress());
        Assert.Equal("1~00002", ledYellow.NormalizedAddress());

        var ledLetGo = g2.SelectTag("输出/放行灯");
        Assert.Equal("放行灯", ledLetGo.TagName());
        Assert.Equal("DO4", ledLetGo.RawAddress());
        Assert.Equal("1~00003", ledLetGo.NormalizedAddress());

        // Verify Cache Size
        var output = g2.SelectCbnt("输出");
        Assert.Equal(4, ((TagCbnt<bool>)output).CacheSize);
        Assert.Equal(4, ((TagCbnt<bool>)output).Cache.Length);
        #endregion
    }
}
