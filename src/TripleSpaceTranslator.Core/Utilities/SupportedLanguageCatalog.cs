using TripleSpaceTranslator.Core.Models;

namespace TripleSpaceTranslator.Core.Utilities;

public static class SupportedLanguageCatalog
{
    public static IReadOnlyList<SupportedLanguage> All { get; } =
    [
        new("en", "English"),
        new("zh", "Chinese (Simplified)"),
        new("ja", "Japanese"),
        new("ko", "Korean"),
        new("fr", "French"),
        new("de", "German"),
        new("es", "Spanish")
    ];

    public static SupportedLanguage GetByCode(string languageCode)
    {
        return All.FirstOrDefault(language => string.Equals(language.Code, languageCode, StringComparison.OrdinalIgnoreCase))
            ?? All[0];
    }
}
