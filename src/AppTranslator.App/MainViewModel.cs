using AppTranslator.Core.Abstractions;
using AppTranslator.Core.Profiles;
using AppTranslator.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppTranslator.App;

public partial class MainViewModel : ObservableObject
{
    public MainViewModel(IProfileStore profileStore, AppSettings settings)
    {
        Profiles = profileStore.LoadAll();
        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == settings.ActiveProfileId) ?? Profiles.FirstOrDefault();
    }

    public IReadOnlyList<GameProfile> Profiles { get; }

    [ObservableProperty]
    private GameProfile? _selectedProfile;
}
