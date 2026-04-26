using TripleSpaceTranslator.Core.Models;

namespace TripleSpaceTranslator.Core.Utilities;

public static class TranslationProviderCatalog
{
    public static IReadOnlyList<TranslationProviderOption> All { get; } =
    [
        new(
            TranslationProviderType.TencentMachineTranslation,
            "腾讯云机器翻译",
            "SecretId",
            "SecretKey",
            "默认只需填写 SecretId 和 SecretKey。Region 默认 ap-guangzhou，ProjectId 默认 0。",
            "会直接使用当前界面里的 SecretId、SecretKey 和目标语言。默认 Region=ap-guangzhou、ProjectId=0；如需修改可展开高级设置。",
            true),
        new(
            TranslationProviderType.BaiduGeneralTextTranslation,
            "百度通用文本翻译",
            "AppId",
            "AppKey",
            "适合非技术用户切换服务，通常只需要填写 AppId 和 AppKey。",
            "会直接使用当前界面里的 AppId、AppKey 和目标语言。",
            false)
    ];

    public static TranslationProviderOption GetByType(TranslationProviderType providerType)
    {
        return All.FirstOrDefault(option => option.ProviderType == providerType) ?? All[0];
    }

    public static string GetMissingCredentialMessage(TranslationProviderType providerType)
    {
        return providerType switch
        {
            TranslationProviderType.BaiduGeneralTextTranslation => "请先在设置里填写百度翻译 AppId 和 AppKey。",
            _ => "请先在设置里填写腾讯云 SecretId 和 SecretKey。"
        };
    }
}
