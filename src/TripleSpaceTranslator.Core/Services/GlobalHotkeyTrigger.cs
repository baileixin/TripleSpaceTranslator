using TripleSpaceTranslator.Core.Interop;
using TripleSpaceTranslator.Core.Models;

namespace TripleSpaceTranslator.Core.Services;

public sealed class GlobalHotkeyTrigger
{
    private readonly HashSet<int> _pressedKeys = [];
    private TriggerHotkey _hotkey;
    private bool _triggeredForCurrentChord;

    public GlobalHotkeyTrigger(TriggerHotkey hotkey)
    {
        _hotkey = hotkey.Clone();
    }

    public bool ProcessKeyEvent(int virtualKeyCode, bool isKeyDown)
    {
        return isKeyDown
            ? ProcessKeyDown(virtualKeyCode)
            : ProcessKeyUp(virtualKeyCode);
    }

    public void UpdateHotkey(TriggerHotkey hotkey)
    {
        _hotkey = hotkey.Clone();
        Reset();
    }

    public void Reset()
    {
        _pressedKeys.Clear();
        _triggeredForCurrentChord = false;
    }

    private bool ProcessKeyDown(int virtualKeyCode)
    {
        if (!_pressedKeys.Add(virtualKeyCode))
        {
            return false;
        }

        if (!_hotkey.IsValid() || IsModifierKey(virtualKeyCode) || virtualKeyCode != _hotkey.KeyCode)
        {
            return false;
        }

        if (_triggeredForCurrentChord)
        {
            return false;
        }

        if (!AreConfiguredModifiersPressed() || AreUnexpectedModifiersPressed())
        {
            return false;
        }

        _triggeredForCurrentChord = true;
        return true;
    }

    private bool ProcessKeyUp(int virtualKeyCode)
    {
        _pressedKeys.Remove(virtualKeyCode);

        if (virtualKeyCode == _hotkey.KeyCode || !IsHotkeyChordStillActive())
        {
            _triggeredForCurrentChord = false;
        }

        return false;
    }

    private bool AreConfiguredModifiersPressed()
    {
        return (!_hotkey.Ctrl || IsControlPressed()) &&
               (!_hotkey.Alt || IsAltPressed()) &&
               (!_hotkey.Shift || IsShiftPressed()) &&
               (!_hotkey.Win || IsWinPressed());
    }

    private bool AreUnexpectedModifiersPressed()
    {
        return (_hotkey.Ctrl != IsControlPressed()) ||
               (_hotkey.Alt != IsAltPressed()) ||
               (_hotkey.Shift != IsShiftPressed()) ||
               (_hotkey.Win != IsWinPressed());
    }

    private bool IsHotkeyChordStillActive()
    {
        return _pressedKeys.Contains(_hotkey.KeyCode) && AreConfiguredModifiersPressed() && !AreUnexpectedModifiersPressed();
    }

    private bool IsControlPressed()
    {
        return _pressedKeys.Contains(NativeMethods.VkControl) ||
               _pressedKeys.Contains(NativeMethods.VkLControl) ||
               _pressedKeys.Contains(NativeMethods.VkRControl);
    }

    private bool IsAltPressed()
    {
        return _pressedKeys.Contains(NativeMethods.VkMenu) ||
               _pressedKeys.Contains(NativeMethods.VkLMenu) ||
               _pressedKeys.Contains(NativeMethods.VkRMenu);
    }

    private bool IsShiftPressed()
    {
        return _pressedKeys.Contains(NativeMethods.VkShift) ||
               _pressedKeys.Contains(NativeMethods.VkLShift) ||
               _pressedKeys.Contains(NativeMethods.VkRShift);
    }

    private bool IsWinPressed()
    {
        return _pressedKeys.Contains(NativeMethods.VkLWin) ||
               _pressedKeys.Contains(NativeMethods.VkRWin);
    }

    private static bool IsModifierKey(int virtualKeyCode)
    {
        return virtualKeyCode is
            NativeMethods.VkShift or
            NativeMethods.VkControl or
            NativeMethods.VkMenu or
            NativeMethods.VkLShift or
            NativeMethods.VkRShift or
            NativeMethods.VkLControl or
            NativeMethods.VkRControl or
            NativeMethods.VkLMenu or
            NativeMethods.VkRMenu or
            NativeMethods.VkLWin or
            NativeMethods.VkRWin;
    }
}
