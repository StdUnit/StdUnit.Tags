using StdUnit.Tags;
using StdUnit.Tags.S7;
using Microsoft.Extensions.DependencyInjection;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.S7Tags;

/// <summary>
/// 测试 <see cref="TagsProject_Extensions"/> 重构后，
/// <see cref="TagsProject_Extensions.AddS7Channel"/> +
/// <see cref="TagsProject_Extensions.AddS7TagCbntBuilder"/> +
/// <see cref="TagsProject_Extensions.AddS7DirectTagBuilder"/> 的手动组合
/// </summary>
public class S7BuilderTests
{
    /// <summary>
    /// 构建测试 XML：含 TagCbnt（组合测点）
    /// </summary>
    private static XElement BuildCbntXml() => XElement.Parse(@"
<root>
    <Channel name='S7-1' driver='S7'>
        <IpAddr>localhost</IpAddr>
        <Rack>0</Rack>
        <Slot>1</Slot>
    </Channel>
    <TagGrp name='g1' isEntry='true' isEnabled='true' address='DB200.100.1' channel='S7-1' scanInterval='10'>
        <TagCbnt name='拍照请求' access='RO' address='DB200.100.1'>
            <Tag name='拍照-请求-标志' address='$$100.1' type='BIT' />
            <Tag name='拍照-请求-料号' address='DB200.102' type='BYTE' />
            <Tag name='拍照-请求-程序号' address='$$104' type='INT16' />
        </TagCbnt>
        <TagCbnt name='拍照响应' access='R1W' address='DB200.400.0'>
            <Tag name='拍照-响应-标志' address='$$400.0' type='BIT' />
            <Tag name='拍照-响应-B1' address='$$400.1' type='BIT' />
        </TagCbnt>
    </TagGrp>
</root>");

    /// <summary>
    /// 构建测试 XML：直接测点（TagGrp 下直接挂 Tag）
    /// </summary>
    private static XElement BuildDirectTagsXml() => XElement.Parse(@"
<root>
    <Channel name='S7-1' driver='S7'>
        <IpAddr>localhost</IpAddr>
        <Rack>0</Rack>
        <Slot>1</Slot>
    </Channel>
    <TagGrp name='g-direct' isEntry='true' isEnabled='true' channel='S7-1' scanInterval='10'>
        <Tag name='bit-flag' address='DB200.100.1' type='BIT' />
        <Tag name='byte-v' address='DB200.102' type='BYTE' />
        <Tag name='int16-v' address='DB200.104' type='INT16' />
        <Tag name='str-v' address='DB200.106' type='STR' maxlen='8' />
    </TagGrp>
</root>");

    #region 等效性：AddS7Support ≡ 手动组合三个小粒度方法

    [Fact]
    public void AddS7Support_EquivalentTo_ManualComposition()
    {
        // Arrange A：旧方式 AddS7Support
        var services1 = new ServiceCollection();
        services1.AddLogging();
        services1.AddTagsProjectServices(b => { b.AddS7Support(); });
        using var root1 = services1.BuildServiceProvider();
        using var scope1 = root1.CreateScope();

        // Arrange B：新方式手动组合
        var services2 = new ServiceCollection();
        services2.AddLogging();
        services2.AddTagsProjectServices(b =>
        {
            b.AddS7Channel();
            b.AddS7TagCbntBuilder();
            b.AddS7DirectTagBuilder();
        });
        using var root2 = services2.BuildServiceProvider();
        using var scope2 = root2.CreateScope();

        var factory1 = scope1.ServiceProvider.GetRequiredService<ITagsProjectFactory>();
        var factory2 = scope2.ServiceProvider.GetRequiredService<ITagsProjectFactory>();

        // Act：加载同一个 XML（含 TagCbnt）
        using var proj1 = factory1.Create(string.Empty, BuildCbntXml());
        using var proj2 = factory2.Create(string.Empty, BuildCbntXml());

        // Assert：通道一致
        Assert.Equal(proj1.Channels.Count, proj2.Channels.Count);

        // Assert：群组一致
        Assert.Equal(proj1.Tags.Children.Count, proj2.Tags.Children.Count);
        var g1 = proj1.Tags.SelectGrp("g1");
        var g2 = proj2.Tags.SelectGrp("g1");
        Assert.NotNull(g1);
        Assert.NotNull(g2);
        foreach (var child in g1.Children)
        {
            Assert.True(g2.Children.ContainsKey(child.Key));
        }
    }

    #endregion

    #region 拆分注册：仅 Channel + DirectTagBuilder（不注册 TagCbntBuilder）可加载直接测点

    [Fact]
    public void AddS7Channel_WithS7DirectTagBuilder_Only_LoadsDirectTags()
    {
        // Arrange：只注册 Channel + DirectTagBuilder，不注册 TagCbntBuilder
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddS7Channel();
            b.AddS7DirectTagBuilder();
        });

        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();

        // Act：加载只含直接测点的 XML
        using var proj = scope.ServiceProvider.GetRequiredService<ITagsProjectFactory>()
            .Create(string.Empty, BuildDirectTagsXml());

        // Assert：通道
        Assert.Single(proj.Channels);
        Assert.IsType<S7TagChannel>(proj.Channels[0]);

        // Assert：直接测点被加载
        var g = proj.Tags.SelectGrp("g-direct");
        Assert.NotNull(g);
        Assert.True(g.Children.ContainsKey("bit-flag"));
        Assert.True(g.Children.ContainsKey("byte-v"));
        Assert.True(g.Children.ContainsKey("int16-v"));
        Assert.True(g.Children.ContainsKey("str-v"));
    }

    #endregion

    #region 拆分注册：DirectTagBuilder 的 configure/predicate 钩子

    [Fact]
    public void AddS7DirectTagBuilder_WithPredicate_OnlyHandlesMatchingTags()
    {
        // Arrange：第一个 DirectTagBuilder 只接受 BIT 类型，第二个接管其余
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddS7Channel();

            // 第一个：只处理 BIT 直接测点
            b.AddS7DirectTagBuilder(
                predicate: bd => bd.TagDescriptor.TagKind == BuiltinTagKinds.BIT
            );

            // 第二个：处理其余所有直接测点
            b.AddS7DirectTagBuilder();
        });

        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();

        // Act
        using var proj = scope.ServiceProvider.GetRequiredService<ITagsProjectFactory>()
            .Create(string.Empty, BuildDirectTagsXml());

        // Assert：所有直接测点仍被正确加载（BIT 由第一个 builder 处理，其余由第二个）
        var g = proj.Tags.SelectGrp("g-direct");
        Assert.NotNull(g);
        var bit = g.SelectTag("bit-flag");
        Assert.NotNull(bit);
        Assert.Equal(BuiltinTagKinds.BIT, bit.TagKind());

        var byteV = g.SelectTag("byte-v");
        Assert.NotNull(byteV);
        Assert.Equal(BuiltinTagKinds.BYTE, byteV.TagKind());

        var int16V = g.SelectTag("int16-v");
        Assert.NotNull(int16V);
        Assert.Equal(BuiltinTagKinds.INT16, int16V.TagKind());
    }

    #endregion
}
