using System;
using System.Threading;
using System.Threading.Tasks;

namespace StdUnit.Tags.Tests.R3;

/// <summary>
/// 用于 R3 扩展测试的 FakeTag，支持手动触发事件
/// </summary>
internal sealed class FakeR3Tag : ITag
{
    public TagDescriptor TagDescriptor { get; set; } = new() { TagName = "test-tag" };
    public object? Value { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsScanned { get; set; }
    public bool IsDirty { get; set; }
    public ITagChannel? Channel => null;
    public TagContainer? Parent { get; set; }

    public event TagSyncEventHandler? OnTagRead;
    public event TagSyncEventHandler? OnTagWritten;

    public void FireOnRead(object? newValue)
    {
        Value = newValue;
        Timestamp = DateTime.UtcNow;
        var args = new TagSyncEventArgs(newValue, Timestamp, TagSyncEventArgs.Kinds.Read);
        OnTagRead?.Invoke(this, args);
    }

    public void FireOnWritten(object? newValue)
    {
        Value = newValue;
        Timestamp = DateTime.UtcNow;
        var args = new TagSyncEventArgs(newValue, Timestamp, TagSyncEventArgs.Kinds.Written);
        OnTagWritten?.Invoke(this, args);
    }

    public Task ReadAsync(CancellationToken ct) => Task.CompletedTask;
    public Task WriteAsync(CancellationToken ct) => Task.CompletedTask;
}
