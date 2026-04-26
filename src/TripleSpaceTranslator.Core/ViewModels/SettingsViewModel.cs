using System.Windows.Input;
using TripleSpaceTranslator.Core.Models;
using TripleSpaceTranslator.Core.Utilities;

namespace TripleSpaceTranslator.Core.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private const string DefaultTencentRegion = "ap-guangzhou";

    private readonly Func<AppSettings, Task> _saveSettingsAsync;
    private readonly Func<TranslationProviderConfig, string, Task<ConnectionTestResult>> _testConnectionAsync;
    private string _connectionStatusMessage = string.Empty;
    private bool _notificationsEnabled;
    private int _projectId;
    private string _region = string.Empty;
    private bool _runAtStartup;
    private SupportedLanguage _selectedLanguage;
    private TranslationProviderOption _selectedProvider;
    private HotkeyKeyOption _selectedShortcutKey;
    private string _secretId = string.Empty;
    private string _secretKey = string.Empty;
    private bool _showAdvancedProviderSettings;
    private int _timeoutSeconds;
    private bool _triggerAlt;
    private bool _triggerCtrl;
    private bool _triggerShift;
    private bool _triggerWin;

    public SettingsViewModel(
        AppSettings settings,
        Func<AppSettings, Task> saveSettingsAsync,
        Func<TranslationProviderConfig, string, Task<ConnectionTestResult>> testConnectionAsync)
    {
        _saveSettingsAsync = saveSettingsAsync;
        _testConnectionAsync = testConnectionAsync;

        AvailableLanguages = SupportedLanguageCatalog.All;
        AvailableProviders = TranslationProviderCatalog.All;
        AvailableShortcutKeys = HotkeyCatalog.AllKeys;

        var hotkey = settings.TriggerHotkey ?? new TriggerHotkey();

        _runAtStartup = settings.RunAtStartup;
        _notificationsEnabled = settings.NotificationsEnabled;
        _selectedLanguage = SupportedLanguageCatalog.GetByCode(settings.DefaultTargetLanguage);
        _selectedProvider = TranslationProviderCatalog.GetByType(settings.ProviderConfig.ProviderType);
        _secretId = settings.ProviderConfig.SecretId;
        _secretKey = settings.ProviderConfig.SecretKey;
        _region = settings.ProviderConfig.Region;
        _projectId = settings.ProviderConfig.ProjectId;
        _timeoutSeconds = settings.ProviderConfig.TimeoutSeconds;
        _showAdvancedProviderSettings = _selectedProvider.SupportsAdvancedSettings &&
                                        ((!string.IsNullOrWhiteSpace(_region) &&
                                          !string.Equals(_region, DefaultTencentRegion, StringComparison.OrdinalIgnoreCase)) ||
                                         _projectId > 0);
        _triggerCtrl = hotkey.Ctrl;
        _triggerAlt = hotkey.Alt;
        _triggerShift = hotkey.Shift;
        _triggerWin = hotkey.Win;
        _selectedShortcutKey = HotkeyCatalog.GetByVirtualKeyCode(hotkey.KeyCode);

        EnsureProviderDefaults();

        SaveCommand = new AsyncRelayCommand(SaveAsync, CanSaveSettings);
        TestConnectionCommand = new AsyncRelayCommand(
            TestConnectionAsync,
            () => !string.IsNullOrWhiteSpace(SecretId) && !string.IsNullOrWhiteSpace(SecretKey));
        TestConnectionCommand.ExecutionStateChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsTestingConnection));
            OnPropertyChanged(nameof(CanTestConnection));
        };
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler? CloseRequested;

    public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; }

    public IReadOnlyList<TranslationProviderOption> AvailableProviders { get; }

    public IReadOnlyList<HotkeyKeyOption> AvailableShortcutKeys { get; }

    public bool CanSaveShortcut => CanSaveSettings();

    public bool CanTestConnection =>
        !string.IsNullOrWhiteSpace(SecretId) &&
        !string.IsNullOrWhiteSpace(SecretKey) &&
        !IsTestingConnection;

    public ICommand CloseCommand { get; }

    public string ConnectionStatusMessage
    {
        get => _connectionStatusMessage;
        private set => SetProperty(ref _connectionStatusMessage, value);
    }

    public string CredentialIdLabel => SelectedProvider.CredentialIdLabel;

    public string CredentialKeyLabel => SelectedProvider.CredentialKeyLabel;

    public string HotkeyDisplayText => HotkeyDisplayFormatter.Format(BuildTriggerHotkey());

    public bool IsTestingConnection => TestConnectionCommand.IsRunning;

    public bool NotificationsEnabled
    {
        get => _notificationsEnabled;
        set => SetProperty(ref _notificationsEnabled, value);
    }

    public int ProjectId
    {
        get => _projectId;
        set => SetProperty(ref _projectId, value);
    }

    public string ProviderConnectionTestHint => SelectedProvider.ConnectionTestHint;

    public string ProviderDescription => SelectedProvider.Description;

    public string Region
    {
        get => _region;
        set => SetProperty(ref _region, value);
    }

    public bool RunAtStartup
    {
        get => _runAtStartup;
        set => SetProperty(ref _runAtStartup, value);
    }

    public AsyncRelayCommand SaveCommand { get; }

    public SupportedLanguage SelectedLanguage
    {
        get => _selectedLanguage;
        set => SetProperty(ref _selectedLanguage, value);
    }

    public TranslationProviderOption SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            if (!SetProperty(ref _selectedProvider, value))
            {
                return;
            }

            EnsureProviderDefaults();
            if (!value.SupportsAdvancedSettings)
            {
                ShowAdvancedProviderSettings = false;
            }

            ConnectionStatusMessage = string.Empty;
            OnPropertyChanged(nameof(CredentialIdLabel));
            OnPropertyChanged(nameof(CredentialKeyLabel));
            OnPropertyChanged(nameof(ProviderDescription));
            OnPropertyChanged(nameof(ProviderConnectionTestHint));
            OnPropertyChanged(nameof(ShowAdvancedProviderSettingsToggle));
            OnPropertyChanged(nameof(ShowRegionSettings));
            OnPropertyChanged(nameof(ShowProjectIdSettings));
        }
    }

    public HotkeyKeyOption SelectedShortcutKey
    {
        get => _selectedShortcutKey;
        set
        {
            if (SetProperty(ref _selectedShortcutKey, value))
            {
                OnHotkeyChanged();
            }
        }
    }

    public string SecretId
    {
        get => _secretId;
        set
        {
            if (SetProperty(ref _secretId, value))
            {
                TestConnectionCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(CanTestConnection));
            }
        }
    }

    public string SecretKey
    {
        get => _secretKey;
        set
        {
            if (SetProperty(ref _secretKey, value))
            {
                TestConnectionCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(CanTestConnection));
            }
        }
    }

    public bool ShowAdvancedProviderSettings
    {
        get => _showAdvancedProviderSettings;
        set
        {
            if (SetProperty(ref _showAdvancedProviderSettings, value))
            {
                OnPropertyChanged(nameof(ShowRegionSettings));
                OnPropertyChanged(nameof(ShowProjectIdSettings));
            }
        }
    }

    public bool ShowAdvancedProviderSettingsToggle => SelectedProvider.SupportsAdvancedSettings;

    public bool ShowProjectIdSettings =>
        SelectedProvider.ProviderType == TranslationProviderType.TencentMachineTranslation &&
        ShowAdvancedProviderSettings;

    public bool ShowRegionSettings =>
        SelectedProvider.ProviderType == TranslationProviderType.TencentMachineTranslation &&
        ShowAdvancedProviderSettings;

    public AsyncRelayCommand TestConnectionCommand { get; }

    public int TimeoutSeconds
    {
        get => _timeoutSeconds;
        set => SetProperty(ref _timeoutSeconds, value);
    }

    public bool TriggerAlt
    {
        get => _triggerAlt;
        set
        {
            if (SetProperty(ref _triggerAlt, value))
            {
                OnHotkeyChanged();
            }
        }
    }

    public bool TriggerCtrl
    {
        get => _triggerCtrl;
        set
        {
            if (SetProperty(ref _triggerCtrl, value))
            {
                OnHotkeyChanged();
            }
        }
    }

    public bool TriggerShift
    {
        get => _triggerShift;
        set
        {
            if (SetProperty(ref _triggerShift, value))
            {
                OnHotkeyChanged();
            }
        }
    }

    public bool TriggerWin
    {
        get => _triggerWin;
        set
        {
            if (SetProperty(ref _triggerWin, value))
            {
                OnHotkeyChanged();
            }
        }
    }

    private async Task SaveAsync()
    {
        await _saveSettingsAsync(BuildSettings()).ConfigureAwait(false);
        ConnectionStatusMessage = $"设置已保存，当前快捷键：{HotkeyDisplayText}";
    }

    private async Task TestConnectionAsync()
    {
        ConnectionStatusMessage = "正在测试连接...";
        var result = await _testConnectionAsync(BuildSettings().ProviderConfig, SelectedLanguage.Code).ConfigureAwait(false);
        ConnectionStatusMessage = result.Succeeded
            ? $"连接成功（{result.ResponseTimeMs} ms）"
            : result.Message;
    }

    private AppSettings BuildSettings()
    {
        var providerType = SelectedProvider.ProviderType;
        var providerConfig = new TranslationProviderConfig
        {
            ProviderType = providerType,
            SecretId = SecretId.Trim(),
            SecretKey = SecretKey.Trim(),
            TimeoutSeconds = Math.Clamp(TimeoutSeconds, 5, 8)
        };

        if (providerType == TranslationProviderType.TencentMachineTranslation)
        {
            providerConfig.Region = string.IsNullOrWhiteSpace(Region) ? DefaultTencentRegion : Region.Trim();
            providerConfig.ProjectId = Math.Max(ProjectId, 0);
            providerConfig.Endpoint = "https://tmt.tencentcloudapi.com/";
        }
        else
        {
            providerConfig.Region = string.Empty;
            providerConfig.ProjectId = 0;
            providerConfig.Endpoint = "https://fanyi-api.baidu.com/api/trans/vip/translate";
        }

        return new AppSettings
        {
            RunAtStartup = RunAtStartup,
            NotificationsEnabled = NotificationsEnabled,
            DefaultTargetLanguage = SelectedLanguage.Code,
            TripleSpaceWindowMs = 800,
            TriggerHotkey = BuildTriggerHotkey(),
            ProviderConfig = providerConfig
        };
    }

    private TriggerHotkey BuildTriggerHotkey()
    {
        return new TriggerHotkey
        {
            Ctrl = TriggerCtrl,
            Alt = TriggerAlt,
            Shift = TriggerShift,
            Win = TriggerWin,
            KeyCode = SelectedShortcutKey?.VirtualKeyCode ?? 0x51
        };
    }

    private bool CanSaveSettings()
    {
        return BuildTriggerHotkey().IsValid();
    }

    private void EnsureProviderDefaults()
    {
        if (SelectedProvider.ProviderType == TranslationProviderType.TencentMachineTranslation &&
            string.IsNullOrWhiteSpace(Region))
        {
            Region = DefaultTencentRegion;
        }
    }

    private void OnHotkeyChanged()
    {
        OnPropertyChanged(nameof(HotkeyDisplayText));
        OnPropertyChanged(nameof(CanSaveShortcut));
        SaveCommand.RaiseCanExecuteChanged();
    }
}
