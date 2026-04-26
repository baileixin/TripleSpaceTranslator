using TripleSpaceTranslator.Core.Models;
using TripleSpaceTranslator.Core.Services;

namespace TripleSpaceTranslator.Tests;

public sealed class GlobalHotkeyTriggerTests
{
    [Fact]
    public void ProcessKeyEvent_TriggersWhenConfiguredHotkeyIsPressed()
    {
        var trigger = new GlobalHotkeyTrigger(new TriggerHotkey
        {
            Ctrl = true,
            Alt = true,
            KeyCode = 0x51
        });

        Assert.False(trigger.ProcessKeyEvent(0xA2, isKeyDown: true));
        Assert.False(trigger.ProcessKeyEvent(0xA4, isKeyDown: true));
        Assert.True(trigger.ProcessKeyEvent(0x51, isKeyDown: true));
    }

    [Fact]
    public void ProcessKeyEvent_DoesNotTriggerWithUnexpectedModifier()
    {
        var trigger = new GlobalHotkeyTrigger(new TriggerHotkey
        {
            Ctrl = true,
            Alt = true,
            KeyCode = 0x51
        });

        Assert.False(trigger.ProcessKeyEvent(0xA2, isKeyDown: true));
        Assert.False(trigger.ProcessKeyEvent(0xA4, isKeyDown: true));
        Assert.False(trigger.ProcessKeyEvent(0xA0, isKeyDown: true));
        Assert.False(trigger.ProcessKeyEvent(0x51, isKeyDown: true));
    }

    [Fact]
    public void ProcessKeyEvent_DoesNotRetriggerUntilChordIsReleased()
    {
        var trigger = new GlobalHotkeyTrigger(new TriggerHotkey
        {
            Ctrl = true,
            Alt = true,
            KeyCode = 0x51
        });

        Assert.False(trigger.ProcessKeyEvent(0xA2, isKeyDown: true));
        Assert.False(trigger.ProcessKeyEvent(0xA4, isKeyDown: true));
        Assert.True(trigger.ProcessKeyEvent(0x51, isKeyDown: true));
        Assert.False(trigger.ProcessKeyEvent(0x51, isKeyDown: true));
        Assert.False(trigger.ProcessKeyEvent(0x51, isKeyDown: false));
        Assert.True(trigger.ProcessKeyEvent(0x51, isKeyDown: true));
    }

    [Fact]
    public void UpdateHotkey_ReplacesExistingConfiguration()
    {
        var trigger = new GlobalHotkeyTrigger(new TriggerHotkey
        {
            Ctrl = true,
            Alt = true,
            KeyCode = 0x51
        });

        trigger.UpdateHotkey(new TriggerHotkey
        {
            Ctrl = true,
            Shift = true,
            KeyCode = 0x45
        });

        Assert.False(trigger.ProcessKeyEvent(0xA2, isKeyDown: true));
        Assert.False(trigger.ProcessKeyEvent(0xA0, isKeyDown: true));
        Assert.True(trigger.ProcessKeyEvent(0x45, isKeyDown: true));
    }
}
