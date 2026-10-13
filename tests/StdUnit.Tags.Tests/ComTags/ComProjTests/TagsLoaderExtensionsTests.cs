using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StdUnit.Tags;
using StdUnit.Tags.Core;
using StdUnit.Tags.Tests.Fakes;
using Xunit;

namespace StdUnit.Tags.Tests.ComTags.ComProjTests;

/// <summary>
/// 测试 <see cref="CompositeTagsLoader"/> 通过 <see cref="TagsLoaderExtensions"/> 
/// 注册的 configure 钩子参数行为——通过公开的 <see cref="CompositeTagsLoader.LoadTagGroup"/> 入口验证
/// </summary>
public class TagsLoaderExtensionsTests
{
    #region Helper Types

    /// <summary>
    /// 可追踪的 DirectTagBuilder：Build() 使用 FakedTag 返回有效测点
    /// </summary>
    private class TraceTagBuilder : TagBuilderBase
    {
        public bool ConfigureWasCalled { get; set; }

        protected override ITag Fallback(ITagChannel channel)
        {
            return new FakedTag(TagDescriptor, channel as FakedChannel, Parent);
        }
    }

    /// <summary>
    /// 带自定义属性的 TraceDirectTagBuilder，用于测试 configure→predicate 联动
    /// </summary>
    private class ConfigurableTagBuilder : TagBuilderBase
    {
        public string? CustomProperty { get; set; }
        public bool ConfigureWasCalled { get; set; }

        protected override ITag Fallback(ITagChannel channel)
        {
            return new FakedTag(TagDescriptor, channel as FakedChannel, Parent);
        }
    }


    class MyTagCbntor : TagCbntor
    {
        public MyTagCbntor(TagDescriptor tagDescriptor, ITagCbnt tagCbnt, int tagOffset, int cacheOffset)
            : base(tagDescriptor, tagCbnt, tagOffset, cacheOffset)
        {
        }

        public override object? Value { get; set; }

        public override Task ReadAsync(CancellationToken ct) => throw new NotSupportedException();
        public override Task WriteAsync(CancellationToken ct) => throw new NotSupportedException();
    }

    /// <summary>
    /// 可追踪的 TagCbntBuilder
    /// </summary>
    private class TraceCbntBuilder : TagCbntBuilderBase
    {
        public TraceCbntBuilder() : base(new TraceCbnt(new TagCbntDescriptor())) { }
        public bool ConfigureWasCalled { get; set; }
        protected override ITagCbntor Fallback(TagDescriptor descriptor, ITagChannel channel) =>
            new MyTagCbntor(descriptor, this.TagCbnt, 0, 0);
        protected override TagCbntBuilderBase AutoLayout() => this;
    }

    /// <summary>
    /// 带自定义属性的 TraceCbntBuilder
    /// </summary>
    private class ConfigurableCbntBuilder : TagCbntBuilderBase
    {
        public ConfigurableCbntBuilder() : base(new TraceCbnt(new TagCbntDescriptor())) { }
        public string? CustomProperty { get; set; }
        protected override ITagCbntor Fallback(TagDescriptor descriptor, ITagChannel channel) =>
            new MyTagCbntor(descriptor, this.TagCbnt, 0, 0);
        protected override TagCbntBuilderBase AutoLayout() => this;
    }

    /// <summary>
    /// 极简 ITagCbnt 实现，供 TraceCbntBuilder 使用
    /// </summary>
    private class TraceCbnt : ITagCbnt
    {
        public TraceCbnt(TagCbntDescriptor descriptor) { Descriptor = descriptor; }
        public TagCbntDescriptor Descriptor { get; set; }
        public ITagGrp? Parent { get; set; }
        public IDictionary<string, ITagCbntor> Children { get; } = new Dictionary<string, ITagCbntor>();
        public ITagCbntor this[string key] => Children[key];
        public bool IsEnabled { get; set; } = true;
        public bool IsScanned { get; set; }
        public ITagChannel? Channel { get; set; }
        public string StartAddress { get; set; } = string.Empty;
        public bool IsDirty { get; set; }
        public Task ReadAsync(CancellationToken ct)
            => Task.CompletedTask;
        public Task WriteAsync(CancellationToken ct)
            => Task.CompletedTask;
    }

