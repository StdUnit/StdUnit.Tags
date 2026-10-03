using Itminus.Tags.S7;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using Xunit;

namespace Itminus.Tags.Tests.S7Tags;

public class S7DirectTagsProjectTests
{
    private readonly ServiceProvider _root;

    public S7DirectTagsProjectTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddS7Support();
        });

        this._root = services.BuildServiceProvider();
    }

    [Fact]
    public void BuildProject_WithDirectTagsUnderTagGrp_Works()
    {
        using var scope = this._root.CreateScope();
        var sp = scope.ServiceProvider;
        var factory = sp.GetRequiredService<ITagsProjectFactory>();

        var dir = TestPaths.Fixture("S7Tags", "DirectTags");

        using var proj = factory.Create(dir);

        Assert.Single(proj.Channels);
        var channel = proj.Channels[0];
        Assert.IsType<S7TagChannel>(channel);

        var grp = proj.Tags.SelectGrp("g-direct");
        Assert.NotNull(grp);


        var bit = grp.SelectTag("bit-flag");
        Assert.IsType<BitDirectTag>(bit);
        Assert.Equal(BuiltinTagKinds.BIT, bit.TagKind());
        Assert.Equal("DB200.100.1", bit.NormalizedAddress());
        Assert.Equal(channel, bit.SearchRequiredChannel());
        Assert.Null(bit.Channel);

        var b = grp.SelectTag("byte-v");
        Assert.IsType<ByteDirectTag>(b);
        Assert.Equal(BuiltinTagKinds.BYTE, b.TagKind());
        Assert.Equal("DB200.102", b.NormalizedAddress());
        Assert.Equal(channel, b.SearchRequiredChannel());
        Assert.Null(b.Channel);

        var i16 = grp.SelectTag("int16-v");
        Assert.IsType<Int16DirectTag>(i16);
        Assert.Equal(BuiltinTagKinds.INT16, i16.TagKind());
        Assert.Equal("DB200.104", i16.NormalizedAddress());
        Assert.Equal(channel, i16.SearchRequiredChannel());
        Assert.Null(i16.Channel);

        var str = grp.SelectTag("str-v");
        var s = Assert.IsType<StrDirectTag>(str);
        Assert.Equal(BuiltinTagKinds.STR, str.TagKind());
        Assert.Equal("DB200.106", str.NormalizedAddress());
        Assert.Equal((byte)8, s.Maxlen);
        Assert.Equal(channel, str.SearchRequiredChannel());
        Assert.Null(str.Channel);
    }


}
