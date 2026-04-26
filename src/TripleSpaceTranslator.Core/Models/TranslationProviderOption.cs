namespace TripleSpaceTranslator.Core.Models;

public sealed record TranslationProviderOption(
    TranslationProviderType ProviderType,
    string DisplayName,
    string CredentialIdLabel,
    string CredentialKeyLabel,
    string Description,
    string ConnectionTestHint,
    bool SupportsAdvancedSettings);
