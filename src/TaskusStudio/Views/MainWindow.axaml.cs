using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using TaskusStudio.Model;
using TaskusStudio.ViewModels;

namespace TaskusStudio.Views;

public sealed partial class MainWindow : Window, IDialogs
{
    private static readonly FilePickerFileType TaskFiles = new("Task files") { Patterns = ["*.yaml", "*.yml"] };
    private static readonly FilePickerFileType ExcelFiles = new("Excel files") { Patterns = ["*.xlsx"] };
    private readonly MainViewModel model;
    private bool closeConfirmed;

    // For the XAML previewer.
    public MainWindow() : this(new AppSettings(), null)
    {
    }

    public MainWindow(AppSettings settings, string? file)
    {
        InitializeComponent();
        model = new MainViewModel(settings, this);
        model.RefreshRecent();
        model.SettingsChanged += () => App.ApplyTheme(settings.Theme);
        DataContext = model;
        if (file is not null)
            model.OpenFile(Path.GetFullPath(file));

        ProblemList.SelectionChanged += (_, _) =>
        {
            if (ProblemList.SelectedItem is Problem problem)
                model.GoToProblemCommand.Execute(problem);
        };
        model.Output.CollectionChanged += (_, _) =>
            Dispatcher.UIThread.Post(() => OutputList.ScrollIntoView(model.Output.Count - 1), DispatcherPriority.Background);
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (closeConfirmed || !model.IsDirty)
            return;
        e.Cancel = true;
        if (await model.ConfirmDiscardAsync())
        {
            closeConfirmed = true;
            Close();
        }
    }

    public async Task<string?> OpenTaskFileAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open a task file",
            FileTypeFilter = [TaskFiles, FilePickerFileTypes.All],
        });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> SaveTaskFileAsync(string suggestedName)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save the task file",
            SuggestedFileName = suggestedName,
            DefaultExtension = "yaml",
            FileTypeChoices = [TaskFiles],
            ShowOverwritePrompt = true,
        });
        return file?.TryGetLocalPath();
    }

    public async Task<string?> OpenExcelFileAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Sample .xlsx file for the preview",
            FileTypeFilter = [ExcelFiles],
        });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> OpenAnyFileAsync(string title)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = title });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    public Task<bool> ConfirmAsync(string title, string message) =>
        MessageDialog.ShowAsync(this, title, message, ["Continue", "Cancel"]).ContinueWith(t => t.Result == 0, TaskScheduler.FromCurrentSynchronizationContext());

    public async Task<SaveChoice> AskSaveAsync(string fileName) =>
        await MessageDialog.ShowAsync(this, "Unsaved changes", $"Save the changes to {fileName}?", ["Save", "Don't save", "Cancel"]) switch
        {
            0 => SaveChoice.Save,
            1 => SaveChoice.Discard,
            _ => SaveChoice.Cancel,
        };

    public Task<string?> ChooseTemplateAsync(IReadOnlyList<TemplateInfo> templates) => new TemplateDialog(templates).ShowDialog<string?>(this);

    public async Task ShowSettingsAsync(SettingsViewModel settings) => await new SettingsWindow(settings, this).ShowDialog(this);
}
