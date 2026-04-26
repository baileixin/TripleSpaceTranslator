using TripleSpaceTranslator.Core.Models;

namespace TripleSpaceTranslator.Core.Interfaces;

public interface ISettingsRepository
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
