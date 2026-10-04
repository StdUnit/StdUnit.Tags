using StdUnit.Tags;
using StdUnit.Tags.S7;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.Core.CrossReferences;

/// <summary>
/// 测试 <see cref="ChannelDriverFactoryValidator"/>（由 <c>EnableCrossReferenceValidation()</c> 启用）：
/// &lt;Channel&gt; 的 driver 属性必须对应已注册的通道工厂。
/// </summary>
public class ChannelDriverFactoryValidatorTests
{
    /// <summary>已注册 driver 的 Channel 通过校验。</summary>
    [Fact]
    public void Validate_RegisteredDriver_NoErrors()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b => b.AddS7Support());
        using var root = services.BuildServiceProvider();
        var factory = root.GetRequiredService<ITagChannelFactory>();

        var validator = new ChannelDriverFactoryValidator(factory);

        var xml = XElement.Parse("""
            <Project>
                <Channel name="S7-1" driver="S7">
                    <IpAddr>localhost</IpAddr>
                </Channel>
            </Project>
            """);
        validator.Validate(xml); // 不抛即通过
    }

    /// <summary>未注册 driver 的 Channel 在加载期被拒绝。</summary>
    [Fact]
    public void Validate_UnregisteredDriver_Throws()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b => b.AddS7Support());
        using var root = services.BuildServiceProvider();
        var factory = root.GetRequiredService<ITagChannelFactory>();

        var validator = new ChannelDriverFactoryValidator(factory);

        var xml = XElement.Parse("""
            <Project>
                <Channel name="c1" driver="NoSuchDriver">
                    <IpAddr>localhost</IpAddr>
                </Channel>
            </Project>
            """);

        var ex = Assert.Throws<TagsProjectSchemaException>(() => validator.Validate(xml));
        Assert.Contains("NoSuchDriver", ex.Message);
        Assert.Contains("c1", ex.Message);
        Assert.Contains("S7", ex.Message); // 已注册驱动列表里有 S7
    }

    /// <summary>端到端：EnableCrossReferenceValidation 后，未注册 driver 在 MakeProject 被拒绝。</summary>
    [Fact]
    public void MakeProject_WithCrossReferenceValidation_RejectsUnregisteredDriver()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddS7Support();
            b.EnableCrossReferenceValidation();
        });
        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ITagsProjectFactory>();

        var xml = XElement.Parse("""
            <Project>
                <Channel name="c1" driver="NoSuchDriver">
                    <IpAddr>localhost</IpAddr>
                </Channel>
            </Project>
            """);

        Assert.Throws<TagsProjectSchemaException>(() => factory.Create(string.Empty, xml));
    }

    /// <summary>端到端：合法 driver 正常构建（含 channel 引用校验器一起启用）。</summary>
    [Fact]
    public void MakeProject_WithCrossReferenceValidation_AcceptsRegisteredDriver()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddS7Support();
            b.EnableCrossReferenceValidation();
        });
        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ITagsProjectFactory>();

        var xml = XElement.Parse("""
            <Project>
                <Channel name="S7-1" driver="S7">
                    <IpAddr>localhost</IpAddr>
                </Channel>
                <TagGrp name="g1" isEntry="true" channel="S7-1">
                    <Tag name="bit" address="DB200.0.0" type="BIT"/>
                </TagGrp>
            </Project>
            """);

        using var proj = factory.Create(string.Empty, xml);
        Assert.NotNull(proj);
    }
}
