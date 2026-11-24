using System.IO;
using AppTranslator.App.Pipeline;
using Microsoft.Extensions.Logging;

namespace AppTranslator.VideoReplay;

/// <summary>Ghi mọi mức log (kể cả Trace từng khung) ra một file văn bản.</summary>
internal sealed class FileLogger : ILogger<SubtitleReader>, IDisposable
{
    private readonly StreamWriter _writer;

    public FileLogger(string path)
    {
        _writer = new StreamWriter(path);
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _writer.WriteLine($"{logLevel,-11} {formatter(state, exception)}");
        if (exception is not null)
            _writer.WriteLine(exception);
    }

    public void Dispose()
    {
        _writer.Dispose();
    }
}
