namespace TripleSpaceTranslator.Core.Models;

using System.Windows.Automation;

public sealed class FocusedTextAccessorResult
{
    public bool IsSupported { get; init; }

    public bool IsReadOnly { get; init; }

    public bool IsPassword { get; init; }

    public string OriginalText { get; init; } = string.Empty;

    public TextReplaceMode ReplaceMode { get; init; }

    public string FocusContextId { get; init; } = string.Empty;

    public AutomationElement? TargetElement { get; init; }

    public bool CanTranslate => IsSupported && !IsReadOnly && !IsPassword;
}
