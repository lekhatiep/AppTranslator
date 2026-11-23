using AppTranslator.Core.Profiles;

namespace AppTranslator.Core.Abstractions;

public interface IProfileStore
{
    IReadOnlyList<GameProfile> LoadAll();

    GameProfile? Get(string id);

    void Save(GameProfile profile);
}
