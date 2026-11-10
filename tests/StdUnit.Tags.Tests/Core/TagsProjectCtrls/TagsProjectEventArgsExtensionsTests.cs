using System;
using Xunit;

namespace StdUnit.Tags.Tests.Core.TagsProjectCtrls;

/// <summary>
/// <see cref="TagsProjectEventArgsExtensions.IsStarted"/>：只是 <see cref="TagsProjectEventArgs.State"/>
/// 的派生判断，事件参数本身不再保存这个状态（避免"bool 与 State 各说各话"）。
/// </summary>
public class TagsProjectEventArgsExtensionsTests
{
    [Fact]
    public void IsStarted_WhenRunning_ReturnsTrue()
    {
        var args = new TagsProjectEventArgs(TagsProjectState.Running, new MockTagsProject());
        Assert.True(args.IsStarted());
    }

    [Fact]
    public void IsStarted_WhenStopped_ReturnsFalse()
    {
        var args = new TagsProjectEventArgs(TagsProjectState.Stopped, project: null, runId: 42, failure: null);
        Assert.False(args.IsStarted());
    }

    [Fact]
    public void IsStarted_WhenFailureCarried_StillFalse()
    {
        var args = new TagsProjectEventArgs(
            TagsProjectState.Stopped,
            project: null,
            runId: 7,
            failure: new InvalidOperationException("boom"));

        Assert.False(args.IsStarted());
        Assert.NotNull(args.Failure);
        Assert.Equal(7, args.RunId);
    }

    [Fact]
    public void IsStarted_WhenArgsIsNull_Throws()
    {
        TagsProjectEventArgs? args = null;
        Assert.Throws<ArgumentNullException>(() => args!.IsStarted());
    }

    [Fact]
    public void IsStarted_WithProject_WhenRunning_ReturnsTrueAndProject()
    {
        var project = new MockTagsProject();
        var args = new TagsProjectEventArgs(TagsProjectState.Running, project);

        Assert.True(args.IsStarted(out var running));

        // 返回 true 时 out 参数必定可用：调用点不需要再判空
        Assert.Same(project, running);
    }

    [Fact]
    public void IsStarted_WithProject_WhenStopped_ReturnsFalseAndNull()
    {
        var args = new TagsProjectEventArgs(TagsProjectState.Stopped, project: null, runId: 42);

        Assert.False(args.IsStarted(out var project));

        // 返回 false 时 out 参数刻意置空：不交出"半截状态"
        Assert.Null(project);
    }

    [Fact]
    public void IsStarted_WithProject_WhenRunningButProjectIsNull_ReturnsFalse()
    {
        // State 与 Project 是两次独立读取，理论上可能读到不一致的瞬间；此时按"未启动"处理
        var args = new TagsProjectEventArgs(TagsProjectState.Running, project: null);

        Assert.False(args.IsStarted(out var project));
        Assert.Null(project);
    }

    [Fact]
    public void IsStarted_WithProject_WhenArgsIsNull_Throws()
    {
        TagsProjectEventArgs? args = null;
        Assert.Throws<ArgumentNullException>(() => args!.IsStarted(out _));
    }

    [Fact]
    public void IsStarted_WithoutOut_IsEquivalentToOutVersion()
    {
        var running = new TagsProjectEventArgs(TagsProjectState.Running, new MockTagsProject());
        var stopped = new TagsProjectEventArgs(TagsProjectState.Stopped, project: null);

        Assert.Equal(running.IsStarted(), running.IsStarted(out _));
        Assert.Equal(stopped.IsStarted(), stopped.IsStarted(out _));
    }
}
