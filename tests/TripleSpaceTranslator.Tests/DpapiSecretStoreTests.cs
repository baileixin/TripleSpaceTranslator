using TripleSpaceTranslator.Core.Services;

namespace TripleSpaceTranslator.Tests;

public sealed class DpapiSecretStoreTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveLoadAndRemoveSecret_RoundTripsValue()
    {
        var store = new DpapiSecretStore(_tempDirectory);

        await store.SaveSecretAsync("provider", "sk-test-value");
        var loaded = await store.LoadSecretAsync("provider");

        Assert.Equal("sk-test-value", loaded);

        await store.RemoveSecretAsync("provider");
        var removed = await store.LoadSecretAsync("provider");

        Assert.Null(removed);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
