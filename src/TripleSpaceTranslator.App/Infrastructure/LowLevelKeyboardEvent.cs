namespace TripleSpaceTranslator.App.Infrastructure;

public sealed class LowLevelKeyboardEvent
{
    public bool IsKeyDown { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public int VirtualKeyCode { get; init; }
}
