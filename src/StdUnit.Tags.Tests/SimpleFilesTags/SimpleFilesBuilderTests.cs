using StdUnit.Tags;
using StdUnit.Tags.SimpleFiles;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.SimpleFilesTags;

/// <summary>
/// 测试 <see cref="TagsProject_Extensions"/> 重构后，
/// <see cref="TagsProject_Extensions.AddSimpleFilesChannel"/> +
/// <see cref="TagsProject_Extensions.AddSimpleFilesDirectTagBuilder"/> 多次注册的场景
/// </summary>
public class SimpleFilesBuilderTests
{
    /// <summary>
    /// 构造用于测试的简易 XML
    /// </summary>
    private static XElement BuildTestXml()
    {
        return XElement.Parse(@"
<root>
    <Channel name='SimpleFiles-1' driver='SimpleFiles'>
        <BaseDir>.</BaseDir>
    </Channel>
    <TagGrp name='g' isEntry='true' channel='SimpleFiles-1' scanInterval='0'>
        <Tag name='int-a' address='int.txt' type='INT32' />
        <Tag name='int-b' address='int2.txt' type='INT32' />
    </TagGrp>
</root>");
    }

    #region AddSimpleFilesChannel + 单次 AddSimpleFilesDirectTagBuilder（等价于 AddSimpleFilesSupport）

    [Fact]
    public void AddSimpleFilesChannel_WithSingleAddSimpleFilesDirectTagBuilder_ShouldLoadTags()
    {
        // Arrange: AddSimpleFilesChannel 一次 + AddSimpleFilesDirectTagBuilder 一次
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddSimpleFilesChannel();
            b.AddSimpleFilesDirectTagBuilder();
        });

        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();
        var sp = scope.ServiceProvider;

        var xml = BuildTestXml();

        // Act
        using var proj = sp.MakeProject(null, xml);

        // Assert
        Assert.Single(proj.Channels);
        Assert.Equal(SimpleFilesNames.DriverName, proj.Channels[0].Driver());

        var g = proj.Tags.SelectGrp("g");
        Assert.NotNull(g);

        var tag1 = g.SelectTag("int-a");
        Assert.NotNull(tag1);
        Assert.Equal("int-a", tag1.TagName());
        Assert.Equal(BuiltinTagKinds.INT32, tag1.TagKind());

