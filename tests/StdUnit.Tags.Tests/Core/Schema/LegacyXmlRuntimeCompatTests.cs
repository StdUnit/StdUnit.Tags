using StdUnit.Tags.S7;
using StdUnit.Tags.SimpleFiles;
using Microsoft.Extensions.DependencyInjection;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.Core.Schema;

/// <summary>
/// 运行时兼容性：老的“无命名空间前缀”XML 配置（如 Channel 下直接写 <c>&lt;IpAddr&gt;</c>）
/// 仍然能正常工作——schema 校验只是编辑期/加载期提示，
/// 运行期解析按 LocalName 索引
/// （<see cref="XElementExtensions_TagChannelDescriptor.ToTagChannelDescriptor"/>），
/// 元素带不带前缀、属于哪个命名空间都不影响。
/// </summary>
public class LegacyXmlRuntimeCompatTests
{
    private const string LegacyS7ChannelXml = """
        <Channel name="S7-1" driver="S7">
            <IpAddr>192.168.1.13</IpAddr>
            <Rack>3</Rack>
            <Slot>4</Slot>
        </Channel>
        """;

    private const string NamespacedS7ChannelXml = """
        <Channel xmlns:s7="tags:s7" name="S7-1" driver="S7">
            <s7:IpAddr>192.168.1.13</s7:IpAddr>
            <s7:Rack>3</s7:Rack>
            <s7:Slot>4</s7:Slot>
        </Channel>
        """;

    /// <summary>
    /// 老格式（无前缀）通道 XML：描述符按 LocalName 正确提取 Extras。
    /// </summary>
    [Fact]
    public void LegacyPrefixlessXml_ParsesChannelDescriptor()
    {
        var descriptor = XElement.Parse(LegacyS7ChannelXml).ToTagChannelDescriptor();

        Assert.Equal("S7-1", descriptor.Name);
        Assert.Equal("S7", descriptor.Driver);
        Assert.Equal(3, descriptor.Extras.Count);
        Assert.Equal("192.168.1.13", descriptor.Extras["IpAddr"].Value);
        Assert.Equal("3", descriptor.Extras["Rack"].Value);
        Assert.Equal("4", descriptor.Extras["Slot"].Value);
    }

    /// <summary>
    /// 老格式（无前缀）XML 转驱动描述符后，各字段值正确。
    /// </summary>
    [Fact]
    public void LegacyPrefixlessXml_ConvertsToDriverDescriptor()
    {
        var descriptor = XElement.Parse(LegacyS7ChannelXml).ToTagChannelDescriptor();
        var s7 = descriptor.ToS7TagChannelDescriptor();

        Assert.Equal("192.168.1.13", s7.IpAddr);
        Assert.Equal(3, s7.Rack);
        Assert.Equal(4, s7.Slot);
    }

    /// <summary>
    /// 老格式与命名空间格式解析结果完全一致（运行时等价）。
    /// </summary>
    [Fact]
    public void LegacyAndNamespacedXml_ProduceEquivalentDescriptors()
    {
        var legacy = XElement.Parse(LegacyS7ChannelXml).ToTagChannelDescriptor();
        var namespaced = XElement.Parse(NamespacedS7ChannelXml).ToTagChannelDescriptor();

        Assert.Equal(legacy.Name, namespaced.Name);
        Assert.Equal(legacy.Driver, namespaced.Driver);
        Assert.Equal(legacy.Extras.Count, namespaced.Extras.Count);
        foreach (var key in legacy.Extras.Keys)
        {
            Assert.True(namespaced.Extras.ContainsKey(key), $"命名空间格式缺少 Extras[{key}]");
            Assert.Equal(legacy.Extras[key].Value, namespaced.Extras[key].Value);
        }
    }

    /// <summary>
    /// 端到端：老格式（无前缀）XML 走完整项目构建（MakeProject），通道正常创建——
    /// 证明运行期不执行 schema 校验，老配置照常工作。
    /// </summary>
    [Fact]
    public void LegacyPrefixlessXml_LoadsProject_EndToEnd()
    {
        var legacyProjectXml = XElement.Parse("""
            <Project>
                <Channel name="S7-1" driver="S7">
                    <IpAddr>localhost</IpAddr>
                    <Rack>0</Rack>
                    <Slot>1</Slot>
                </Channel>
                <Channel name="file" driver="SimpleFiles">
                    <BaseDir>.</BaseDir>
                </Channel>
                <TagGrp name="g1" isEntry="true" isEnabled="true" channel="S7-1" scanInterval="10">
                    <Tag name="bit" address="DB200.0.0" type="BIT"/>
                </TagGrp>
            </Project>
            """);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddS7Support();
            b.AddSimpleFilesSupport();
        });
        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ITagsProjectFactory>();

        using var proj = factory.Create(string.Empty, legacyProjectXml);

        Assert.Equal(2, proj.Channels.Count);
        Assert.IsType<S7TagChannel>(proj.Channels[0]);
        Assert.IsType<SimpleFilesTagChannel>(proj.Channels[1]);
    }
}
