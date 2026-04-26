using TripleSpaceTranslator.Core.Models;

namespace TripleSpaceTranslator.Core.Interfaces;

public interface ITranslationProvider
{
    TranslationProviderType ProviderType { get; }

    Task<string> TranslateAsync(string sourceText, string targetLanguage, CancellationToken cancellationToken);

    Task<ConnectionTestResult> TestConnectionAsync(TranslationProviderConfig providerConfig, string targetLanguage, CancellationToken cancellationToken);
}