        var tag2 = g.SelectTag("int-b");
        Assert.NotNull(tag2);
        Assert.Equal("int-b", tag2.TagName());
        Assert.Equal(BuiltinTagKinds.INT32, tag2.TagKind());
    }

    [Fact]
    public void AddSimpleFilesChannel_WithSingleAddSimpleFilesDirectTagBuilder_ConfigureIsInvoked()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var configureWasCalled = false;

        services.AddTagsProjectServices(b =>
        {
            b.AddSimpleFilesChannel();
            b.AddSimpleFilesDirectTagBuilder(
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

    #region AddSimpleFilesChannel + 多次 AddSimpleFilesDirectTagBuilder

    [Fact]
    public void AddSimpleFilesChannel_WithMultipleAddSimpleFilesDirectTagBuilder_AllConfigureInvoked()
    {
        // Arrange: channel 一次 + 两次 DirectTagBuilder，验证注册顺序
        var services = new ServiceCollection();
        services.AddLogging();
        var configure1Called = false;
        var configure2Called = false;

        services.AddTagsProjectServices(b =>
        {
            b.AddSimpleFilesChannel();

            b.AddSimpleFilesDirectTagBuilder(
                configure: _ => { configure1Called = true; }
            );

            b.AddSimpleFilesDirectTagBuilder(
                configure: _ => { configure2Called = true; }
            );
        });

        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();
        var sp = scope.ServiceProvider;

        var xml = BuildTestXml();

        // Act
        using var proj = sp.MakeProject(null, xml);

        // Assert: 第一个 builder 无 predicate，处理了所有标签，第二个 builder 不应被用到
        Assert.True(configure1Called);
        Assert.False(configure2Called, "第二个 builder 不应被用到（第一个 builder 无 predicate，已处理所有合适的标签）");

        var g = proj.Tags.SelectGrp("g");
        Assert.NotNull(g);
        Assert.NotNull(g.SelectTag("int-a"));
        Assert.NotNull(g.SelectTag("int-b"));
    }

    [Fact]
    public void AddSimpleFilesChannel_WithMultipleAddSimpleFilesDirectTagBuilder_DifferentPredicates_ShouldRouteToCorrectBuilder()
    {
        // Arrange:
        //   第一个 builder → 只接受 int-a
        //   第二个 builder → 只接受 int-b
        // 验证路由正确：每个标签被对应的 builder 处理
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddTagsProjectServices(b =>
        {
            b.AddSimpleFilesChannel();

            b.AddSimpleFilesDirectTagBuilder(
                predicate: bd => bd.TagDescriptor.TagName == "int-a"
            );

            b.AddSimpleFilesDirectTagBuilder(
                predicate: bd => bd.TagDescriptor.TagName == "int-b"
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

        var tag1 = g.SelectTag("int-a");
        Assert.NotNull(tag1);
        Assert.Equal("int-a", tag1.TagName());

        var tag2 = g.SelectTag("int-b");
        Assert.NotNull(tag2);
        Assert.Equal("int-b", tag2.TagName());
    }

    [Fact]
    public void AddSimpleFilesChannel_WithMultipleAddSimpleFilesDirectTagBuilder_FirstBuilderRejectsSome_SecondBuilderHandlesRest()
    {
        // Arrange:
        //   第一个 builder: predicate 拒绝所有（返回 false）
        //   第二个 builder: 无 predicate，处理所有
        // 验证：所有标签由第二个 builder 处理
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddTagsProjectServices(b =>
        {
            b.AddSimpleFilesChannel();

            // 第一个 builder：predicate 永远返回 false
            b.AddSimpleFilesDirectTagBuilder(
                predicate: _ => false
            );

            // 第二个 builder：无 predicate，接手处理
            b.AddSimpleFilesDirectTagBuilder();
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
        Assert.NotNull(g.SelectTag("int-a"));
        Assert.NotNull(g.SelectTag("int-b"));
    }

    [Fact]
    public void AddSimpleFilesChannel_WithMultipleAddSimpleFilesDirectTagBuilder_FirstBuilderHandlesPartial_SecondBuilderHandlesRemaining()
    {
        // Arrange:
        //   第一个 builder: 处理 int-a（用 predicate 限制为 TagName 以 "a" 结尾的标签）
        //   第二个 builder: 处理剩余标签（无 predicate）
        // 验证两个 builder 各司其职
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddTagsProjectServices(b =>
        {
            b.AddSimpleFilesChannel();

            // 第一个 builder：只处理 TagName 以 "a" 结尾的标签
            b.AddSimpleFilesDirectTagBuilder(
                predicate: bd => bd.TagDescriptor.TagName.EndsWith("a")
            );

            // 第二个 builder：无 predicate，处理其他所有标签
            b.AddSimpleFilesDirectTagBuilder();
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
        Assert.NotNull(g.SelectTag("int-a"));
        Assert.NotNull(g.SelectTag("int-b"));
    }

    #endregion

    #region AddSimpleFilesSupport 等效性

    [Fact]
    public void AddSimpleFilesSupport_EquivalentTo_AddSimpleFilesChannel_Plus_AddSimpleFilesDirectTagBuilder()
    {
        // Arrange: 用 AddSimpleFilesSupport（旧方式）
        var services1 = new ServiceCollection();
        services1.AddLogging();
        services1.AddTagsProjectServices(b => { b.AddSimpleFilesSupport(); });
        using var root1 = services1.BuildServiceProvider();
        using var scope1 = root1.CreateScope();

        // Arrange: 用 AddSimpleFilesChannel + AddSimpleFilesDirectTagBuilder（新拆分方式）
        var services2 = new ServiceCollection();
        services2.AddLogging();
        services2.AddTagsProjectServices(b =>
        {
            b.AddSimpleFilesChannel();
            b.AddSimpleFilesDirectTagBuilder();
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

    #endregion
}
