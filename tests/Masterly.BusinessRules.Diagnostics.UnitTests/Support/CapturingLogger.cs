using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Masterly.BusinessRules.Diagnostics.UnitTests.Support;

/// <summary>
/// One captured log call with its structured fields.
/// </summary>
public sealed class LogEntry
{
    public LogEntry(LogLevel level, EventId eventId, string message, Exception? exception, IReadOnlyDictionary<string, object?> fields)
    {
        Level = level;
        EventId = eventId;
        Message = message;
        Exception = exception;
        Fields = fields;
    }

    public LogLevel Level { get; }

    public EventId EventId { get; }

    public string Message { get; }

    public Exception? Exception { get; }

    public IReadOnlyDictionary<string, object?> Fields { get; }

    public object? this[string field] => Fields.TryGetValue(field, out object? value) ? value : null;
}

/// <summary>
/// An <see cref="ILogger"/> that records every enabled call, thread-safe so parallel batches can log into it.
/// </summary>
public sealed class CapturingLogger : ILogger
{
    private readonly ConcurrentQueue<LogEntry> _entries = new ConcurrentQueue<LogEntry>();

    public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;

    public IReadOnlyList<LogEntry> Entries => _entries.ToArray();

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return NullScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None && logLevel >= MinimumLevel;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        Dictionary<string, object?> fields = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (state is IReadOnlyList<KeyValuePair<string, object?>> pairs)
        {
            foreach (KeyValuePair<string, object?> pair in pairs)
            {
                fields[pair.Key] = pair.Value;
            }
        }

        _entries.Enqueue(new LogEntry(logLevel, eventId, formatter(state, exception), exception, fields));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new NullScope();

        public void Dispose()
        {
        }
    }
}
