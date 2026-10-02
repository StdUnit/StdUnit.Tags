using System;
using System.IO;
using Itminus.Tags.SimpleFiles;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Itminus.Tags.Tests.SimpleFilesTags;

public class ProjTests
{
    private readonly ServiceProvider _root;

    public ProjTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddSimpleFilesSupport();
        });

        this._root = services.BuildServiceProvider();
    }

    [Fact]
    public void BuildProject_WithAllTagTypes_LoadsCorrectly()
    {
        using var scope = this._root.CreateScope();
        var sp = scope.ServiceProvider;
        var factory = sp.GetRequiredService<ITagsProjectFactory>();

        var dir = TestPaths.Fixture("SimpleFilesTags", "DirectTags");

        using var proj = factory.Create(dir);

        // 验证通道
        Assert.Single(proj.Channels);
        Assert.IsType<SimpleFilesTagChannel>(proj.Channels[0]);
        Assert.Equal(SimpleFilesNames.DriverName, proj.Channels[0].Driver());

        // 验证群组
        var grp = proj.Tags.SelectGrp("g-direct");
        Assert.NotNull(grp);
        Assert.True(grp.IsEntry());

        // 验证各个类型的测点
        var bit = grp.SelectTag("bit-v");
        Assert.Equal(BuiltinTagKinds.BIT, bit.TagKind());
        Assert.Equal("bit.txt", bit.RawAddress());

        var byteTag = grp.SelectTag("byte-v");
        Assert.Equal(BuiltinTagKinds.BYTE, byteTag.TagKind());
        Assert.Equal("byte.txt", byteTag.RawAddress());

        var shortTag = grp.SelectTag("short-v");
        Assert.Equal(BuiltinTagKinds.INT16, shortTag.TagKind());
        Assert.Equal("short.txt", shortTag.RawAddress());

        var ushortTag = grp.SelectTag("ushort-v");
        Assert.Equal(BuiltinTagKinds.UINT16, ushortTag.TagKind());
        Assert.Equal("ushort.txt", ushortTag.RawAddress());

        var intTag = grp.SelectTag("int-v");
        Assert.Equal(BuiltinTagKinds.INT32, intTag.TagKind());
        Assert.Equal("int.txt", intTag.RawAddress());

        var uintTag = grp.SelectTag("uint-v");
        Assert.Equal(BuiltinTagKinds.UINT32, uintTag.TagKind());
        Assert.Equal("uint.txt", uintTag.RawAddress());

        var floatTag = grp.SelectTag("float-v");
        Assert.Equal(BuiltinTagKinds.FLOAT, floatTag.TagKind());
        Assert.Equal("float.txt", floatTag.RawAddress());

        var doubleTag = grp.SelectTag("double-v");
        Assert.Equal(BuiltinTagKinds.DOUBLE, doubleTag.TagKind());
        Assert.Equal("double.txt", doubleTag.RawAddress());

        var strTag = grp.SelectTag("str-v");
        Assert.Equal(BuiltinTagKinds.STR, strTag.TagKind());
        Assert.Equal("str.txt", strTag.RawAddress());
    }

    [Fact]
    public void BuildProject_NormalizedAddress_CombinesBaseDirWithAddress()
    {
        using var scope = this._root.CreateScope();
        var sp = scope.ServiceProvider;
        var factory = sp.GetRequiredService<ITagsProjectFactory>();

        var dir = TestPaths.Fixture("SimpleFilesTags", "DirectTags");

        using var proj = factory.Create(dir);

        var grp = proj.Tags.SelectGrp("g-direct");
        var tag = grp.SelectTag("int-v");

        // BaseDir="." → Path.Combine(".", "int.txt") = ".\\int.txt"
        Assert.EndsWith("int.txt", tag.NormalizedAddress());
    }
}
