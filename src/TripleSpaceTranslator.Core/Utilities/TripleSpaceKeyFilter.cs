using TripleSpaceTranslator.Core.Interop;

namespace TripleSpaceTranslator.Core.Utilities;

public static class TripleSpaceKeyFilter
{
    public static bool IsSpace(int virtualKeyCode)
    {
        return virtualKeyCode == NativeMethods.VkSpace;
    }

    public static bool ShouldResetSequence(int virtualKeyCode)
    {
        return !IsSpace(virtualKeyCode) && !ShouldIgnoreForSequence(virtualKeyCode);
    }

    private static bool ShouldIgnoreForSequence(int virtualKeyCode)
    {
        return virtualKeyCode is
            NativeMethods.VkShift or
            NativeMethods.VkLShift or
            NativeMethods.VkRShift or
            NativeMethods.VkControl or
            NativeMethods.VkLControl or
            NativeMethods.VkRControl or
            NativeMethods.VkMenu or
            NativeMethods.VkLMenu or
            NativeMethods.VkRMenu or
            NativeMethods.VkLWin or
            NativeMethods.VkRWin or
            NativeMethods.VkKana or
            NativeMethods.VkImeOn or
            NativeMethods.VkJunja or
            NativeMethods.VkFinal or
            NativeMethods.VkHanja or
            NativeMethods.VkKanji or
            NativeMethods.VkImeOff or
            NativeMethods.VkConvert or
            NativeMethods.VkNonConvert or
            NativeMethods.VkAccept or
            NativeMethods.VkModechange or
            NativeMethods.VkProcessKey or
            NativeMethods.VkPacket;
    }
}
