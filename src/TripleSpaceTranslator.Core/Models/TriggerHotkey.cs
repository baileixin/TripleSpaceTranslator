namespace TripleSpaceTranslator.Core.Models;

public sealed class TriggerHotkey
{
    public bool Alt { get; set; }

    public bool Ctrl { get; set; }

    public int KeyCode { get; set; }

    public bool Shift { get; set; }

    public bool Win { get; set; }

    public static TriggerHotkey CreateDefault()
    {
        return new TriggerHotkey
        {
            Ctrl = true,
            Alt = true,
            KeyCode = 0x51
        };
    }

    public TriggerHotkey Clone()
    {
        return new TriggerHotkey
        {
            Alt = Alt,
            Ctrl = Ctrl,
            KeyCode = KeyCode,
            Shift = Shift,
            Win = Win
        };
    }

    public bool IsValid()
    {
        return KeyCode != 0 && (Ctrl || Alt || Shift || Win);
    }
}
