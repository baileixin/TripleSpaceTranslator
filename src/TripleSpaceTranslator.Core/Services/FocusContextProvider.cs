using TripleSpaceTranslator.Core.Interfaces;
using TripleSpaceTranslator.Core.Interop;

namespace TripleSpaceTranslator.Core.Services;

public sealed class FocusContextProvider : IFocusContextProvider
{
    public string GetFocusContextId()
    {
        var foregroundWindow = NativeMethods.GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
        {
            return string.Empty;
        }

        // Low-level keyboard hooks can observe transient IME focus hops, so use the
        // foreground window as the trigger context instead of the volatile child HWND.
        return $"{foregroundWindow.ToInt64():X}";
    }
}
