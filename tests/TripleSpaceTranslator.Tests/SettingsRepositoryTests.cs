using System.Text.Json;
using TripleSpaceTranslator.Core.Models;
using TripleSpaceTranslator.Core.Services;

namespace TripleSpaceTranslator.Tests;

public sealed class SettingsRepositoryTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveAndLoadAsync_PersistsSettingsAndEncryptsApiKeySeparately()
    {
        Directory.CreateDirectory(_tempDirectory);

        var settingsFile = Path.Combine(_tempDirectory, "settings.json");
        var secretStore = new DpapiSecretStore(Path.Combine(_tempDirectory, "secrets"));
        var repository = new SettingsRepository(settingsFile, secretStore);

        var settings = new AppSettings
        {
            RunAtStartup = true,
            DefaultTargetLanguage = "en",
            NotificationsEnabled = false,
            TripleSpaceWindowMs = 900,
            TriggerHotkey = new TriggerHotkey
            {
                Ctrl = true,
                Shift = true,
                KeyCode = 0x45
            },
            ProviderConfig = new TranslationProviderConfig
            {
                ProviderType = TranslationProviderType.TencentMachineTranslation,
                SecretId = "AKIDEXAMPLE",
                SecretKey = "SECRETEXAMPLE",
                Region = "ap-shanghai",
                ProjectId = 1001,
                TimeoutSeconds = 7
            }
        };

        await repository.SaveAsync(settings);
        var loaded = await repository.LoadAsync();

        Assert.True(loaded.RunAtStartup);
        Assert.Equal("en", loaded.DefaultTargetLanguage);
        Assert.False(loaded.NotificationsEnabled);
        Assert.Equal(900, loaded.TripleSpaceWindowMs);
        Assert.True(loaded.TriggerHotkey.Ctrl);
        Assert.False(loaded.TriggerHotkey.Alt);
        Assert.True(loaded.TriggerHotkey.Shift);
        Assert.Equal(0x45, loaded.TriggerHotkey.KeyCode);
        Assert.Equal("AKIDEXAMPLE", loaded.ProviderConfig.SecretId);
        Assert.Equal("SECRETEXAMPLE", loaded.ProviderConfig.SecretKey);
        Assert.Equal("ap-shanghai", loaded.ProviderConfig.Region);
        Assert.Equal(1001, loaded.ProviderConfig.ProjectId);
        Assert.Equal(7, loaded.ProviderConfig.TimeoutSeconds);

        var rawJson = await File.ReadAllTextAsync(settingsFile);
        Assert.DoesNotContain("AKIDEXAMPLE", rawJson, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRETEXAMPLE", rawJson, StringComparison.Ordinal);

        var document = JsonDocument.Parse(rawJson);
        Assert.Equal(string.Empty, document.RootElement.GetProperty("ProviderConfig").GetProperty("SecretId").GetString());
        Assert.Equal(string.Empty, document.RootElement.GetProperty("ProviderConfig").GetProperty("SecretKey").GetString());
    }

    [Fact]
    public async Task LoadAsync_WhenJsonIsCorrupted_ReturnsDefaults()
    {
        Directory.CreateDirectory(_tempDirectory);

        var settingsFile = Path.Combine(_tempDirectory, "settings.json");
        await File.WriteAllTextAsync(settingsFile, "{ this is not valid json", System.Text.Encoding.UTF8);

        var secretStore = new DpapiSecretStore(Path.Combine(_tempDirectory, "secrets"));
        var repository = new SettingsRepository(settingsFile, secretStore);

        var loaded = await repository.LoadAsync();

        Assert.False(loaded.RunAtStartup);
        Assert.Equal("en", loaded.DefaultTargetLanguage);
        Assert.True(loaded.NotificationsEnabled);
        Assert.Equal(800, loaded.TripleSpaceWindowMs);
        Assert.True(loaded.TriggerHotkey.Ctrl);
        Assert.True(loaded.TriggerHotkey.Alt);
        Assert.Equal(0x51, loaded.TriggerHotkey.KeyCode);
        Assert.Equal(string.Empty, loaded.ProviderConfig.SecretId);
        Assert.Equal(string.Empty, loaded.ProviderConfig.SecretKey);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
