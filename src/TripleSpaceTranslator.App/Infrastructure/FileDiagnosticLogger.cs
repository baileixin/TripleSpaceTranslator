using System.IO;
using System.Text;
using TripleSpaceTranslator.Core.Interfaces;

namespace TripleSpaceTranslator.App.Infrastructure;

public sealed class FileDiagnosticLogger : IDiagnosticLogger
{
    private readonly string _logFilePath;
    private readonly object _syncRoot = new();

    public FileDiagnosticLogger(string logFilePath)
    {
        _logFilePath = logFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath)!);
    }

    public void Log(string category, string message)
    {
        var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}] [{category}] {message}{Environment.NewLine}";

        lock (_syncRoot)
        {
            File.AppendAllText(_logFilePath, line, Encoding.UTF8);
        }
    }
}
