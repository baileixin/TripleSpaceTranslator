using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TripleSpaceTranslator.Core.Interfaces;
using TripleSpaceTranslator.Core.Models;

namespace TripleSpaceTranslator.Core.Services;

public sealed class SettingsRepository : ISettingsRepository
{
    private const string ProviderSecretIdSecretName = "provider-secret-id";
    private const string ProviderSecretKeySecretName = "provider-secret-key";
    private const string LegacyProviderApiKeySecretName = "provider-api-key";

    private readonly JsonSerializerOptions _serializerOptions;
    private readonly string _settingsFilePath;
    private readonly ISecretStore _secretStore;

    public SettingsRepository(string settingsFilePath, ISecretStore secretStore)
    {
        _settingsFilePath = settingsFilePath;
        _secretStore = secretStore;
        _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
        _serializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        AppSettings settings;

        if (!File.Exists(_settingsFilePath))
        {
            settings = new AppSettings();
        }
        else
        {
            try
            {
                await using var stream = File.OpenRead(_settingsFilePath);
                settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, _serializerOptions, cancellationToken).ConfigureAwait(false)
                    ?? new AppSettings();
            }
            catch (JsonException)
            {
                settings = new AppSettings();
                await WriteDefaultsAsync(settings, cancellationToken).ConfigureAwait(false);
            }
        }

        settings.ProviderConfig ??= new TranslationProviderConfig();
        settings.TriggerHotkey ??= TriggerHotkey.CreateDefault();
        if (!settings.TriggerHotkey.IsValid())
        {
            settings.TriggerHotkey = TriggerHotkey.CreateDefault();
        }
        settings.ProviderConfig.SecretId = await _secretStore.LoadSecretAsync(ProviderSecretIdSecretName, cancellationToken).ConfigureAwait(false) ?? string.Empty;
        settings.ProviderConfig.SecretKey = await _secretStore.LoadSecretAsync(ProviderSecretKeySecretName, cancellationToken).ConfigureAwait(false) ?? string.Empty;

        return settings;
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);

        var clone = settings.Clone();
        var secretId = clone.ProviderConfig.SecretId;
        var secretKey = clone.ProviderConfig.SecretKey;
        clone.ProviderConfig.SecretId = string.Empty;
        clone.ProviderConfig.SecretKey = string.Empty;

        await using (var stream = File.Create(_settingsFilePath))
        {
            await JsonSerializer.SerializeAsync(stream, clone, _serializerOptions, cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(secretId))
        {
            await _secretStore.RemoveSecretAsync(ProviderSecretIdSecretName, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _secretStore.SaveSecretAsync(ProviderSecretIdSecretName, secretId.Trim(), cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            await _secretStore.RemoveSecretAsync(ProviderSecretKeySecretName, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _secretStore.SaveSecretAsync(ProviderSecretKeySecretName, secretKey.Trim(), cancellationToken).ConfigureAwait(false);
        }

        await _secretStore.RemoveSecretAsync(LegacyProviderApiKeySecretName, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteDefaultsAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);

        await using var stream = File.Create(_settingsFilePath);
        await JsonSerializer.SerializeAsync(stream, settings, _serializerOptions, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
