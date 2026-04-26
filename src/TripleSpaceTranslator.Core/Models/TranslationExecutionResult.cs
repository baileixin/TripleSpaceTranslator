namespace TripleSpaceTranslator.Core.Models;

public sealed class TranslationExecutionResult
{
    public bool Succeeded { get; init; }

    public string Message { get; init; } = string.Empty;

    public static TranslationExecutionResult Success(string message = "翻译完成。")
    {
        return new TranslationExecutionResult
        {
            Succeeded = true,
            Message = message
        };
    }

    public static TranslationExecutionResult Failure(string message)
    {
        return new TranslationExecutionResult
        {
            Succeeded = false,
            Message = message
        };
    }
}
