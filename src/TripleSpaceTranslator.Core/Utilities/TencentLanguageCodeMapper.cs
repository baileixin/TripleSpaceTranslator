namespace TripleSpaceTranslator.Core.Utilities;

public static class TencentLanguageCodeMapper
{
    public static string Normalize(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return "en";
        }

        return languageCode.Trim().ToLowerInvariant() switch
        {
            "zh-cn" => "zh",
            "zh_cn" => "zh",
            "zh-tw" => "zh-TW",
            "zh_tw" => "zh-TW",
            _ => languageCode.Trim()
        };
    }
}
