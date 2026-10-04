using StdUnit.Tags;
using StdUnit.Tags.Tests.Fakes;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StdUnit.Tags.Tests.Core.TagCbnts;

/// <summary>
/// 针对 <see cref="TagCbntBuilderBase"/> 基类行为的单元测试：
/// AddTags 的「WithFactory 委托优先 → CreateDefaultTagCbntor 回退」调度、委托参数传递、fluent 配置
/// </summary>
public class TagCbntBuilderBaseTests
{
    #region Helper Types

    /// <summary>
    /// 极简 ITagCbnt 实现
    /// </summary>
    private class FakeCbnt : ITagCbnt
    {
        public FakeCbnt(TagCbntDescriptor descriptor) { Descriptor = descriptor; }
        public TagCbntDescriptor Descriptor { get; set; }
        public ITagGrp? Parent { get; set; }
        public IDictionary<string, ITagCbntor> Children { get; } = new Dictionary<string, ITagCbntor>();
        public ITagCbntor this[string key] => Children[key];
        public bool IsEnabled { get; set; } = true;
        public bool IsScaned { get; set; }
        public ITagChannel? Channel { get; set; }
        public string StartAddress { get; set; } = string.Empty;
        public bool IsDirty { get; set; }
        public Task ReadAsync(CancellationToken ct) => Task.CompletedTask;
        public Task WriteAsync(CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>
    /// 极简 ITagCbntor 实现（继承 TagCbntor，仅需实现 Value）
    /// </summary>
    private class FakeTagCbntor : TagCbntor
    {
        public FakeTagCbntor(TagDescriptor descriptor, ITagCbnt cbnt, int tagOffset = 0, int cacheOffset = 0)
            : base(descriptor, cbnt, tagOffset, cacheOffset)
        {
        }

        public override object? Value { get; set; }

        public override Task ReadAsync(CancellationToken ct) => throw new NotSupportedException();
        public override Task WriteAsync(CancellationToken ct) => throw new NotSupportedException();
    }

    /// <summary>
    /// 可追踪 CreateDefaultTagCbntor 调用情况的测试子类
    /// </summary>
    private class TraceableCbntBuilder : TagCbntBuilderBase
    {
        public TraceableCbntBuilder() : base(new FakeCbnt(new TagCbntDescriptor())) { }

        public int FallbackCallCount { get; private set; }
        public TagDescriptor? LastDefaultDescriptor { get; private set; }
        public ITagChannel? LastDefaultChannel { get; private set; }

        protected override ITagCbntor Fallback(TagDescriptor descriptor, ITagChannel channel)
        {
            FallbackCallCount++;
            LastDefaultDescriptor = descriptor;
            LastDefaultChannel = channel;
            return new FakeTagCbntor(descriptor, this.TagCbnt);
        }

        protected override TagCbntBuilderBase AutoLayout() => this;
    }

    class FakeT1TagCbntor : TagCbntor
    {
        public FakeT1TagCbntor(TagDescriptor tagDescriptor, ITagCbnt tagCbnt, int tagOffset, int cacheOffset)
            : base(tagDescriptor, tagCbnt, tagOffset, cacheOffset)
        {
        }

        public override object? Value { get; set; }

        public override Task ReadAsync(CancellationToken ct) => throw new NotSupportedException();
        public override Task WriteAsync(CancellationToken ct) => throw new NotSupportedException();
    }

    #endregion

    #region Helper

    private static FakedChannel CreateChannel() =>
        new FakedChannel(new TagChannelDescriptor { Name = "fake-channel" });

    private static TagDescriptor CreateDescriptor(string tagName = "t1") =>
        new TagDescriptor { TagName = tagName };

    #endregion

    #region AddTags 调度：委托优先 → 回退

    [Fact]
    public void AddTags_WithoutFactory_InvokesFallback_AndAddsToChildren()
    {
        // Arrange
        var channel = CreateChannel();
        var builder = new TraceableCbntBuilder();

        // Act
        builder.AddTags(new[] { CreateDescriptor("t1"), CreateDescriptor("t2") }, channel);

        // Assert：Fallback 被调用两次，两个测点都被加入 Children
        Assert.Equal(2, builder.FallbackCallCount);
        Assert.True(builder.TagCbnt.Children.ContainsKey("t1"));
        Assert.True(builder.TagCbnt.Children.ContainsKey("t2"));
    }

    [Fact]
    public void AddTags_WithFactory_DelegatePreferredOverDefault()
    {
        // Arrange
        var channel = CreateChannel();
        var builder = new TraceableCbntBuilder();

        var delegateCalled = false;
        builder.WithFactory((descriptor, _, b) =>
        {
            delegateCalled = true;
            return new FakeTagCbntor(descriptor, builder.TagCbnt);
        });

        // Act
        builder.AddTags(new[] { CreateDescriptor() }, channel);

        // Assert：委托被优先调用，默认逻辑未被调用
        Assert.True(delegateCalled);
        Assert.Equal(0, builder.FallbackCallCount);
        Assert.True(builder.TagCbnt.Children.ContainsKey("t1"));
    }

    [Fact]
    public void AddTags_WithFactoryReturnsNull_FallsBackToDefault()
    {
        // Arrange
        var channel = CreateChannel();
        var builder = new TraceableCbntBuilder();

        builder.WithFactory((_, _, _) => null!);

        // Act
        builder.AddTags(new[] { CreateDescriptor() }, channel);

        // Assert：委托返回 null → 回退默认逻辑
        Assert.Equal(1, builder.FallbackCallCount);
        Assert.True(builder.TagCbnt.Children.ContainsKey("t1"));
    }
    #endregion

    #region 委托参数传递

    [Fact]
    public void AddTags_DefaultReceivesCorrectArguments()
    {
        // Arrange
        var channel = CreateChannel();
        var descriptor = CreateDescriptor("myTag");
        var builder = new TraceableCbntBuilder();

        // Act
        builder.AddTags(new[] { descriptor }, channel);

        // Assert
        Assert.Same(descriptor, builder.LastDefaultDescriptor);
        Assert.Same(channel, builder.LastDefaultChannel);
    }

    [Fact]
    public void AddTags_DelegateReceivesCorrectArguments()
    {
        // Arrange
        var channel = CreateChannel();
        var descriptor = CreateDescriptor("myTag");
        var builder = new TraceableCbntBuilder();

        builder.WithFactory((d, ch, b) =>
        {
            Assert.Same(descriptor, d);
            Assert.Same(channel, ch);
            return new FakeTagCbntor(d, builder.TagCbnt);
        });

        // Act（委托内断言在调用时执行）
        builder.AddTags(new[] { descriptor }, channel);
    }

    #endregion

    #region Fluent 配置

    [Fact]
    public void WithFactory_ReturnsSelf_ForChaining()
    {
        var builder = new TraceableCbntBuilder();
        var result = builder.WithFactory((_, _, _) => null!);
        Assert.Same(builder, result);
    }

    [Fact]
    public void AddTags_ReturnsSelf_ForChaining()
    {
        var builder = new TraceableCbntBuilder();
        var result = builder.AddTags(new[] { CreateDescriptor() }, CreateChannel());
        Assert.Same(builder, result);
    }

    #endregion
}
