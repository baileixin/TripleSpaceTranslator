namespace TripleSpaceTranslator.Core.Interfaces;

public interface IStartupRegistrationService
{
    bool IsEnabled();

    void SetEnabled(string executablePath, bool enabled);
}