    #endregion

    #region AddDirectTagBuilder — 通过 LoadTagGroup 入口验证

    [Fact]
    public void AddDirectTagBuilder_ConfigureIsInvoked_ThroughLoadTagGroup()
    {
        // Arrange
        var loader = new CompositeTagsLoader();
        var channel = new FakedChannel(
            new TagChannelDescriptor { Name = "ch1", Driver = "FakedDriver" });
        var parent = new TagGrp(
            new TagGrpDescriptor { Name = "root" }, channel);

        var configureWasCalled = false;

        // Act: 注册带 configure 钩子的 DirectTagBuilder
        loader.AddDirectTagBuilder<TraceTagBuilder>(
            "FakedDriver",
            configure: b =>
            {
                configureWasCalled = true;
                b.ConfigureWasCalled = true;
            }
        );

        // Act: 通过公开的 LoadTagGroup 入口触发加载
        var tagDescriptor = new TagDescriptor { TagName = "t1", ChannelName = "ch1" };
        loader.LoadTagGroup(parent, tagDescriptor, new[] { channel });

        // Assert: configure 被调用
        Assert.True(configureWasCalled);
        // 并且子测点被正确添加到了父节点
        Assert.True(parent.Children.ContainsKey("t1"));
    }

    [Fact]
    public void AddDirectTagBuilder_ConfigureCanModifyBuilder_BeforePredicate_ThroughLoadTagGroup()
    {
        // Arrange
        var loader = new CompositeTagsLoader();
        var channel = new FakedChannel(
            new TagChannelDescriptor { Name = "ch1", Driver = "FakedDriver" });
        var parent = new TagGrp(
            new TagGrpDescriptor { Name = "root" }, channel);

        // Act: configure 设置 CustomProperty，predicate 检查它
        loader.AddDirectTagBuilder<ConfigurableTagBuilder>(
            "FakedDriver",
            configure: b =>
            {
                b.CustomProperty = "allow";
                b.ConfigureWasCalled = true;
            },
            predicate: b => b.CustomProperty == "allow"
        );

        // Act: 加载测点
        var tagDescriptor = new TagDescriptor { TagName = "t1", ChannelName = "ch1" };
        loader.LoadTagGroup(parent, tagDescriptor, new[] { channel });

        // Assert: configure 有效，predicate 放行，测点被添加
        Assert.True(parent.Children.ContainsKey("t1"));
    }

    [Fact]
    public void AddDirectTagBuilder_PredicateCanFilterAfterConfigure_ThroughLoadTagGroup()
    {
        // Arrange
        var loader = new CompositeTagsLoader();
        var channel = new FakedChannel(
            new TagChannelDescriptor { Name = "ch1", Driver = "FakedDriver" });
        var parent = new TagGrp(
            new TagGrpDescriptor { Name = "root" }, channel);

        // configure 设置 "block"，但 predicate 要求 "allow" → predicate 应拒绝
        loader.AddDirectTagBuilder<ConfigurableTagBuilder>(
            "FakedDriver",
            configure: b => b.CustomProperty = "block",
            predicate: b => b.CustomProperty == "allow"
        );

        // Assert: predicate 拒绝导致 ChooseDirectTagBuilder 返回 null → LoadDirectTag 抛异常
        var tagDescriptor = new TagDescriptor { TagName = "t1", ChannelName = "ch1" };
        Assert.Throws<TagsProjectConfigurationException>(() =>
            loader.LoadTagGroup(parent, tagDescriptor, new[] { channel }));
    }

    [Fact]
    public void AddDirectTagBuilder_ConfigureIsOptional_ThroughLoadTagGroup()
    {
        // Arrange
        var loader = new CompositeTagsLoader();
        var channel = new FakedChannel(
            new TagChannelDescriptor { Name = "ch1", Driver = "FakedDriver" });
        var parent = new TagGrp(
            new TagGrpDescriptor { Name = "root" }, channel);

        // Act: 不传 configure，仅 predicate
        loader.AddDirectTagBuilder<ConfigurableTagBuilder>(
            "FakedDriver",
            predicate: b => true
        );

        var tagDescriptor = new TagDescriptor { TagName = "t1", ChannelName = "ch1" };
        loader.LoadTagGroup(parent, tagDescriptor, new[] { channel });

        Assert.True(parent.Children.ContainsKey("t1"));
    }

