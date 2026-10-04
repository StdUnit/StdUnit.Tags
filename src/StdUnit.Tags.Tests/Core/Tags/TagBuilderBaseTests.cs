using StdUnit.Tags;
using StdUnit.Tags.Tests.Fakes;
using System;
using Xunit;

namespace StdUnit.Tags.Tests.Core.Tags;

/// <summary>
/// 针对 <see cref="TagBuilderBase"/> 基类行为的单元测试：
/// Build 的「WithFactory 委托优先 → Fallback 回退」调度、委托参数传递、fluent 配置
/// </summary>
public class TagBuilderBaseTests
{
    #region Helper Types

    /// <summary>
    /// 可追踪 Fallback 调用情况的测试子类
    /// </summary>
    private class TraceableTagBuilder : TagBuilderBase
    {
        public int FallbackCallCount { get; private set; }
        public ITagChannel? LastFallbackChannel { get; private set; }

        protected override ITag Fallback(ITagChannel channel)
        {
            FallbackCallCount++;
            LastFallbackChannel = channel;
            return new FakedTag(TagDescriptor, channel as FakedChannel, Parent);
        }
    }

    #endregion

    #region Helper

    private static FakedChannel CreateChannel() =>
        new FakedChannel(new TagChannelDescriptor { Name = "fake-channel" });

    private static TagGrp CreateParent(FakedChannel channel) =>
        new TagGrp(new TagGrpDescriptor { Name = "root" }, channel);

    private static TagDescriptor CreateDescriptor() =>
        new TagDescriptor { TagName = "t1" };

    private static TraceableTagBuilder CreateBuilder(
        FakedChannel? channel = null,
        ITagGrp? parent = null,
        TagDescriptor? descriptor = null)
    {
        var builder = new TraceableTagBuilder();
        if (descriptor is not null) builder.WithTagDescriptor(descriptor);
        if (parent is not null) builder.WithParent(parent);
        if (channel is not null) builder.WithChannel(channel);
        return builder;
    }

    #endregion

    #region Build 调度：委托优先 → Fallback 回退

    [Fact]
    public void Build_WithoutFactory_InvokesFallback()
    {
        // Arrange
        var channel = CreateChannel();
        var parent = CreateParent(channel);
        var builder = CreateBuilder(channel, parent, CreateDescriptor());

        // Act
        var tag = builder.Build(channel);

        // Assert
        Assert.Equal(1, builder.FallbackCallCount);
        Assert.Same(channel, builder.LastFallbackChannel);
        Assert.IsType<FakedTag>(tag);
    }

    [Fact]
    public void Build_WithFactory_DelegatePreferredOverFallback()
    {
        // Arrange
        var channel = CreateChannel();
        var parent = CreateParent(channel);
        var builder = CreateBuilder(channel, parent, CreateDescriptor());

        var delegateCalled = false;
        builder.WithFactory((_, _, _) =>
        {
            delegateCalled = true;
            return new FakedTag(builder.TagDescriptor, channel, parent);
        });

        // Act
        var tag = builder.Build(channel);

        // Assert：委托被优先调用，Fallback 未被调用
        Assert.True(delegateCalled);
        Assert.Equal(0, builder.FallbackCallCount);
        Assert.IsType<FakedTag>(tag);
    }

    [Fact]
    public void Build_WithFactoryReturnsNull_FallsBack()
    {
        // Arrange
        var channel = CreateChannel();
        var parent = CreateParent(channel);
        var builder = CreateBuilder(channel, parent, CreateDescriptor());

        builder.WithFactory((_, _, _) => null!);

        // Act
        var tag = builder.Build(channel);

        // Assert：委托返回 null → 回退 Fallback
        Assert.Equal(1, builder.FallbackCallCount);
        Assert.IsType<FakedTag>(tag);
    }

    [Fact]
    public void Build_NullChannel_Throws()
    {
        // Arrange
        var channel = CreateChannel();
        var parent = CreateParent(channel);
        var builder = CreateBuilder(channel, parent, CreateDescriptor());

        // Act & Assert
        var ex = Assert.Throws<Exception>(() => builder.Build(null!));
        Assert.Contains("未配置通道", ex.Message);
    }

    #endregion

    #region 委托参数传递

    [Fact]
    public void Build_DelegateReceivesCorrectArguments()
    {
        // Arrange
        var channel = CreateChannel();
        var parent = CreateParent(channel);
        var descriptor = CreateDescriptor();
        var builder = CreateBuilder(channel, parent, descriptor);

        builder.WithFactory((d, thisChannel, container) =>
        {
            // Assert：委托收到的参数 = TagDescriptor / this.Channel / TagContainer.From(Parent)
            Assert.Same(descriptor, d);
            Assert.Same(channel, thisChannel);
            Assert.True(container.IsTagGrp);
            return new FakedTag(descriptor, channel, parent);
        });

        // Act（委托内的断言在调用时执行）
        var tag = builder.Build(channel);
        Assert.IsType<FakedTag>(tag);
    }

    [Fact]
    public void Build_DelegateWithNullSelfChannel_ReceivesNull()
    {
        // Arrange：不设置自身通道（this.Channel = null）
        var channel = CreateChannel();
        var parent = CreateParent(channel);
        var builder = CreateBuilder(null, parent, CreateDescriptor());

        builder.WithFactory((d, thisChannel, container) =>
        {
            Assert.Null(thisChannel);
            return new FakedTag(d, channel, parent);
        });

        // Act
        var tag = builder.Build(channel);

        // Assert
        Assert.IsType<FakedTag>(tag);
    }

    #endregion

    #region Fluent 配置

    [Fact]
    public void WithFactory_ReturnsSelf_ForChaining()
    {
        var builder = new TraceableTagBuilder();
        var result = builder.WithFactory((_, _, _) => null!);
        Assert.Same(builder, result);
    }

    [Fact]
    public void WithTagDescriptor_SetsProperty_AndReturnsSelf()
    {
        var descriptor = CreateDescriptor();
        var builder = new TraceableTagBuilder();

        var result = builder.WithTagDescriptor(descriptor);

        Assert.Same(descriptor, builder.TagDescriptor);
        Assert.Same(builder, result);
    }

    [Fact]
    public void WithChannel_SetsProperty_AndReturnsSelf()
    {
        var channel = CreateChannel();
        var builder = new TraceableTagBuilder();

        var result = builder.WithChannel(channel);

        Assert.Same(channel, builder.Channel);
        Assert.Same(builder, result);
    }

    [Fact]
    public void WithParent_SetsProperty_AndReturnsSelf()
    {
        var channel = CreateChannel();
        var parent = CreateParent(channel);
        var builder = new TraceableTagBuilder();

        var result = builder.WithParent(parent);

        Assert.Same(parent, builder.Parent);
        Assert.Same(builder, result);
    }

    [Fact]
    public void Configure_InvokesAction_AndReturnsSelf()
    {
        var builder = new TraceableTagBuilder();
        var called = false;

        var result = builder.Configure(_ => called = true);

        Assert.True(called);
        Assert.Same(builder, result);
    }

    [Fact]
    public void Name_ReturnsDescriptorTagName()
    {
        var descriptor = CreateDescriptor();
        var builder = new TraceableTagBuilder();

        builder.WithTagDescriptor(descriptor);

        Assert.Equal("t1", builder.Name);
    }

    #endregion
}
