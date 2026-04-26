using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using TripleSpaceTranslator.App.Infrastructure;
using TripleSpaceTranslator.App.Windows;
using TripleSpaceTranslator.Core.Interfaces;
using TripleSpaceTranslator.Core.Interop;
using TripleSpaceTranslator.Core.Models;
using TripleSpaceTranslator.Core.Services;
using TripleSpaceTranslator.Core.Utilities;
using TripleSpaceTranslator.Core.ViewModels;
using Forms = System.Windows.Forms;

namespace TripleSpaceTranslator.App;

public partial class App : System.Windows.Application
{
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly SemaphoreSlim _translationSemaphore = new(1, 1);

    private AppSettings _currentSettings = new();
    private IDiagnosticLogger _diagnosticLogger = NullDiagnosticLogger.Instance;
    private FocusedTextAccessor? _focusedTextAccessor;
    private GlobalHotkeyTrigger? _hotkeyTrigger;
    private GlobalKeyboardHook? _keyboardHook;
    private HttpClient? _httpClient;
    private NotifyIconHost? _notifyIconHost;
    private ISettingsRepository? _settingsRepository;
    private IStartupRegistrationService? _startupRegistrationService;
    private SettingsWindow? _settingsWindow;
    private TranslationProviderFactory? _translationProviderFactory;
    private TripleSpaceTranslationCoordinator? _translationCoordinator;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        try
        {
            Directory.CreateDirectory(AppDataPaths.BaseDirectory);
            _diagnosticLogger = new FileDiagnosticLogger(Path.Combine(AppDataPaths.BaseDirectory, "logs", "app.log"));
            _diagnosticLogger.Log("startup", $"Application starting. ProcessId={Environment.ProcessId}, BaseDirectory={AppDomain.CurrentDomain.BaseDirectory}.");

            var secretStore = new DpapiSecretStore(AppDataPaths.SecretsDirectory);
            _settingsRepository = new SettingsRepository(AppDataPaths.SettingsFilePath, secretStore);
            _currentSettings = await _settingsRepository.LoadAsync(_shutdownCts.Token);
            _diagnosticLogger.Log(
                "startup",
                $"Settings loaded. TargetLanguage={_currentSettings.DefaultTargetLanguage}, Hotkey={HotkeyDisplayFormatter.Format(_currentSettings.TriggerHotkey)}, NotificationsEnabled={_currentSettings.NotificationsEnabled}, Provider={_currentSettings.ProviderConfig.ProviderType}, Region={_currentSettings.ProviderConfig.Region}, ProjectId={_currentSettings.ProviderConfig.ProjectId}, HasSecretId={!string.IsNullOrWhiteSpace(_currentSettings.ProviderConfig.SecretId)}, HasSecretKey={!string.IsNullOrWhiteSpace(_currentSettings.ProviderConfig.SecretKey)}.");

            _httpClient = new HttpClient();
            _translationProviderFactory = new TranslationProviderFactory(_httpClient);
            _startupRegistrationService = new StartupRegistrationService("TripleSpaceTranslator");
            _focusedTextAccessor = new FocusedTextAccessor(new FocusContextProvider(), _diagnosticLogger);
            _translationCoordinator = new TripleSpaceTranslationCoordinator(_focusedTextAccessor, _translationProviderFactory, _diagnosticLogger);
            _hotkeyTrigger = new GlobalHotkeyTrigger(_currentSettings.TriggerHotkey);

            _notifyIconHost = new NotifyIconHost();
            _notifyIconHost.SettingsRequested += OnSettingsRequested;
            _notifyIconHost.ExitRequested += OnExitRequested;

            _keyboardHook = new GlobalKeyboardHook(HandleLowLevelKeyboardEvent);
            _keyboardHook.Start();
            _diagnosticLogger.Log("startup", "Keyboard hook installed successfully.");
        }
        catch (Exception ex)
        {
            _diagnosticLogger.Log("startup", $"Startup failed: {ex}.");
            System.Windows.MessageBox.Show(
                $"应用启动失败：{ex.Message}",
                "TripleSpaceTranslator",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _diagnosticLogger.Log("shutdown", "Application exiting.");
        _shutdownCts.Cancel();
        _keyboardHook?.Dispose();
        _notifyIconHost?.Dispose();
        _httpClient?.Dispose();
        _translationSemaphore.Dispose();
        _shutdownCts.Dispose();

        base.OnExit(e);
    }

    private bool HandleLowLevelKeyboardEvent(LowLevelKeyboardEvent keyboardEvent)
    {
        if (_hotkeyTrigger is null)
        {
            return false;
        }

        var shouldTrigger = _hotkeyTrigger.ProcessKeyEvent(keyboardEvent.VirtualKeyCode, keyboardEvent.IsKeyDown);
        if (!shouldTrigger)
        {
            return false;
        }

        var hotkeySnapshot = _currentSettings.TriggerHotkey.Clone();
        _diagnosticLogger.Log(
            "keys",
            $"Hotkey trigger accepted. Hotkey={HotkeyDisplayFormatter.Format(hotkeySnapshot)}, Vk=0x{keyboardEvent.VirtualKeyCode:X2}.");
        _ = BeginTranslationAfterHotkeyReleaseAsync(hotkeySnapshot);
        return true;
    }

    private async Task BeginTranslationAfterHotkeyReleaseAsync(TriggerHotkey triggerHotkey)
    {
        var released = await WaitForHotkeyReleaseAsync(triggerHotkey, _shutdownCts.Token).ConfigureAwait(false);
        _diagnosticLogger.Log(
            "keys",
            released
                ? $"Hotkey released. Starting translation. Hotkey={HotkeyDisplayFormatter.Format(triggerHotkey)}."
                : $"Hotkey release wait timed out. Starting translation anyway. Hotkey={HotkeyDisplayFormatter.Format(triggerHotkey)}.");

        await BeginTranslationAsync().ConfigureAwait(false);
    }

    private static async Task<bool> WaitForHotkeyReleaseAsync(TriggerHotkey triggerHotkey, CancellationToken cancellationToken)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(1500);

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsHotkeyOrModifierStillDown(triggerHotkey))
            {
                await Task.Delay(80, cancellationToken).ConfigureAwait(false);
                return true;
            }

