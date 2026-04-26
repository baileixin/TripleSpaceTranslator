using TripleSpaceTranslator.Core.Models;

namespace TripleSpaceTranslator.Core.Interfaces;

public interface IFocusedTextAccessor
{
    string GetCurrentFocusContextId();

    FocusedTextAccessorResult GetFocusedText();

    bool ReplaceText(string replacementText, FocusedTextAccessorResult accessResult);
}
