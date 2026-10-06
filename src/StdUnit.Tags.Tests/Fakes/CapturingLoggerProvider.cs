using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace StdUnit.Tags.Tests.Fakes;

/// <summary>
/// 把日志条目收集到内存，用于验证「清理路径有意吞掉异常，但必须留痕」。
/// </summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly List<(LogLevel Level, string Message, Exception? Exception)> _entries = new();

    /// <summary>
    /// 已收集的日志条目快照。
    /// </summary>
    public IReadOnlyList<(LogLevel Level, string Message, Exception? Exception)> Entries
    {
        get
        {
            lock (_entries)
            {
                return _entries.ToArray();
            }
        }
    }

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly CapturingLoggerProvider _owner;

        public CapturingLogger(CapturingLoggerProvider owner) => _owner = owner;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (_owner._entries)
            {
                _owner._entries.Add((logLevel, formatter(state, exception), exception));
            }
        }
    }
}
