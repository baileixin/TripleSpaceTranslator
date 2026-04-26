using System.Diagnostics;
using System.Runtime.InteropServices;
using TripleSpaceTranslator.Core.Interop;

namespace TripleSpaceTranslator.App.Infrastructure;

public sealed class GlobalKeyboardHook : IDisposable
{
    private readonly Func<LowLevelKeyboardEvent, bool> _handler;
    private readonly NativeMethods.LowLevelKeyboardProc _hookCallback;

    private bool _disposed;
    private IntPtr _hookHandle;

    public GlobalKeyboardHook(Func<LowLevelKeyboardEvent, bool> handler)
    {
        _handler = handler;
        _hookCallback = HookCallback;
    }

    public void Start()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return;
        }

        using var currentProcess = Process.GetCurrentProcess();
        using var currentModule = currentProcess.MainModule;
        var moduleHandle = NativeMethods.GetModuleHandle(currentModule?.ModuleName);
        _hookHandle = NativeMethods.SetWindowsHookEx(
            NativeMethods.WhKeyboardLl,
            _hookCallback,
            moduleHandle,
            0);

        if (_hookHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Unable to install keyboard hook. Win32 error: {Marshal.GetLastWin32Error()}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        var isKeyDown = wParam == (IntPtr)NativeMethods.WmKeydown || wParam == (IntPtr)NativeMethods.WmSyskeydown;
        var isKeyUp = wParam == (IntPtr)NativeMethods.WmKeyup || wParam == (IntPtr)NativeMethods.WmSyskeyup;

        if (code >= 0 && (isKeyDown || isKeyUp))
        {
            var hookStruct = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            var keyboardEvent = new LowLevelKeyboardEvent
            {
                VirtualKeyCode = (int)hookStruct.vkCode,
                IsKeyDown = isKeyDown,
                Timestamp = DateTimeOffset.UtcNow
            };

            if (_handler(keyboardEvent))
            {
                return new IntPtr(1);
            }
        }

        return NativeMethods.CallNextHookEx(_hookHandle, code, wParam, lParam);
    }
}
