using TripleSpaceTranslator.Core.Models;
using TripleSpaceTranslator.Core.Utilities;
using TripleSpaceTranslator.Core.ViewModels;

namespace TripleSpaceTranslator.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void TestConnectionCommand_IsDisabledWithoutCredentials()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.TestConnectionCommand.CanExecute(null));

        viewModel.SecretId = "AKIDDEMO";
        Assert.False(viewModel.TestConnectionCommand.CanExecute(null));

        viewModel.SecretKey = "SECRETDEMO";

        Assert.True(viewModel.TestConnectionCommand.CanExecute(null));
    }

    [Fact]
    public void SaveCommand_IsDisabledWhenNoModifierIsSelected()
    {
        var viewModel = CreateViewModel();

        viewModel.TriggerCtrl = false;
        viewModel.TriggerAlt = false;
        viewModel.TriggerShift = false;
        viewModel.TriggerWin = false;

        Assert.False(viewModel.SaveCommand.CanExecute(null));
        Assert.Equal("Q", viewModel.SelectedShortcutKey.DisplayName);
    }

    [Fact]
    public async Task TestConnectionCommand_UpdatesStatusAndRunningState()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var viewModel = new SettingsViewModel(
            CreateSettings("AKIDDEMO", "SECRETDEMO"),
            _ => Task.CompletedTask,
            async (_, _) =>
            {
                started.SetResult();
                await release.Task;
                return ConnectionTestResult.Success(42);
            });

        var execution = viewModel.TestConnectionCommand.ExecuteAsync();

        await started.Task;
        Assert.True(viewModel.IsTestingConnection);
        Assert.Equal("正在测试连接...", viewModel.ConnectionStatusMessage);

        release.SetResult();
        await execution;

        Assert.False(viewModel.IsTestingConnection);
        Assert.Equal("连接成功（42 ms）", viewModel.ConnectionStatusMessage);
    }

    [Fact]
    public async Task SaveCommand_UsesSelectedBaiduProviderAndMinimalFields()
    {
        AppSettings? capturedSettings = null;
        var viewModel = new SettingsViewModel(
            CreateSettings(),
            settings =>
            {
                capturedSettings = settings;
                return Task.CompletedTask;
            },
            (_, _) => Task.FromResult(ConnectionTestResult.Success(10)));

        viewModel.SelectedProvider = TranslationProviderCatalog.GetByType(TranslationProviderType.BaiduGeneralTextTranslation);
        viewModel.SecretId = "BAIDU_APP_ID";
        viewModel.SecretKey = "BAIDU_APP_KEY";

        await viewModel.SaveCommand.ExecuteAsync();

        Assert.NotNull(capturedSettings);
        Assert.Equal(TranslationProviderType.BaiduGeneralTextTranslation, capturedSettings!.ProviderConfig.ProviderType);
        Assert.Equal("BAIDU_APP_ID", capturedSettings.ProviderConfig.SecretId);
        Assert.Equal("BAIDU_APP_KEY", capturedSettings.ProviderConfig.SecretKey);
        Assert.Equal(string.Empty, capturedSettings.ProviderConfig.Region);
        Assert.Equal(0, capturedSettings.ProviderConfig.ProjectId);
        Assert.Equal("https://fanyi-api.baidu.com/api/trans/vip/translate", capturedSettings.ProviderConfig.Endpoint);
    }

    [Fact]
    public void SelectedProvider_UpdatesLabelsAndAdvancedVisibility()
    {
        var viewModel = CreateViewModel();

        Assert.Equal("SecretId", viewModel.CredentialIdLabel);
        Assert.True(viewModel.ShowAdvancedProviderSettingsToggle);

        viewModel.SelectedProvider = TranslationProviderCatalog.GetByType(TranslationProviderType.BaiduGeneralTextTranslation);

        Assert.Equal("AppId", viewModel.CredentialIdLabel);
        Assert.Equal("AppKey", viewModel.CredentialKeyLabel);
        Assert.False(viewModel.ShowAdvancedProviderSettingsToggle);
        Assert.False(viewModel.ShowRegionSettings);
        Assert.False(viewModel.ShowProjectIdSettings);
    }

    private static SettingsViewModel CreateViewModel()
    {
        return new SettingsViewModel(
            CreateSettings(),
            _ => Task.CompletedTask,
            (_, _) => Task.FromResult(ConnectionTestResult.Success(10)));
    }

    private static AppSettings CreateSettings(string secretId = "", string secretKey = "")
    {
        return new AppSettings
        {
            DefaultTargetLanguage = "en",
            TriggerHotkey = new TriggerHotkey
            {
                Ctrl = true,
                Alt = true,
                KeyCode = 0x51
            },
            ProviderConfig = new TranslationProviderConfig
            {
                ProviderType = TranslationProviderType.TencentMachineTranslation,
                SecretId = secretId,
                SecretKey = secretKey,
                Region = "ap-guangzhou",
                ProjectId = 0,
                TimeoutSeconds = 6
            }
        };
    }
}
