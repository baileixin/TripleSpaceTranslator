using TripleSpaceTranslator.Core.Models;
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
            new AppSettings
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
                    SecretId = "AKIDDEMO",
                    SecretKey = "SECRETDEMO",
                    Region = "ap-guangzhou",
                    ProjectId = 0,
                    TimeoutSeconds = 6
                }
            },
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

    private static SettingsViewModel CreateViewModel()
    {
        return new SettingsViewModel(
            new AppSettings
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
                    SecretId = string.Empty,
                    SecretKey = string.Empty,
                    Region = "ap-guangzhou",
                    ProjectId = 0,
                    TimeoutSeconds = 6
                }
            },
            _ => Task.CompletedTask,
            (_, _) => Task.FromResult(ConnectionTestResult.Success(10)));
    }
}
