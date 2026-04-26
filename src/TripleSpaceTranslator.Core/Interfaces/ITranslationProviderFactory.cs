using TripleSpaceTranslator.Core.Models;

namespace TripleSpaceTranslator.Core.Interfaces;

public interface ITranslationProviderFactory
{
    ITranslationProvider Create(TranslationProviderConfig providerConfig);
}
