using Avalonia.Controls;
using TaskusStudio.Model;
using TaskusStudio.ViewModels;

namespace TaskusStudio.Views;

public sealed partial class SettingsWindow : Window
{
    public SettingsWindow() : this(new SettingsViewModel(new AppSettings()), null)
    {
    }

    public SettingsWindow(SettingsViewModel settings, IDialogs? dialogs)
    {
        InitializeComponent();
        DataContext = settings;
        OkButton.Click += (_, _) =>
        {
            settings.Accepted = true;
            Close();
        };
        CancelButton.Click += (_, _) => Close();
        BrowseTaskus.Click += async (_, _) =>
        {
            if (dialogs is not null && await dialogs.OpenAnyFileAsync("taskus executable") is { } file)
                settings.TaskusPath = file;
        };
        BrowseSecrets.Click += async (_, _) =>
        {
            if (dialogs is not null && await dialogs.OpenAnyFileAsync("Secrets file") is { } file)
                settings.SecretsPath = Path.GetExtension(file).Equals(".sec", StringComparison.OrdinalIgnoreCase) ? Path.ChangeExtension(file, ".yaml") : file;
        };
    }
}
