using TripleSpaceTranslator.Core.Interfaces;
using TripleSpaceTranslator.Core.Models;
using TripleSpaceTranslator.Core.Utilities;

namespace TripleSpaceTranslator.Core.Services;

public sealed class TripleSpaceTranslationCoordinator
{
    private readonly IFocusedTextAccessor _focusedTextAccessor;
    private readonly IDiagnosticLogger _logger;
    private readonly ITranslationProviderFactory _translationProviderFactory;

    public TripleSpaceTranslationCoordinator(
        IFocusedTextAccessor focusedTextAccessor,
        ITranslationProviderFactory translationProviderFactory,
        IDiagnosticLogger? logger = null)
    {
        _focusedTextAccessor = focusedTextAccessor;
        _translationProviderFactory = translationProviderFactory;
        _logger = logger ?? NullDiagnosticLogger.Instance;
    }

    public async Task<TranslationExecutionResult> TranslateFocusedTextAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.ProviderConfig.SecretId) ||
            string.IsNullOrWhiteSpace(settings.ProviderConfig.SecretKey))
        {
            _logger.Log("translate", "Translation aborted because credentials were missing.");
            return TranslationExecutionResult.Failure(
                TranslationProviderCatalog.GetMissingCredentialMessage(settings.ProviderConfig.ProviderType));
        }

        var accessResult = _focusedTextAccessor.GetFocusedText();
        if (!accessResult.IsSupported)
        {
            _logger.Log("translate", $"Translation aborted because focused control was unsupported. FocusContext={accessResult.FocusContextId}.");
            return TranslationExecutionResult.Failure("当前输入框不支持读取或写回文本。");
        }

        if (accessResult.IsPassword)
        {
            _logger.Log("translate", $"Translation aborted because focused control was a password box. FocusContext={accessResult.FocusContextId}.");
            return TranslationExecutionResult.Failure("密码输入框不会被翻译。");
        }

        if (accessResult.IsReadOnly)
        {
            _logger.Log("translate", $"Translation aborted because focused control was read-only. FocusContext={accessResult.FocusContextId}.");
            return TranslationExecutionResult.Failure("只读输入框不会被翻译。");
        }

        var normalizedText = StripTriggerSpaces(accessResult.OriginalText);
        if (!string.Equals(normalizedText, accessResult.OriginalText, StringComparison.Ordinal))
        {
            _ = _focusedTextAccessor.ReplaceText(normalizedText, accessResult);
        }

        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            _logger.Log("translate", $"Translation aborted because normalized text was empty. FocusContext={accessResult.FocusContextId}.");
            return TranslationExecutionResult.Failure("输入框内容为空。");
        }

        try
        {
            _logger.Log(
                "translate",
                $"Translation started. FocusContext={accessResult.FocusContextId}, SourceLength={normalizedText.Length}, TargetLanguage={settings.DefaultTargetLanguage}, Provider={settings.ProviderConfig.ProviderType}.");
            var provider = _translationProviderFactory.Create(settings.ProviderConfig);
            var translatedText = await provider.TranslateAsync(normalizedText, settings.DefaultTargetLanguage, cancellationToken).ConfigureAwait(false);
            if (!_focusedTextAccessor.ReplaceText(translatedText, accessResult))
            {
                _logger.Log("translate", $"Translation succeeded but write-back failed. FocusContext={accessResult.FocusContextId}, ResultLength={translatedText.Length}.");
                return TranslationExecutionResult.Failure("翻译成功，但无法把文本写回输入框。");
            }

            _logger.Log("translate", $"Translation completed successfully. FocusContext={accessResult.FocusContextId}, ResultLength={translatedText.Length}.");
            return TranslationExecutionResult.Success("翻译完成。");
        }
        catch (TranslationProviderException ex)
        {
            _logger.Log("translate", $"Translation failed with provider error. Code={ex.ErrorCode}, Message={ex.Message}.");
            return TranslationExecutionResult.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.Log("translate", $"Translation failed with unexpected error: {ex}.");
            return TranslationExecutionResult.Failure("翻译失败：" + ex.Message);
        }
    }

    private static string StripTriggerSpaces(string originalText)
    {
        if (string.IsNullOrEmpty(originalText))
        {
            return string.Empty;
        }

        var trailingSpaces = 0;
        for (var index = originalText.Length - 1; index >= 0 && originalText[index] == ' ' && trailingSpaces < 2; index--)
        {
            trailingSpaces++;
        }

        return trailingSpaces == 0
            ? originalText
            : originalText[..^trailingSpaces];
    }
}
