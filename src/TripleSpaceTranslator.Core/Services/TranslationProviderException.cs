namespace TripleSpaceTranslator.Core.Services;

public sealed class TranslationProviderException : Exception
{
    public TranslationProviderException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}
