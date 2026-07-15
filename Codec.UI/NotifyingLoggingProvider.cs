namespace Codec.UI
{
    using System;
    using Microsoft.Extensions.Logging;

    public sealed class NotifyingLoggerProvider : ILoggerProvider
    {
        public event EventHandler<LogEntry>? EntryLogged;

        /// <inheritdoc/>
        public ILogger CreateLogger(string categoryName) =>
            new NotifyingLogger(categoryName, e => this.EntryLogged?.Invoke(this, e));

        /// <inheritdoc/>
        public void Dispose()
        {
        }

        private sealed class NotifyingLogger(string category, Action<LogEntry> onLog) : ILogger
        {
            private readonly string category = category;
            private readonly Action<LogEntry> onLog = onLog;

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull =>
                null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                this.onLog(new LogEntry(logLevel, formatter(state, exception), exception?.StackTrace ?? string.Empty));
            }
        }

        public readonly record struct LogEntry(LogLevel Severity, string Text, string Location);
    }
}
