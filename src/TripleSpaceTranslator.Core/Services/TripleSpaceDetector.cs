namespace TripleSpaceTranslator.Core.Services;

public sealed class TripleSpaceDetector
{
    private readonly int _triggerCount;
    private string _lastFocusContextId = string.Empty;
    private DateTimeOffset _lastPressedAt;
    private int _spacePressCount;
    private TimeSpan _window;

    public TripleSpaceDetector(int windowMilliseconds, int triggerCount = 3)
    {
        if (windowMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowMilliseconds));
        }

        if (triggerCount < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(triggerCount));
        }

        _window = TimeSpan.FromMilliseconds(windowMilliseconds);
        _triggerCount = triggerCount;
    }

    public bool RegisterSpacePress(string focusContextId, DateTimeOffset timestamp, bool hasModifierKeys)
    {
        if (hasModifierKeys || string.IsNullOrWhiteSpace(focusContextId))
        {
            Reset();
            return false;
        }

        if (_spacePressCount == 0 ||
            !string.Equals(_lastFocusContextId, focusContextId, StringComparison.Ordinal) ||
            timestamp - _lastPressedAt > _window)
        {
            _spacePressCount = 1;
            _lastFocusContextId = focusContextId;
            _lastPressedAt = timestamp;
            return false;
        }

        _spacePressCount++;
        _lastFocusContextId = focusContextId;
        _lastPressedAt = timestamp;

        if (_spacePressCount >= _triggerCount)
        {
            Reset();
            return true;
        }

        return false;
    }

    public void RegisterNonSpacePress()
    {
        Reset();
    }

    public void UpdateWindow(int windowMilliseconds)
    {
        if (windowMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowMilliseconds));
        }

        _window = TimeSpan.FromMilliseconds(windowMilliseconds);
        Reset();
    }

    public void Reset()
    {
        _spacePressCount = 0;
        _lastFocusContextId = string.Empty;
        _lastPressedAt = default;
    }
}
