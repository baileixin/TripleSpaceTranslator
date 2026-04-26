using System.IO;
using System.Security.Cryptography;
using System.Text;
using TripleSpaceTranslator.Core.Interfaces;

namespace TripleSpaceTranslator.Core.Services;

public sealed class DpapiSecretStore : ISecretStore
{
    private readonly string _baseDirectory;

    public DpapiSecretStore(string baseDirectory)
    {
        _baseDirectory = baseDirectory;
    }

    public async Task SaveSecretAsync(string key, string secret, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_baseDirectory);

        var encrypted = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(secret),
            optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);

        await File.WriteAllBytesAsync(GetSecretPath(key), encrypted, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> LoadSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        var secretPath = GetSecretPath(key);
        if (!File.Exists(secretPath))
        {
            return null;
        }

        var encrypted = await File.ReadAllBytesAsync(secretPath, cancellationToken).ConfigureAwait(false);
        var decrypted = ProtectedData.Unprotect(
            encrypted,
            optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);

        return Encoding.UTF8.GetString(decrypted);
    }

    public Task RemoveSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        var secretPath = GetSecretPath(key);
        if (File.Exists(secretPath))
        {
            File.Delete(secretPath);
        }

        return Task.CompletedTask;
    }

    private string GetSecretPath(string key)
    {
        return Path.Combine(_baseDirectory, $"{key}.bin");
    }
}
