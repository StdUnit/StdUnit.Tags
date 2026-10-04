using StdUnit.Tags;
using StdUnit.Tags.ComScanner;
using StdUnit.Tags.ComScanner.Channels;
using StdUnit.Tags.ComScanner.Tags;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Text;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.ComTags.ComProjTests;

/// <summary>
/// 测试 <see cref="ComDirectTagBuilder.WithFactory"/>：
/// 通过 <see cref="TagsProject_Extensions.AddComScannerDirectTagBuilder"/> 的 configure 钩子注入创建委托，
/// 无需编写自定义 DirectTagBuilder 子类，即可扩展串口测点构建逻辑。
/// </summary>
public class ComTagBuilderWithFactoryTests
{
    #region Helper

    /// <summary>
    /// 构建测试 XML：包含一个 STR 测点
    /// </summary>
    private static XElement BuildTestXml()
    {
        return XElement.Parse(@"
<root>
    <Channel name='COM-1' driver='COM'>
        <Port>COM1</Port>
        <BaudRate>9600</BaudRate>
        <Parity>None</Parity>
        <DataBits>8</DataBits>
        <StopBits>One</StopBits>
    </Channel>
    <TagGrp name='g' isEntry='true'>
        <Tag name='扫码枪1' channel='COM-1' type='STR' access='RO'></Tag>
    </TagGrp>
</root>");
    }

    /// <summary>
    /// 构建包含自定义 AnyLoad 测点的测试 XML
    /// </summary>
    private static XElement BuildAnyLoadTestXml()
    {
        return XElement.Parse(@"
<root>
    <Channel name='COM-1' driver='COM'>
        <Port>COM1</Port>
        <BaudRate>9600</BaudRate>
        <Parity>None</Parity>
        <DataBits>8</DataBits>
        <StopBits>One</StopBits>
    </Channel>
    <TagGrp name='g' isEntry='true'>
        <Tag name='扫码枪1' channel='COM-1' type='STR' access='RO'></Tag>
        <Tag name='anyload-1' channel='COM-1' type='AnyLoad'></Tag>
    </TagGrp>
</root>");
    }

    #endregion

    /// <summary>
    /// 工厂委托被优先调用
    /// </summary>
    [Fact]
    public void WithFactory_DelegateIsInvoked_InsteadOfInternalLogic()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddComScannerChannel();

            // 故意设置一个工厂来创建 ComWriteOnlyTag<string>，以验证委托被优先调用，而非内部逻辑的 ComReadOnlyTag<string>
            b.AddComScannerDirectTagBuilder(
                configure: b => b.WithFactory(
                    (descriptor, thisChannel, container) => new ComWriteOnlyTag<string>(
                        descriptor,
                        thisChannel as ComChannelBase<string>,
                        container,
                        converter: str => Encoding.UTF8.GetBytes(str)
                    )
                ),
                predicate: bd => bd.TagDescriptor.TagName == "扫码枪1"
            );
        });

        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();

        // Act
        using var proj = scope.ServiceProvider.MakeProject(null, BuildTestXml());

        // Assert：工厂委托创建了 ComWriteOnlyTag ，而非内部逻辑的 ComReadOnlyTag
        var g = proj.Tags.SelectGrp("g");
        Assert.NotNull(g);
        var tag = g.SelectTag("扫码枪1");
        Assert.NotNull(tag);
        Assert.IsType<ComWriteOnlyTag<string>>(tag);
    }



    /// <summary>
    /// WithFactory 委托返回 null 时回退内部逻辑
    /// </summary>
    [Fact]
    public void WithFactory_DelegateReturnsNull_FallsBackToInternalLogic()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddComScannerChannel();

            // 委托永远返回 null → 应回退内部逻辑
            b.AddComScannerDirectTagBuilder(
                configure: b => b.WithFactory((_, _, _) => null!)
            );
        });

        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();

        // Act
        using var proj = scope.ServiceProvider.MakeProject(null, BuildTestXml());

        // Assert：STR 测点由内部逻辑创建 ComReadOnlyTag<string>（access=RO）
        var g = proj.Tags.SelectGrp("g");
        Assert.NotNull(g);
        var tag = g.SelectTag("扫码枪1");
        Assert.NotNull(tag);
        Assert.IsType<ComReadOnlyTag<string>>(tag);
        Assert.Equal(BuiltinTagKinds.STR, tag.TagKind());
    }


    /// <summary>
    /// WithFactory 扩展非 STR 类型（不需要自定义 DirectTagBuilder 子类）
    /// </summary>
    [Fact]
    public void WithFactory_ExtendsNonStrTagKind_WithoutCustomBuilderClass()
    {
        // Arrange：与 ComProjTagSelectorTests 中 AnyLoadComDirectTagBuilder 等价的委托实现，
        // 但无需编写自定义 DirectTagBuilder 子类
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddComScannerChannel();

            // 自定义 AnyLoad 测点创建逻辑
            b.AddComScannerDirectTagBuilder(
                configure: b => b.WithFactory((descriptor, channel, container) =>
                {
                    if (channel is not ComChannelBase<string> com)
                    {
                        throw new InvalidCastException($"测点({descriptor.TagName})当前通道必须是{nameof(ComChannelBase<string>)}！实际={channel?.GetType()}");
                    }

                    var accessMode = descriptor.AccessMode ?? container.Map(
                        cbnt => cbnt.SearchAccessMode(),
                        grp => grp.SearchAccessMode());

                    return accessMode switch
                    {
                        TagAccessMode.WO => new ComWriteOnlyTag<string>(descriptor, com, container, converter: str => Encoding.UTF8.GetBytes(str)),
                        _ => new ComReadOnlyTag<string>(descriptor, com, container),
                    };
                }),
                predicate: bd => bd.TagDescriptor.TagKind == "AnyLoad"
            );

            // 基本 STR 测点走默认逻辑
            b.AddComScannerDirectTagBuilder();
        });

        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();

        // Act
        using var proj = scope.ServiceProvider.MakeProject(null, BuildAnyLoadTestXml());
        var g = proj.Tags.SelectGrp("g");
        Assert.NotNull(g);

        var anyload = g.SelectTag("anyload-1");
        Assert.NotNull(anyload);
        Assert.Equal("AnyLoad", anyload.TagKind());
        Assert.IsType<ComReadOnlyTag<string>>(anyload);
        Assert.Equal(proj.Channels[0], anyload.Channel);

        // STR 测点仍由内部逻辑处理
        var strTag = g.SelectTag("扫码枪1");
        Assert.NotNull(strTag);
        Assert.IsType<ComReadOnlyTag<string>>(strTag);
    }



    /// <summary>
    /// 未设置 WithFactory 时行为不变
    /// </summary>
    [Fact]
    public void WithoutFactory_BehaviorUnchanged()
    {
        // Arrange：不设置 WithFactory
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddComScannerChannel();
            b.AddComScannerDirectTagBuilder();
        });

        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();

        // Act
        using var proj = scope.ServiceProvider.MakeProject(null, BuildTestXml());

        // Assert：STR 测点正常加载
        var g = proj.Tags.SelectGrp("g");
        Assert.NotNull(g);
        var tag = g.SelectTag("扫码枪1");
        Assert.NotNull(tag);
        Assert.IsType<ComReadOnlyTag<string>>(tag);
    }

}
