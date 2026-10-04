using StdUnit.Tags;
using StdUnit.Tags.ComScanner;
using StdUnit.Tags.ComScanner.Channels;
using StdUnit.Tags.ComScanner.Tags;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.ComTags.ComProjTests;

/// <summary>
/// 测试 <see cref="TagsProject_Extensions"/> 重构后，
/// <see cref="TagsProject_Extensions.AddComScannerChannel"/> +
/// <see cref="TagsProject_Extensions.AddComScannerDirectTagBuilder"/> 多次注册的场景
/// </summary>
public class ComScannerBuilderTests
{
    /// <summary>
    /// 构造用于测试的简易 XML
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
        <Tag name='扫码枪2' channel='COM-1' type='STR' access='RO'></Tag>
    </TagGrp>
</root>");
    }

    #region AddComScannerChannel + 单次 AddComScannerDirectTagBuilder（等价于 AddComScannerSupport）

    [Fact]
    public void AddComScannerChannel_WithSingleAddComScannerDirectTagBuilder_ShouldLoadTags()
    {
        // Arrange: AddComScannerChannel 一次 + AddComScannerDirectTagBuilder 一次
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddComScannerChannel();
            b.AddComScannerDirectTagBuilder();
        });

        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();
        var sp = scope.ServiceProvider;

        var xml = BuildTestXml();

        // Act
        using var proj = sp.MakeProject(null, xml);

        // Assert
        Assert.Single(proj.Channels);
        Assert.IsType<LineBasedComChannel>(proj.Channels[0]);

        var g = proj.Tags.SelectGrp("g");
        Assert.NotNull(g);

        var tag1 = g.SelectTag("扫码枪1");
        Assert.NotNull(tag1);
        Assert.Equal("扫码枪1", tag1.TagName());
        Assert.Equal(BuiltinTagKinds.STR, tag1.TagKind());
        Assert.IsType<ComReadOnlyTag<string>>(tag1);
        Assert.Same(proj.Channels[0], tag1.Channel);

        var tag2 = g.SelectTag("扫码枪2");
        Assert.NotNull(tag2);
        Assert.Equal("扫码枪2", tag2.TagName());
        Assert.Equal(BuiltinTagKinds.STR, tag2.TagKind());
        Assert.IsType<ComReadOnlyTag<string>>(tag2);
        Assert.Same(proj.Channels[0], tag2.Channel);
    }

    [Fact]
    public void AddComScannerChannel_WithSingleAddComScannerDirectTagBuilder_ConfigureIsInvoked()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var configureWasCalled = false;

        services.AddTagsProjectServices(b =>
        {
            b.AddComScannerChannel();
            b.AddComScannerDirectTagBuilder(
                configure: _ => { configureWasCalled = true; }
            );
        });

        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();
        var sp = scope.ServiceProvider;

        var xml = BuildTestXml();

        // Act
        using var proj = sp.MakeProject(null, xml);

        // Assert
        Assert.True(configureWasCalled);
    }

    #endregion

    #region AddComScannerChannel + 多次 AddComScannerDirectTagBuilder

    [Fact]
    public void AddComScannerChannel_WithMultipleAddComScannerDirectTagBuilder_AllConfigureInvoked()
    {
        // Arrange: channel 一次 + 两次 DirectTagBuilder，验证第一个匹配的 builder 会处理所有标签，第二个 builder 不会被调用
        var services = new ServiceCollection();
        services.AddLogging();
        var configure1Called = false;
        var configure2Called = false;

        services.AddTagsProjectServices(b =>
        {
            b.AddComScannerChannel();

            b.AddComScannerDirectTagBuilder(
                configure: _ => { configure1Called = true; }
            );

            b.AddComScannerDirectTagBuilder(
                configure: _ => { configure2Called = true; }
            );
        });

        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();
        var sp = scope.ServiceProvider;

        var xml = BuildTestXml();

        // Act
        using var proj = sp.MakeProject(null, xml);

        // Assert: 所有标签都应该被第一个匹配的 builder 加载
        //（默认不加 predicate 时第一个 builder 会处理所有 STR 标签）
        Assert.True(configure1Called);
        Assert.False(configure2Called, "第二个 builder 不应被用到（第一个 builder 无 predicate，已处理所有合适合适的标签）");

        var g = proj.Tags.SelectGrp("g");
        Assert.NotNull(g);
        Assert.NotNull(g.SelectTag("扫码枪1"));
        Assert.NotNull(g.SelectTag("扫码枪2"));
    }

    [Fact]
    public void AddComScannerChannel_WithMultipleAddComScannerDirectTagBuilder_DifferentPredicates_ShouldRouteToCorrectBuilder()
    {
        // Arrange: 
        //   第一个 builder → 只接受 扫码枪1
        //   第二个 builder → 只接受 扫码枪2
        // 验证路由正确：每个标签被对应的 builder 处理
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddTagsProjectServices(b =>
        {
            b.AddComScannerChannel();

            b.AddComScannerDirectTagBuilder(
                predicate: bd => bd.TagDescriptor.TagName == "扫码枪1"
            );

            b.AddComScannerDirectTagBuilder(
                predicate: bd => bd.TagDescriptor.TagName == "扫码枪2"
            );
        });

        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();
        var sp = scope.ServiceProvider;

        var xml = BuildTestXml();

        // Act
        using var proj = sp.MakeProject(null, xml);

        // Assert
        var g = proj.Tags.SelectGrp("g");
        Assert.NotNull(g);

        var tag1 = g.SelectTag("扫码枪1");
        Assert.NotNull(tag1);
        Assert.Equal("扫码枪1", tag1.TagName());

        var tag2 = g.SelectTag("扫码枪2");
        Assert.NotNull(tag2);
        Assert.Equal("扫码枪2", tag2.TagName());
    }

    [Fact]
    public void AddComScannerChannel_WithMultipleAddComScannerDirectTagBuilder_FirstBuilderRejectsSome_SecondBuilderHandlesRest()
    {
        // Arrange:
        //   第一个 builder: predicate 拒绝所有（返回 false）
        //   第二个 builder: 无 predicate，处理所有
        // 验证：所有标签由第二个 builder 处理
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddTagsProjectServices(b =>
        {
            b.AddComScannerChannel();

            // 第一个 builder：predicate 永远返回 false
            b.AddComScannerDirectTagBuilder(
                predicate: _ => false
            );

            // 第二个 builder：无 predicate，接手处理
            b.AddComScannerDirectTagBuilder();
        });

        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();
        var sp = scope.ServiceProvider;

        var xml = BuildTestXml();

        // Act
        using var proj = sp.MakeProject(null, xml);

        // Assert: 所有标签都应该被加载（由第二个 builder 处理）
        var g = proj.Tags.SelectGrp("g");
        Assert.NotNull(g);
        Assert.NotNull(g.SelectTag("扫码枪1"));
        Assert.NotNull(g.SelectTag("扫码枪2"));
    }

    [Fact]
    public void AddComScannerChannel_WithMultipleAddComScannerDirectTagBuilder_FirstBuilderHandlesPartial_SecondBuilderHandlesRemaining()
    {
        // Arrange:
        //   第一个 builder: 处理 扫码枪1（用 predicate 限制为 TagName 以 "1" 结尾的标签）
        //   第二个 builder: 处理剩余标签（无 predicate）
        // 验证两个 builder 各司其职
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddTagsProjectServices(b =>
        {
            b.AddComScannerChannel();

            // 第一个 builder：只处理 TagName 包含 "1" 的标签
            b.AddComScannerDirectTagBuilder(
                predicate: bd => bd.TagDescriptor.TagName.Contains("1")
            );

            // 第二个 builder：无 predicate，处理其他所有标签
            b.AddComScannerDirectTagBuilder();
        });

        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();
        var sp = scope.ServiceProvider;

        var xml = BuildTestXml();

        // Act
        using var proj = sp.MakeProject(null, xml);

        // Assert
        var g = proj.Tags.SelectGrp("g");
        Assert.NotNull(g);
        Assert.NotNull(g.SelectTag("扫码枪1"));
        Assert.NotNull(g.SelectTag("扫码枪2"));
    }

    #endregion


    /// <summary>
    /// 验证 AddComScannerSupport 等价于 AddComScannerChannel + AddComScannerDirectTagBuilder
    /// </summary>
    [Fact]
    public void AddComScannerSupport_EquivalentTo_AddComScannerChannel_Plus_AddComScannerDirectTagBuilder()
    {
        // Arrange: 用 AddComScannerSupport
        var services1 = new ServiceCollection();
        services1.AddLogging();
        services1.AddTagsProjectServices(b => { b.AddComScannerSupport(); });
        using var root1 = services1.BuildServiceProvider();
        using var scope1 = root1.CreateScope();

        // Arrange: 用 AddComScannerChannel + AddComScannerDirectTagBuilder（新拆分方式）
        var services2 = new ServiceCollection();
        services2.AddLogging();
        services2.AddTagsProjectServices(b =>
        {
            b.AddComScannerChannel();
            b.AddComScannerDirectTagBuilder();
        });
        using var root2 = services2.BuildServiceProvider();
        using var scope2 = root2.CreateScope();

        var xml = BuildTestXml();

        // Act
        using var proj1 = scope1.ServiceProvider.MakeProject(null, xml);
        using var proj2 = scope2.ServiceProvider.MakeProject(null, xml);

        // Assert: 结果一致
        Assert.Equal(proj1.Channels.Count, proj2.Channels.Count);
        Assert.Equal(proj1.Tags.Children.Count, proj2.Tags.Children.Count);

        var g1 = proj1.Tags.SelectGrp("g");
        var g2 = proj2.Tags.SelectGrp("g");
        Assert.NotNull(g1);
        Assert.NotNull(g2);

        foreach (var child in g1.Children)
        {
            Assert.True(g2.Children.ContainsKey(child.Key));
        }
    }


}
