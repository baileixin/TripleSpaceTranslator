using System.Net.Http;
using TripleSpaceTranslator.Core.Interfaces;
using TripleSpaceTranslator.Core.Models;

namespace TripleSpaceTranslator.Core.Services;

public sealed class TranslationProviderFactory : ITranslationProviderFactory
{
    private readonly HttpClient _httpClient;

    public TranslationProviderFactory(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public ITranslationProvider Create(TranslationProviderConfig providerConfig)
    {
        return providerConfig.ProviderType switch
        {
            TranslationProviderType.TencentMachineTranslation => new TencentMachineTranslationProvider(_httpClient, providerConfig),
            _ => throw new NotSupportedException($"Unsupported provider type: {providerConfig.ProviderType}")
        };
    }
}
