using TripleSpaceTranslator.Core.Models;

namespace TripleSpaceTranslator.Core.Utilities;

public static class HotkeyCatalog
{
    private static readonly IReadOnlyList<HotkeyKeyOption> AllKeysInternal = BuildAllKeys();

    public static IReadOnlyList<HotkeyKeyOption> AllKeys => AllKeysInternal;

    public static HotkeyKeyOption GetByVirtualKeyCode(int virtualKeyCode)
    {
        return AllKeysInternal.FirstOrDefault(option => option.VirtualKeyCode == virtualKeyCode)
               ?? new HotkeyKeyOption
               {
                   DisplayName = $"VK {virtualKeyCode:X2}",
                   VirtualKeyCode = virtualKeyCode
               };
    }

    private static IReadOnlyList<HotkeyKeyOption> BuildAllKeys()
    {
        var keys = new List<HotkeyKeyOption>();

        for (var keyCode = 0x41; keyCode <= 0x5A; keyCode++)
        {
            keys.Add(new HotkeyKeyOption
            {
                DisplayName = ((char)keyCode).ToString(),
                VirtualKeyCode = keyCode
            });
        }

        for (var keyCode = 0x30; keyCode <= 0x39; keyCode++)
        {
            keys.Add(new HotkeyKeyOption
            {
                DisplayName = ((char)keyCode).ToString(),
                VirtualKeyCode = keyCode
            });
        }

        for (var functionIndex = 1; functionIndex <= 12; functionIndex++)
        {
            keys.Add(new HotkeyKeyOption
            {
                DisplayName = $"F{functionIndex}",
                VirtualKeyCode = 0x6F + functionIndex
            });
        }

        keys.Add(new HotkeyKeyOption
        {
            DisplayName = "Space",
            VirtualKeyCode = 0x20
        });

        return keys;
    }
}
