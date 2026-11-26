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
    private static readonly int[] PanelColumns = [1, 3, 5];
    private readonly AppSettings settings;
    private readonly MainViewModel model;
    private readonly Dictionary<int, GridLength> widths = [];
    private bool closeConfirmed;

    // For the XAML previewer.
    public MainWindow() : this(new AppSettings(), null)
    {
    }

    public MainWindow(AppSettings settings, string? file)
    {
        InitializeComponent();
        this.settings = settings;
        if (settings.PanelWidths.Count == PanelColumns.Length && settings.PanelWidths.All(w => w > 0))
        {
            for (var i = 0; i < PanelColumns.Length; i++)
                Panels.ColumnDefinitions[PanelColumns[i]].Width = new GridLength(settings.PanelWidths[i], GridUnitType.Star);
        }

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
        model.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(MainViewModel.SourceCollapsed): Collapse(1, model.SourceCollapsed); break;
                case nameof(MainViewModel.MapCollapsed): Collapse(3, model.MapCollapsed); break;
                case nameof(MainViewModel.SinkCollapsed): Collapse(5, model.SinkCollapsed); break;
            }
        };
    }

    // A collapsed panel shrinks to its title strip; expanding restores the width it had, splitter drags included.
    private void Collapse(int column, bool collapsed)
    {
        var definition = Panels.ColumnDefinitions[column];
        if (collapsed)
        {
            widths[column] = new GridLength(definition.ActualWidth, GridUnitType.Star); // pixels as star weights, like the saved layout
            definition.Width = GridLength.Auto;
        }
        else if (widths.Remove(column, out var width))
        {
            definition.Width = width;
        }

        SourceSplitter.IsVisible = !model.SourceCollapsed && !model.MapCollapsed;
        SinkSplitter.IsVisible = !model.MapCollapsed && !model.SinkCollapsed;
    }

    // One layout for all task files: the widths go to the editor settings.
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        settings.PanelWidths = PanelColumns
            .Select(c => widths.TryGetValue(c, out var collapsed) ? collapsed.Value : Panels.ColumnDefinitions[c].ActualWidth)
            .ToList();
        settings.Save();
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
