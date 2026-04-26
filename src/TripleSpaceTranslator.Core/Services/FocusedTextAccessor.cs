using System.Windows.Automation;
using TripleSpaceTranslator.Core.Interfaces;
using TripleSpaceTranslator.Core.Interop;
using TripleSpaceTranslator.Core.Models;
using TripleSpaceTranslator.Core.Utilities;

namespace TripleSpaceTranslator.Core.Services;

public sealed class FocusedTextAccessor : IFocusedTextAccessor
{
    private const int FocusDelayMs = 60;
    private const int SelectionDelayMs = 60;
    private const int DeleteDelayMs = 60;
    private const int VerificationTimeoutMs = 900;
    private const int VerificationPollIntervalMs = 60;

    private readonly IFocusContextProvider _focusContextProvider;
    private readonly IDiagnosticLogger _logger;

    public FocusedTextAccessor(IFocusContextProvider focusContextProvider, IDiagnosticLogger? logger = null)
    {
        _focusContextProvider = focusContextProvider;
        _logger = logger ?? NullDiagnosticLogger.Instance;
    }

    public string GetCurrentFocusContextId()
    {
        return _focusContextProvider.GetFocusContextId();
    }

    public FocusedTextAccessorResult GetFocusedText()
    {
        var element = AutomationElement.FocusedElement;
        if (element is null)
        {
            _logger.Log("focus", "FocusedElement was null.");
            return BuildUnsupportedResult();
        }

        var focusContextId = _focusContextProvider.GetFocusContextId();
        var isPassword = GetBooleanProperty(element, AutomationElement.IsPasswordProperty);
        var isEnabled = GetBooleanProperty(element, AutomationElement.IsEnabledProperty);
        var controlType = element.Current.ControlType?.ProgrammaticName ?? "unknown";
        var className = element.Current.ClassName ?? string.Empty;

        if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePatternObject))
        {
            var valuePattern = (ValuePattern)valuePatternObject;
            var originalText = valuePattern.Current.Value ?? string.Empty;
            _logger.Log(
                "focus",
                $"ValuePattern supported. ControlType={controlType}, ClassName={className}, FocusContext={focusContextId}, IsEnabled={isEnabled}, IsPassword={isPassword}, IsReadOnly={valuePattern.Current.IsReadOnly}, TextLength={originalText.Length}.");

            return new FocusedTextAccessorResult
            {
                IsSupported = true,
                IsReadOnly = valuePattern.Current.IsReadOnly || !isEnabled,
                IsPassword = isPassword,
                OriginalText = originalText,
                ReplaceMode = valuePattern.Current.IsReadOnly || !isEnabled ? TextReplaceMode.Unsupported : TextReplaceMode.ValuePattern,
                FocusContextId = focusContextId,
                TargetElement = element
            };
        }

        if (element.TryGetCurrentPattern(TextPattern.Pattern, out var textPatternObject))
        {
            var textPattern = (TextPattern)textPatternObject;
            var originalText = NormalizeText(textPattern.DocumentRange.GetText(-1));
            _logger.Log(
                "focus",
                $"TextPattern supported. ControlType={controlType}, ClassName={className}, FocusContext={focusContextId}, IsEnabled={isEnabled}, IsPassword={isPassword}, TextLength={originalText.Length}.");

            return new FocusedTextAccessorResult
            {
                IsSupported = true,
                IsReadOnly = !isEnabled,
                IsPassword = isPassword,
                OriginalText = originalText,
                ReplaceMode = !isEnabled ? TextReplaceMode.Unsupported : TextReplaceMode.InputInjection,
                FocusContextId = focusContextId,
                TargetElement = element
            };
        }

        _logger.Log(
            "focus",
            $"Focused element unsupported. ControlType={controlType}, ClassName={className}, FocusContext={focusContextId}, IsEnabled={isEnabled}, IsPassword={isPassword}.");
        return BuildUnsupportedResult(focusContextId);
    }

    public bool ReplaceText(string replacementText, FocusedTextAccessorResult accessResult)
    {
        if (!accessResult.CanTranslate)
        {
            _logger.Log(
                "replace",
                $"ReplaceText skipped because access result cannot translate. Supported={accessResult.IsSupported}, ReadOnly={accessResult.IsReadOnly}, Password={accessResult.IsPassword}, FocusContext={accessResult.FocusContextId}.");
            return false;
        }

        var element = accessResult.TargetElement ?? AutomationElement.FocusedElement;
        if (element is null)
        {
            _logger.Log("replace", "ReplaceText failed because FocusedElement was null.");
            return false;
        }

        if (accessResult.ReplaceMode == TextReplaceMode.ValuePattern &&
            element.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePatternObject))
        {
            ((ValuePattern)valuePatternObject).SetValue(replacementText);
            if (WaitForTextMatch(element, replacementText, out var currentText))
            {
                _logger.Log("replace", $"Text replaced via ValuePattern. TextLength={replacementText.Length}, FocusContext={accessResult.FocusContextId}.");
                return true;
            }

            _logger.Log(
                "replace",
                $"ValuePattern SetValue could not be verified. FocusContext={accessResult.FocusContextId}, ObservedLength={currentText.Length}, ExpectedLength={replacementText.Length}. Trying input fallback.");
            var fallbackReplaced = ReplaceUsingInputInjection(element, replacementText, accessResult.FocusContextId);
            _logger.Log(
                "replace",
                fallbackReplaced
                    ? $"ValuePattern fallback replacement succeeded. TextLength={replacementText.Length}, FocusContext={accessResult.FocusContextId}."
                    : $"ValuePattern fallback replacement failed. FocusContext={accessResult.FocusContextId}.");
            return fallbackReplaced;
        }

        if (accessResult.ReplaceMode == TextReplaceMode.ValuePattern)
        {
            _logger.Log(
                "replace",
                $"Stored target no longer exposes ValuePattern. FocusContext={accessResult.FocusContextId}. Trying input fallback.");
            var fallbackReplaced = ReplaceUsingInputInjection(element, replacementText, accessResult.FocusContextId);
            _logger.Log(
                "replace",
                fallbackReplaced
                    ? $"ValuePattern missing fallback replacement succeeded. TextLength={replacementText.Length}, FocusContext={accessResult.FocusContextId}."
                    : $"ValuePattern missing fallback replacement failed. FocusContext={accessResult.FocusContextId}.");
            return fallbackReplaced;
        }

        if (accessResult.ReplaceMode == TextReplaceMode.InputInjection)
        {
            var replaced = ReplaceUsingInputInjection(element, replacementText, accessResult.FocusContextId);
            _logger.Log(
                "replace",
                replaced
                    ? $"Text replaced via input injection. TextLength={replacementText.Length}, FocusContext={accessResult.FocusContextId}."
                    : $"Input injection replacement failed. FocusContext={accessResult.FocusContextId}.");
            return replaced;
        }

        _logger.Log("replace", $"ReplaceText failed because ReplaceMode={accessResult.ReplaceMode} was not applicable.");
        return false;
    }

    private bool ReplaceUsingInputInjection(AutomationElement element, string replacementText, string focusContextId)
    {
        try
        {
            if (IsLikelyRichWebEditor(element))
            {
                _logger.Log("replace", $"Rich web editor detected. Prioritizing clipboard paste. FocusContext={focusContextId}.");
                if (ReplaceUsingClipboardPaste(element, replacementText, focusContextId))
                {
                    return true;
                }
            }

            element.SetFocus();
            Thread.Sleep(FocusDelayMs);

            SelectAll(element);
            ClearSelection();

            if (replacementText.Length == 0)
            {
                return WaitForTextMatch(element, replacementText, out _);
            }

            SendUnicodeText(replacementText);

            if (WaitForTextMatch(element, replacementText, out var currentText))
            {
                _logger.Log("replace", $"Unicode input write-back verified. FocusContext={focusContextId}.");
                return true;
            }

            _logger.Log(
                "replace",
                $"Unicode input write-back could not be verified. FocusContext={focusContextId}, ObservedLength={currentText.Length}, ExpectedLength={replacementText.Length}. Trying clipboard paste fallback.");

            return ReplaceUsingClipboardPaste(element, replacementText, focusContextId);
        }
        catch (Exception ex)
        {
            _logger.Log("replace", $"Input injection threw an exception. FocusContext={focusContextId}, Error={ex.Message}.");
            return false;
        }
    }

    private bool ReplaceUsingClipboardPaste(AutomationElement element, string replacementText, string focusContextId)
    {
        ClipboardTextSnapshot clipboardSnapshot = default;

        try
        {
            clipboardSnapshot = ClipboardHelper.CaptureTextSnapshot();
            ClipboardHelper.SetText(replacementText);

            element.SetFocus();
            Thread.Sleep(FocusDelayMs);

            SelectAll(element);
            ClearSelection();
            SendKeyCombination(NativeMethods.VkControl, NativeMethods.VkV);

            if (WaitForTextMatch(element, replacementText, out var currentText))
            {
                _logger.Log("replace", $"Clipboard paste fallback verified successfully. FocusContext={focusContextId}.");
                return true;
            }

            _logger.Log(
                "replace",
                $"Ctrl+V paste could not be verified. FocusContext={focusContextId}, ObservedLength={currentText.Length}, ExpectedLength={replacementText.Length}. Trying Shift+Insert fallback.");

            SendKeyCombination(NativeMethods.VkShift, NativeMethods.VkInsert);
            var success = WaitForTextMatch(element, replacementText, out currentText);
            _logger.Log(
                "replace",
                success
                    ? $"Shift+Insert fallback verified successfully. FocusContext={focusContextId}."
                    : $"Clipboard paste fallback could not be verified. FocusContext={focusContextId}, ObservedLength={currentText.Length}, ExpectedLength={replacementText.Length}.");
            return success;
        }
        catch (Exception ex)
        {
            _logger.Log("replace", $"Clipboard paste fallback failed. FocusContext={focusContextId}, Error={ex.Message}.");
            return false;
        }
        finally
        {
            try
            {
                ClipboardHelper.RestoreTextSnapshot(clipboardSnapshot);
            }
            catch (Exception ex)
            {
                _logger.Log("replace", $"Clipboard restore failed. FocusContext={focusContextId}, Error={ex.Message}.");
            }
        }
    }

    private static bool IsLikelyRichWebEditor(AutomationElement element)
    {
        var className = element.Current.ClassName ?? string.Empty;
        var controlType = element.Current.ControlType?.ProgrammaticName ?? string.Empty;

        return className.Contains("ProseMirror", StringComparison.OrdinalIgnoreCase) ||
               className.Contains("contenteditable", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(controlType, "ControlType.Group", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(controlType, "ControlType.Document", StringComparison.OrdinalIgnoreCase);
    }

    private static void SelectAll(AutomationElement element)
    {
        element.SetFocus();
        Thread.Sleep(FocusDelayMs);
        SendKeyCombination(NativeMethods.VkControl, NativeMethods.VkA);
        Thread.Sleep(SelectionDelayMs);
    }

    private static void ClearSelection()
    {
        SendVirtualKey(NativeMethods.VkDelete);
        Thread.Sleep(DeleteDelayMs);
    }

    private static bool WaitForTextMatch(AutomationElement element, string expectedText, out string observedText)
    {
        var startedAt = Environment.TickCount64;
        var normalizedExpected = NormalizeForComparison(expectedText);

        do
        {
            observedText = ReadCurrentElementText(element);
            if (string.Equals(NormalizeForComparison(observedText), normalizedExpected, StringComparison.Ordinal))
            {
                return true;
            }

            Thread.Sleep(VerificationPollIntervalMs);
        }
        while (Environment.TickCount64 - startedAt < VerificationTimeoutMs);

        observedText = ReadCurrentElementText(element);
        return string.Equals(NormalizeForComparison(observedText), normalizedExpected, StringComparison.Ordinal);
    }

    private static string ReadCurrentElementText(AutomationElement element)
    {
        if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePatternObject))
        {
            return NormalizeText(((ValuePattern)valuePatternObject).Current.Value);
        }

        if (element.TryGetCurrentPattern(TextPattern.Pattern, out var textPatternObject))
        {
            return NormalizeText(((TextPattern)textPatternObject).DocumentRange.GetText(-1));
        }

        return string.Empty;
    }

    private static FocusedTextAccessorResult BuildUnsupportedResult(string focusContextId = "")
    {
        return new FocusedTextAccessorResult
        {
            FocusContextId = focusContextId,
            ReplaceMode = TextReplaceMode.Unsupported
        };
    }

    private static bool GetBooleanProperty(AutomationElement element, AutomationProperty property)
    {
        return element.GetCurrentPropertyValue(property) is bool value && value;
    }

    private static string NormalizeText(string? text)
    {
        return (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .TrimEnd('\n');
    }

    private static string NormalizeForComparison(string text)
    {
        return NormalizeText(text)
            .Replace('\u00A0', ' ')
            .Replace("\u200B", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private static void SendKeyCombination(int modifierKey, int mainKey)
    {
        SendInputs(
            CreateVirtualKeyInput(modifierKey, keyUp: false),
            CreateVirtualKeyInput(mainKey, keyUp: false),
            CreateVirtualKeyInput(mainKey, keyUp: true),
            CreateVirtualKeyInput(modifierKey, keyUp: true));
    }

    private static void SendVirtualKey(int keyCode)
    {
        SendInputs(
            CreateVirtualKeyInput(keyCode, keyUp: false),
            CreateVirtualKeyInput(keyCode, keyUp: true));
    }

    private static void SendUnicodeText(string text)
    {
        var inputs = new List<NativeMethods.INPUT>(text.Length * 2);
        foreach (var character in text)
        {
            inputs.Add(CreateUnicodeInput(character, keyUp: false));
            inputs.Add(CreateUnicodeInput(character, keyUp: true));
        }

        SendInputs(inputs.ToArray());
    }

    private static NativeMethods.INPUT CreateVirtualKeyInput(int keyCode, bool keyUp)
    {
        return new NativeMethods.INPUT
        {
            type = NativeMethods.InputKeyboard,
            U = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = (ushort)keyCode,
                    wScan = 0,
                    dwFlags = keyUp ? NativeMethods.KeyeventfKeyup : 0
                }
            }
        };
    }

    private static NativeMethods.INPUT CreateUnicodeInput(char character, bool keyUp)
    {
        return new NativeMethods.INPUT
        {
            type = NativeMethods.InputKeyboard,
            U = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = 0,
                    wScan = character,
                    dwFlags = NativeMethods.KeyeventfUnicode | (keyUp ? NativeMethods.KeyeventfKeyup : 0)
                }
            }
        };
    }

    private static void SendInputs(params NativeMethods.INPUT[] inputs)
    {
        if (inputs.Length == 0)
        {
            return;
        }

        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.INPUT>());
        if (sent != inputs.Length)
        {
            throw new InvalidOperationException($"SendInput sent {sent} of {inputs.Length} events. Win32 error: {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}.");
        }
    }
}
