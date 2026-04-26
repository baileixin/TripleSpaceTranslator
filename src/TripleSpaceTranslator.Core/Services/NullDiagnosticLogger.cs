using TripleSpaceTranslator.Core.Interfaces;

namespace TripleSpaceTranslator.Core.Services;

public sealed class NullDiagnosticLogger : IDiagnosticLogger
{
    public static readonly NullDiagnosticLogger Instance = new();

    private NullDiagnosticLogger()
    {
    }

    public void Log(string category, string message)
    {
    }
}
