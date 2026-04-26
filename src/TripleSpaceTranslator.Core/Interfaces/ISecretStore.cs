namespace TripleSpaceTranslator.Core.Interfaces;

public interface ISecretStore
{
    Task SaveSecretAsync(string key, string secret, CancellationToken cancellationToken = default);

    Task<string?> LoadSecretAsync(string key, CancellationToken cancellationToken = default);

    Task RemoveSecretAsync(string key, CancellationToken cancellationToken = default);
}