            await Task.Delay(20, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    private static bool IsHotkeyOrModifierStillDown(TriggerHotkey triggerHotkey)
    {
        return IsKeyDown(triggerHotkey.KeyCode) ||
               IsKeyDown(NativeMethods.VkControl) ||
               IsKeyDown(NativeMethods.VkLControl) ||
               IsKeyDown(NativeMethods.VkRControl) ||
               IsKeyDown(NativeMethods.VkMenu) ||
               IsKeyDown(NativeMethods.VkLMenu) ||
               IsKeyDown(NativeMethods.VkRMenu) ||
               IsKeyDown(NativeMethods.VkShift) ||
               IsKeyDown(NativeMethods.VkLShift) ||
               IsKeyDown(NativeMethods.VkRShift) ||
               IsKeyDown(NativeMethods.VkLWin) ||
               IsKeyDown(NativeMethods.VkRWin);
    }

    private async Task BeginTranslationAsync()
    {
        if (_translationCoordinator is null)
        {
            _diagnosticLogger.Log("translate", "BeginTranslationAsync skipped because coordinator was null.");
            return;
        }

        if (!await _translationSemaphore.WaitAsync(0).ConfigureAwait(false))
        {
            _diagnosticLogger.Log("translate", "BeginTranslationAsync skipped because another translation was already in progress.");
            return;
        }

        try
        {
            var result = await _translationCoordinator
                .TranslateFocusedTextAsync(_currentSettings.Clone(), _shutdownCts.Token)
                .ConfigureAwait(false);

            _diagnosticLogger.Log("translate", $"Translation finished. Succeeded={result.Succeeded}, Message={result.Message}.");

            if (!result.Succeeded && _currentSettings.NotificationsEnabled)
            {
                Current.Dispatcher.Invoke(() =>
                {
                    _notifyIconHost?.ShowMessage("TripleSpaceTranslator", result.Message, Forms.ToolTipIcon.Warning);
                });
            }
        }
        finally
        {
            _translationSemaphore.Release();
        }
    }

    private async Task SaveSettingsAsync(AppSettings settings)
    {
        if (_settingsRepository is null || _startupRegistrationService is null || _hotkeyTrigger is null)
        {
            return;
        }

        _currentSettings = settings.Clone();
        await _settingsRepository.SaveAsync(_currentSettings, _shutdownCts.Token);
        _diagnosticLogger.Log(
            "settings",
            $"Settings saved. TargetLanguage={_currentSettings.DefaultTargetLanguage}, Hotkey={HotkeyDisplayFormatter.Format(_currentSettings.TriggerHotkey)}, NotificationsEnabled={_currentSettings.NotificationsEnabled}, Region={_currentSettings.ProviderConfig.Region}, ProjectId={_currentSettings.ProviderConfig.ProjectId}, HasSecretId={!string.IsNullOrWhiteSpace(_currentSettings.ProviderConfig.SecretId)}, HasSecretKey={!string.IsNullOrWhiteSpace(_currentSettings.ProviderConfig.SecretKey)}.");

        var executablePath = Environment.ProcessPath ?? throw new InvalidOperationException("Unable to locate the current executable path.");
        _startupRegistrationService.SetEnabled(executablePath, _currentSettings.RunAtStartup);
        _hotkeyTrigger.UpdateHotkey(_currentSettings.TriggerHotkey);

        _notifyIconHost?.ShowMessage("TripleSpaceTranslator", $"设置已保存，当前快捷键：{HotkeyDisplayFormatter.Format(_currentSettings.TriggerHotkey)}", Forms.ToolTipIcon.Info);
    }

    private async Task<ConnectionTestResult> TestConnectionAsync(TranslationProviderConfig providerConfig, string targetLanguage)
    {
        if (_translationProviderFactory is null)
        {
            return ConnectionTestResult.Failure("not_ready", "翻译服务尚未初始化。");
        }

        _diagnosticLogger.Log(
            "settings",
            $"Connection test started. TargetLanguage={targetLanguage}, Region={providerConfig.Region}, ProjectId={providerConfig.ProjectId}, TimeoutSeconds={providerConfig.TimeoutSeconds}, HasSecretId={!string.IsNullOrWhiteSpace(providerConfig.SecretId)}, HasSecretKey={!string.IsNullOrWhiteSpace(providerConfig.SecretKey)}.");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_shutdownCts.Token);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(providerConfig.TimeoutSeconds, 5, 8)));

        var provider = _translationProviderFactory.Create(providerConfig);
        var result = await provider.TestConnectionAsync(providerConfig, targetLanguage, cts.Token);
        _diagnosticLogger.Log("settings", $"Connection test finished. Succeeded={result.Succeeded}, ErrorCode={result.ErrorCode}, ResponseTimeMs={result.ResponseTimeMs}, Message={result.Message}.");
        return result;
    }

    private void OpenSettingsWindow()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            _settingsWindow.Focus();
            return;
        }

        var viewModel = new SettingsViewModel(_currentSettings.Clone(), SaveSettingsAsync, TestConnectionAsync);
        viewModel.CloseRequested += (_, _) => _settingsWindow?.Close();

        _settingsWindow = new SettingsWindow
        {
            DataContext = viewModel
        };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void ExitApplication()
    {
        _settingsWindow?.Close();
        Shutdown();
    }

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        OpenSettingsWindow();
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        ExitApplication();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _diagnosticLogger.Log("error", $"Unhandled dispatcher exception: {e.Exception}.");
        _notifyIconHost?.ShowMessage("TripleSpaceTranslator", $"发生未处理异常：{e.Exception.Message}", Forms.ToolTipIcon.Error);
        e.Handled = true;
    }

    private static bool IsKeyDown(int virtualKeyCode)
    {
        return (NativeMethods.GetAsyncKeyState(virtualKeyCode) & 0x8000) != 0;
    }
}
