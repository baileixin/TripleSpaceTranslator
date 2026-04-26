namespace TripleSpaceTranslator.Core.Interfaces;

public interface IDiagnosticLogger
{
    void Log(string category, string message);
}
