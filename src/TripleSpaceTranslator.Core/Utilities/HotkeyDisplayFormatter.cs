using TripleSpaceTranslator.Core.Models;

namespace TripleSpaceTranslator.Core.Utilities;

public static class HotkeyDisplayFormatter
{
    public static string Format(TriggerHotkey hotkey)
    {
        if (hotkey is null || !hotkey.IsValid())
        {
            return "未设置";
        }

        var parts = new List<string>();

        if (hotkey.Ctrl)
        {
            parts.Add("Ctrl");
        }

        if (hotkey.Alt)
        {
            parts.Add("Alt");
        }

        if (hotkey.Shift)
        {
            parts.Add("Shift");
        }

        if (hotkey.Win)
        {
            parts.Add("Win");
        }

        parts.Add(HotkeyCatalog.GetByVirtualKeyCode(hotkey.KeyCode).DisplayName);
        return string.Join("+", parts);
    }
}
