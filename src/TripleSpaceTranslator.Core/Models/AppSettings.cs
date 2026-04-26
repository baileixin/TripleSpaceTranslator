namespace TripleSpaceTranslator.Core.Models;

public sealed class AppSettings
{
    public bool RunAtStartup { get; set; }

    public string DefaultTargetLanguage { get; set; } = "en";

    public TranslationProviderConfig ProviderConfig { get; set; } = new();

    public int TripleSpaceWindowMs { get; set; } = 800;

    public TriggerHotkey TriggerHotkey { get; set; } = TriggerHotkey.CreateDefault();

    public bool NotificationsEnabled { get; set; } = true;

    public AppSettings Clone()
    {
        return new AppSettings
        {
            RunAtStartup = RunAtStartup,
            DefaultTargetLanguage = DefaultTargetLanguage,
            ProviderConfig = ProviderConfig.Clone(),
            TripleSpaceWindowMs = TripleSpaceWindowMs,
            TriggerHotkey = TriggerHotkey.Clone(),
            NotificationsEnabled = NotificationsEnabled
        };
    }
}
