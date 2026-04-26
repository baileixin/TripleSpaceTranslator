namespace TripleSpaceTranslator.Core.Models;

public sealed class TranslationProviderConfig
{
    public TranslationProviderType ProviderType { get; set; } = TranslationProviderType.TencentMachineTranslation;

    public string SecretId { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public string Region { get; set; } = "ap-guangzhou";

    public int ProjectId { get; set; }

    public string Endpoint { get; set; } = "https://tmt.tencentcloudapi.com/";

    public int TimeoutSeconds { get; set; } = 6;

    public TranslationProviderConfig Clone()
    {
        return new TranslationProviderConfig
        {
            ProviderType = ProviderType,
            SecretId = SecretId,
            SecretKey = SecretKey,
            Region = Region,
            ProjectId = ProjectId,
            Endpoint = Endpoint,
            TimeoutSeconds = TimeoutSeconds
        };
    }
}
