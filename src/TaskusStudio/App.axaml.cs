using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using TaskusStudio.Model;
using TaskusStudio.Views;

namespace TaskusStudio;

public sealed class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = AppSettings.Load(AppContext.BaseDirectory, AppSettings.UserFolder());
            ApplyTheme(settings.Theme);
            desktop.MainWindow = new MainWindow(settings, desktop.Args?.FirstOrDefault());
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static void ApplyTheme(ThemeChoice theme)
    {
        if (Current is { } app)
        {
            app.RequestedThemeVariant = theme switch
            {
                ThemeChoice.Light => ThemeVariant.Light,
                ThemeChoice.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default,
            };
        }
    }
}
