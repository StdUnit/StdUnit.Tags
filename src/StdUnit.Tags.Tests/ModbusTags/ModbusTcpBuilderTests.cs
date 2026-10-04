using StdUnit.Tags;
using StdUnit.Tags.ModbusTcp;
using Microsoft.Extensions.DependencyInjection;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.ModbusTags;

/// <summary>
/// 测试 <see cref="TagsProject_Extensions"/> 重构后，
/// <see cref="TagsProject_Extensions.AddModbusTcpChannel"/> +
/// <see cref="TagsProject_Extensions.AddModbusBitTagCbntBuilder"/> +
/// <see cref="TagsProject_Extensions.AddModbusTcpDirectTagBuilder"/> 的手动组合
/// </summary>
public class ModbusTcpBuilderTests
{
    /// <summary>
    /// 构建测试 XML：含 TagCbnt（组合测点）
    /// </summary>
    private static XElement BuildCbntXml() => XElement.Parse(@"
<root>
    <Channel name='ModbusTcp-2' driver='ModbusTcp'>
        <IpAddr>localhost</IpAddr>
        <Port>502</Port>
    </Channel>
    <TagGrp name='g3' isEntry='true' isEnabled='true' channel='ModbusTcp-2' scanInterval='10'>
        <TagCbnt name='输入' address='10001' access='RO'>
            <Tag name='放行按钮闭合状态' address='10001' type='DI'></Tag>
            <Tag name='手动' address='10002' type='DI'></Tag>
        </TagCbnt>
        <TagCbnt name='输出' address='00001' access='R1W'>
            <Tag name='绿灯' address='00020' type='DO'></Tag>
            <Tag name='红灯' address='00021' type='DO'></Tag>
        </TagCbnt>
    </TagGrp>
</root>");

    /// <summary>
    /// 构建测试 XML：直接测点（TagGrp 下直接挂 Tag）
    /// </summary>
    private static XElement BuildDirectTagsXml() => XElement.Parse(@"
<root>
    <Channel name='ModbusTcp-2' driver='ModbusTcp'>
        <IpAddr>localhost</IpAddr>
        <Port>502</Port>
    </Channel>
    <TagGrp name='g-direct' isEntry='true' isEnabled='true' channel='ModbusTcp-2' scanInterval='10'>
        <Tag name='bit-v' address='1~40021.7' type='BIT' />
        <Tag name='byte-v' address='1~40020' type='BYTE' />
        <Tag name='u32-v' address='1~40022' type='UINT32' />
        <Tag name='i16-v' address='1~40024' type='INT16' />
    </TagGrp>
</root>");

    #region 等效性：AddModbusTcpSupport ≡ 手动组合三个小粒度方法

    [Fact]
    public void AddModbusTcpSupport_EquivalentTo_ManualComposition()
    {
        // Arrange A：旧方式 AddModbusTcpSupport
        var services1 = new ServiceCollection();
        services1.AddLogging();
        services1.AddTagsProjectServices(b => { b.AddModbusTcpSupport(); });
        using var root1 = services1.BuildServiceProvider();
        using var scope1 = root1.CreateScope();

        // Arrange B：新方式手动组合
        var services2 = new ServiceCollection();
        services2.AddLogging();
        services2.AddTagsProjectServices(b =>
        {
            b.AddModbusTcpChannel();
            b.AddModbusBitTagCbntBuilder();
            b.AddModbusTcpDirectTagBuilder();
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
        var g1 = proj1.Tags.SelectGrp("g3");
        var g2 = proj2.Tags.SelectGrp("g3");
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
    public void AddModbusTcpChannel_WithDirectTagBuilder_Only_LoadsDirectTags()
    {
        // Arrange：只注册 Channel + DirectTagBuilder，不注册 TagCbntBuilder
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddModbusTcpChannel();
            b.AddModbusTcpDirectTagBuilder();
        });

        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();

        // Act：加载只含直接测点的 XML
        using var proj = scope.ServiceProvider.GetRequiredService<ITagsProjectFactory>()
            .Create(string.Empty, BuildDirectTagsXml());

        // Assert：通道
        Assert.Single(proj.Channels);
        Assert.IsType<ModbusTcpChannel>(proj.Channels[0]);

        // Assert：直接测点被加载
        var g = proj.Tags.SelectGrp("g-direct");
        Assert.NotNull(g);
        Assert.True(g.Children.ContainsKey("bit-v"));
        Assert.True(g.Children.ContainsKey("byte-v"));
        Assert.True(g.Children.ContainsKey("u32-v"));
        Assert.True(g.Children.ContainsKey("i16-v"));
    }

    #endregion

    #region 拆分注册：DirectTagBuilder 的 configure/predicate 钩子

    [Fact]
    public void AddModbusTcpDirectTagBuilder_WithPredicate_OnlyHandlesMatchingTags()
    {
        // Arrange：第一个 DirectTagBuilder 只接受 BIT 类型，第二个接管其余
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddModbusTcpChannel();

            // 第一个：只处理 BIT 直接测点
            b.AddModbusTcpDirectTagBuilder(
                predicate: bd => bd.TagDescriptor.TagKind == BuiltinTagKinds.BIT
            );

            // 第二个：处理其余所有直接测点
            b.AddModbusTcpDirectTagBuilder();
        });

        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();

        // Act
        using var proj = scope.ServiceProvider.GetRequiredService<ITagsProjectFactory>()
            .Create(string.Empty, BuildDirectTagsXml());

        // Assert：所有直接测点仍被正确加载
        var g = proj.Tags.SelectGrp("g-direct");
        Assert.NotNull(g);
        var bit = g.SelectTag("bit-v");
        Assert.NotNull(bit);
        Assert.Equal(BuiltinTagKinds.BIT, bit.TagKind());

        var byteV = g.SelectTag("byte-v");
        Assert.NotNull(byteV);
        Assert.Equal(BuiltinTagKinds.BYTE, byteV.TagKind());

        var u32 = g.SelectTag("u32-v");
        Assert.NotNull(u32);
        Assert.Equal(BuiltinTagKinds.UINT32, u32.TagKind());
    }

    #endregion
}
