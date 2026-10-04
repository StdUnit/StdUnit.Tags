using StdUnit.Tags;
using StdUnit.Tags.OpcUaClient;
using Microsoft.Extensions.DependencyInjection;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.OpcUaClientTags;

/// <summary>
/// 测试 <see cref="TagsProject_Extensions"/> 重构后，
/// <see cref="TagsProject_Extensions.AddOpcUaClientChannel"/> +
/// <see cref="TagsProject_Extensions.AddOpcUaClientTagCbntBuilder"/> +
/// <see cref="TagsProject_Extensions.AddOpcUaClientDirectTagBuilder"/> 的手动组合
/// </summary>
public class OpcUaClientBuilderTests
{
    /// <summary>
    /// 构建测试 XML：含 TagCbnt（组合测点）
    /// </summary>
    private static XElement BuildCbntXml() => XElement.Parse(@"
<root>
    <Channel name='OpcUaClient-2' driver='OpcUaClient'>
        <ClientName>client-name-1</ClientName>
        <ServerOpt>
            <DiscoveryUrl>192.168.10.68</DiscoveryUrl>
            <UsePassword>true</UsePassword>
            <UserName>user-1</UserName>
            <Password>pass-1</Password>
        </ServerOpt>
    </Channel>
    <TagGrp name='g4' isEntry='true' isEnabled='true' channel='OpcUaClient-2' scanInterval='10'>
        <TagCbnt name='输入' access='RO'>
            <Tag name='放行按钮闭合状态' address='ns=4;s=|var|CODESYS Control Win V3 x64.Application.PLC_PRG.var3'></Tag>
            <Tag name='手动' address='ns=4;s=|var|CODESYS Control Win V3 x64.Application.PLC_PRG.var3'></Tag>
        </TagCbnt>
        <TagCbnt name='输出' access='RW'>
            <Tag name='绿灯' address='ns=4;s=|var|CODESYS Control Win V3 x64.Application.PLC_PRG.var3'></Tag>
            <Tag name='红灯' address='ns=4;s=|var|CODESYS Control Win V3 x64.Application.PLC_PRG.var4'></Tag>
        </TagCbnt>
    </TagGrp>
</root>");

    /// <summary>
    /// 构建测试 XML：直接测点（TagGrp 下直接挂 Tag）
    /// </summary>
    private static XElement BuildDirectTagsXml() => XElement.Parse(@"
<root>
    <Channel name='OpcUaClient-2' driver='OpcUaClient'>
        <ClientName>client-name-1</ClientName>
        <ServerOpt>
            <DiscoveryUrl>192.168.10.68</DiscoveryUrl>
            <UsePassword>true</UsePassword>
            <UserName>user-1</UserName>
            <Password>pass-1</Password>
        </ServerOpt>
    </Channel>
    <TagGrp name='g-direct' isEntry='true' isEnabled='true' channel='OpcUaClient-2' scanInterval='10'>
        <Tag name='var3' address='ns=4;s=|var|CODESYS Control Win V3 x64.Application.PLC_PRG.var3' />
        <Tag name='var4' address='ns=4;s=|var|CODESYS Control Win V3 x64.Application.PLC_PRG.var4' />
    </TagGrp>
</root>");

    #region 等效性：AddOpcUaClientSupport ≡ 手动组合三个小粒度方法

    [Fact]
    public void AddOpcUaClientSupport_EquivalentTo_ManualComposition()
    {
        // Arrange A：旧方式 AddOpcUaClientSupport
        var services1 = new ServiceCollection();
        services1.AddLogging();
        services1.AddTagsProjectServices(b => { b.AddOpcUaClientSupport(); });
        using var root1 = services1.BuildServiceProvider();
        using var scope1 = root1.CreateScope();

        // Arrange B：新方式手动组合
        var services2 = new ServiceCollection();
        services2.AddLogging();
        services2.AddTagsProjectServices(b =>
        {
            b.AddOpcUaClientChannel();
            b.AddOpcUaClientTagCbntBuilder();
            b.AddOpcUaClientDirectTagBuilder();
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
        var g1 = proj1.Tags.SelectGrp("g4");
        var g2 = proj2.Tags.SelectGrp("g4");
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
    public void AddOpcUaClientChannel_WithDirectTagBuilder_Only_LoadsDirectTags()
    {
        // Arrange：只注册 Channel + DirectTagBuilder，不注册 TagCbntBuilder
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddOpcUaClientChannel();
            b.AddOpcUaClientDirectTagBuilder();
        });

        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();

        // Act：加载只含直接测点的 XML
        using var proj = scope.ServiceProvider.GetRequiredService<ITagsProjectFactory>()
            .Create(string.Empty, BuildDirectTagsXml());

        // Assert：通道
        Assert.Single(proj.Channels);
        Assert.IsType<OpcUaClientTagChannel>(proj.Channels[0]);

        // Assert：直接测点被加载
        var g = proj.Tags.SelectGrp("g-direct");
        Assert.NotNull(g);
        Assert.True(g.Children.ContainsKey("var3"));
        Assert.True(g.Children.ContainsKey("var4"));
    }

    #endregion
}
