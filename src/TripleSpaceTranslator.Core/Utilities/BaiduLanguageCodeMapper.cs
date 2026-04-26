namespace TripleSpaceTranslator.Core.Utilities;

public static class BaiduLanguageCodeMapper
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
            "zh-tw" => "cht",
            "zh_tw" => "cht",
            "ja" => "jp",
            "ko" => "kor",
            "fr" => "fra",
            "es" => "spa",
            _ => languageCode.Trim().ToLowerInvariant()
        };
    }
}