    [Fact]
    public void AddDirectTagBuilder_BothConfigureAndPredicateAreOptional_ThroughLoadTagGroup()
    {
        // Arrange
        var loader = new CompositeTagsLoader();
        var channel = new FakedChannel(
            new TagChannelDescriptor { Name = "ch1", Driver = "FakedDriver" });
        var parent = new TagGrp(
            new TagGrpDescriptor { Name = "root" }, channel);

        // Act: 既不传 configure 也不传 predicate
        loader.AddDirectTagBuilder<ConfigurableTagBuilder>("FakedDriver");

        var tagDescriptor = new TagDescriptor { TagName = "t1", ChannelName = "ch1" };
        loader.LoadTagGroup(parent, tagDescriptor, new[] { channel });

        Assert.True(parent.Children.ContainsKey("t1"));
    }

    [Fact]
    public void AddDirectTagBuilder_DriverMismatch_ThroughLoadTagGroup()
    {
        // Arrange
        var loader = new CompositeTagsLoader();
        var channel = new FakedChannel(
            new TagChannelDescriptor { Name = "ch1", Driver = "OtherDriver" });
        var parent = new TagGrp(
            new TagGrpDescriptor { Name = "root" }, channel);

        // 注册的是 FakedDriver，但通道是 OtherDriver
        loader.AddDirectTagBuilder<ConfigurableTagBuilder>("FakedDriver");

        var tagDescriptor = new TagDescriptor { TagName = "t1", ChannelName = "ch1" };
        // ChooseDirectTagBuilder 没有匹配的工厂 → 抛出 TagsProjectConfigurationException
        Assert.Throws<TagsProjectConfigurationException>(() =>
            loader.LoadTagGroup(parent, tagDescriptor, new[] { channel }));
    }

    #endregion

    #region AddTagsCbntBuilder — 通过 LoadTagGroup 入口验证

    [Fact]
    public void AddTagsCbntBuilder_ConfigureIsInvoked_ThroughLoadTagGroup()
    {
        // Arrange
        var loader = new CompositeTagsLoader();
        var channel = new FakedChannel(
            new TagChannelDescriptor { Name = "ch1", Driver = "FakedDriver" });
        var parent = new TagGrp(
            new TagGrpDescriptor { Name = "root" }, channel);

        var configureWasCalled = false;

        // Act: 注册带 configure 钩子的 TagCbntBuilder
        loader.AddTagsCbntBuilder<TraceCbntBuilder>(
            "FakedDriver",
            configure: b =>
            {
                configureWasCalled = true;
                b.ConfigureWasCalled = true;
            }
        );

        // 测点组合描述符
        var cbntDescriptor = new TagCbntDescriptor
        {
            Name = "cbnt1",
            ChannelName = "ch1",
            Children = { new TagDescriptor { TagName = "child1" } }
        };

        loader.LoadTagGroup(parent, cbntDescriptor, new[] { channel });

        // Assert
        Assert.True(configureWasCalled);
        Assert.True(parent.Children.ContainsKey("cbnt1"));
    }

    [Fact]
    public void AddTagsCbntBuilder_ConfigureCanModifyBuilder_BeforePredicate_ThroughLoadTagGroup()
    {
        // Arrange
        var loader = new CompositeTagsLoader();
        var channel = new FakedChannel(
            new TagChannelDescriptor { Name = "ch1", Driver = "FakedDriver" });
        var parent = new TagGrp(
            new TagGrpDescriptor { Name = "root" }, channel);

        // configure 设置 CustomProperty，predicate 依赖它
        loader.AddTagsCbntBuilder<ConfigurableCbntBuilder>(
            "FakedDriver",
            configure: b => b.CustomProperty = "allow",
            predicate: b => b.CustomProperty == "allow"
        );

        var cbntDescriptor = new TagCbntDescriptor
        {
            Name = "cbnt1",
            ChannelName = "ch1"
        };

        loader.LoadTagGroup(parent, cbntDescriptor, new[] { channel });

        Assert.True(parent.Children.ContainsKey("cbnt1"));
    }

