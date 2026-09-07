using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace TrayApp.Shared.Utils;

public class FileLogger : ILogger
{
    private readonly string _name;
    private readonly FileLoggerProvider _provider;

    public FileLogger(string name, FileLoggerProvider provider)
    {
        _name = name;
        _provider = provider;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => logLevel >= _provider.MinLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        var message = formatter(state, exception);
        var line = $"[{DateTime.UtcNow:O}] {logLevel} {_name}: {message}" + (exception != null ? $" {exception}" : string.Empty);
        _provider.WriteLine(line);
    }
}

public class FileLoggerProvider : ILoggerProvider
{
    private readonly string _path;
    private readonly BlockingCollection<string> _queue = new();
    private readonly Thread _worker;
    public LogLevel MinLevel { get; set; } = LogLevel.Information;

    public FileLoggerProvider(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        _worker = new Thread(Worker) { IsBackground = true };
        _worker.Start();
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, this);

    public void Dispose()
    {
        _queue.CompleteAdding();
        _worker.Join(2000);
    }

    internal void WriteLine(string line) => _queue.Add(line);

    private void Worker()
    {
        using var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream) { AutoFlush = true };
        foreach (var line in _queue.GetConsumingEnumerable())
        {
            try { writer.WriteLine(line); } catch { }
        }
    }
}

public static class FileLoggerExtensions
{
    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string path)
    {
        var provider = new FileLoggerProvider(path);
        builder.AddProvider(provider);
        return builder;
    }
}
