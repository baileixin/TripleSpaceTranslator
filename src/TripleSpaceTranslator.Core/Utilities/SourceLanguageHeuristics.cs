namespace TripleSpaceTranslator.Core.Utilities;

public static class SourceLanguageHeuristics
{
    public static string DetectBaiduLanguageCode(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "en";
        }

        foreach (var character in text)
        {
            if (IsJapanese(character))
            {
                return "jp";
            }

            if (IsKorean(character))
            {
                return "kor";
            }

            if (IsChinese(character))
            {
                return "zh";
            }

            if (IsCyrillic(character))
            {
                return "ru";
            }

            if (IsArabic(character))
            {
                return "ara";
            }
        }

        return "en";
    }

    public static string DetectTencentLanguageCode(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "en";
        }

        foreach (var character in text)
        {
            if (IsJapanese(character))
            {
                return "ja";
            }

            if (IsKorean(character))
            {
                return "ko";
            }

            if (IsChinese(character))
            {
                return "zh";
            }

            if (IsCyrillic(character))
            {
                return "ru";
            }

            if (IsArabic(character))
            {
                return "ar";
            }
        }

        return "en";
    }

    private static bool IsChinese(char character)
    {
        return character is >= '\u4E00' and <= '\u9FFF';
    }

    private static bool IsJapanese(char character)
    {
        return character is >= '\u3040' and <= '\u30FF';
    }

    private static bool IsKorean(char character)
    {
        return character is >= '\uAC00' and <= '\uD7AF';
    }

    private static bool IsCyrillic(char character)
    {
        return character is >= '\u0400' and <= '\u04FF';
    }

    private static bool IsArabic(char character)
    {
        return character is >= '\u0600' and <= '\u06FF';
    }
}