    [Fact]
    public void AddTagsCbntBuilder_PredicateCanFilterAfterConfigure_ThroughLoadTagGroup()
    {
        // Arrange
        var loader = new CompositeTagsLoader();
        var channel = new FakedChannel(
            new TagChannelDescriptor { Name = "ch1", Driver = "FakedDriver" });
        var parent = new TagGrp(
            new TagGrpDescriptor { Name = "root" }, channel);

        // configure 设置 block，predicate 要求 allow
        loader.AddTagsCbntBuilder<ConfigurableCbntBuilder>(
            "FakedDriver",
            configure: b => b.CustomProperty = "block",
            predicate: b => b.CustomProperty == "allow"
        );

        var cbntDescriptor = new TagCbntDescriptor
        {
            Name = "cbnt1",
            ChannelName = "ch1"
        };

        // ChooseTagCbntBuilder 返回 null → LoadTagCbnt 抛出 TagsProjectConfigurationException
        Assert.Throws<TagsProjectConfigurationException>(() =>
            loader.LoadTagGroup(parent, cbntDescriptor, new[] { channel }));
    }

    [Fact]
    public void AddTagsCbntBuilder_ConfigureIsOptional_ThroughLoadTagGroup()
    {
        // Arrange
        var loader = new CompositeTagsLoader();
        var channel = new FakedChannel(
            new TagChannelDescriptor { Name = "ch1", Driver = "FakedDriver" });
        var parent = new TagGrp(
            new TagGrpDescriptor { Name = "root" }, channel);

        // Act: 不传 configure，仅 predicate
        loader.AddTagsCbntBuilder<TraceCbntBuilder>(
            "FakedDriver",
            predicate: b => true
        );

        var cbntDescriptor = new TagCbntDescriptor
        {
            Name = "cbnt1",
            ChannelName = "ch1"
        };

        loader.LoadTagGroup(parent, cbntDescriptor, new[] { channel });
        Assert.True(parent.Children.ContainsKey("cbnt1"));
    }

    [Fact]
    public void AddTagsCbntBuilder_BothConfigureAndPredicateAreOptional_ThroughLoadTagGroup()
    {
        // Arrange
        var loader = new CompositeTagsLoader();
        var channel = new FakedChannel(
            new TagChannelDescriptor { Name = "ch1", Driver = "FakedDriver" });
        var parent = new TagGrp(
            new TagGrpDescriptor { Name = "root" }, channel);

        // Act: 既不传 configure 也不传 predicate
        loader.AddTagsCbntBuilder<TraceCbntBuilder>("FakedDriver");

        var cbntDescriptor = new TagCbntDescriptor
        {
            Name = "cbnt1",
            ChannelName = "ch1"
        };

        loader.LoadTagGroup(parent, cbntDescriptor, new[] { channel });
        Assert.True(parent.Children.ContainsKey("cbnt1"));
    }

    [Fact]
    public void AddTagsCbntBuilder_DriverMismatch_ThroughLoadTagGroup()
    {
        // Arrange
        var loader = new CompositeTagsLoader();
        var channel = new FakedChannel(
            new TagChannelDescriptor { Name = "ch1", Driver = "OtherDriver" });
        var parent = new TagGrp(
            new TagGrpDescriptor { Name = "root" }, channel);

        loader.AddTagsCbntBuilder<TraceCbntBuilder>("FakedDriver");

        var cbntDescriptor = new TagCbntDescriptor
        {
            Name = "cbnt1",
            ChannelName = "ch1"
        };

        // ChooseTagCbntBuilder 没有匹配 → 抛出 TagsProjectConfigurationException
        Assert.Throws<TagsProjectConfigurationException>(() =>
            loader.LoadTagGroup(parent, cbntDescriptor, new[] { channel }));
    }

    #endregion
}
