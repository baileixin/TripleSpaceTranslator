using Microsoft.Win32;
using TripleSpaceTranslator.Core.Interfaces;

namespace TripleSpaceTranslator.Core.Services;

public sealed class StartupRegistrationService : IStartupRegistrationService
{
    private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _applicationName;

    public StartupRegistrationService(string applicationName)
    {
        _applicationName = applicationName;
    }

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: false);
        var configured = key?.GetValue(_applicationName) as string;
        return !string.IsNullOrWhiteSpace(configured);
    }

    public void SetEnabled(string executablePath, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path is required.", nameof(executablePath));
        }

        using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
        if (enabled)
        {
            key.SetValue(_applicationName, $"\"{executablePath}\"");
            return;
        }

        key.DeleteValue(_applicationName, throwOnMissingValue: false);
    }
}
