using CommunityToolkit.Mvvm.ComponentModel;
using TaskusStudio.Model;

namespace TaskusStudio.ViewModels;

/// <summary>The settings window: edits a copy, applied on OK.</summary>
public sealed partial class SettingsViewModel(AppSettings settings) : ObservableObject
{
    [ObservableProperty] private string taskusPath = settings.TaskusPath;
    [ObservableProperty] private string secretsPath = settings.SecretsPath;
    [ObservableProperty] private ThemeChoice theme = settings.Theme;

    public bool Accepted { get; set; }

    public string FilePath => settings.FilePath;

    public IReadOnlyList<ThemeChoice> Themes { get; } = Enum.GetValues<ThemeChoice>();

    public void Apply()
    {
        settings.TaskusPath = TaskusPath.Trim();
        settings.SecretsPath = SecretsPath.Trim();
        settings.Theme = Theme;
        settings.Save();
    }
}
