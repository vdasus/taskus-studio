using System.Collections.ObjectModel;
using System.IO.Abstractions;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Taskus.Config;
using TaskusStudio.Model;
using YamlDotNet.RepresentationModel;

namespace TaskusStudio.ViewModels;

/// <summary>One task file in one window: source, map and sink, the run settings, validation, preview and CLI output.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IFileSystem fs;
    private readonly IDialogs dialogs;
    private bool loading;
    private (string Type, string Folder, string Pattern, string Sheet) previewKey;
    private JsonObject? secrets;
    private CancellationTokenSource? running;

    public MainViewModel(AppSettings settings, IDialogs dialogs, IFileSystem? fs = null)
    {
        Settings = settings;
        this.dialogs = dialogs;
        this.fs = fs ?? new FileSystem();
        loading = true; // the sections report changes while they are built
        Source = new SectionViewModel(Schema.SourceTypes, Schema.SourceType, "excel", Changed);
        Sink = new SectionViewModel(Schema.SinkTypes, Schema.SinkType, "excel", Changed);
        Run = new SectionViewModel(Schema.RunSettings, Changed);
        MapEntries.CollectionChanged += (_, _) => Changed();
        loading = false;
        LoadSecrets();
        RefreshPreviewSource(force: true);
        Refresh();
    }

    public AppSettings Settings { get; }

    public SectionViewModel Source { get; }

    public SectionViewModel Sink { get; }

    public SectionViewModel Run { get; }

    public ObservableCollection<MapEntryViewModel> MapEntries { get; } = [];

    public ObservableCollection<Problem> Problems { get; } = [];

    public ObservableCollection<OutputLine> Output { get; } = [];

    public ObservableCollection<string> PreviewFiles { get; } = [];

    public ObservableCollection<string> Columns { get; } = [];

    public ObservableCollection<string> RecentFiles { get; } = [];

    public IReadOnlyList<string> SecretNames { get; private set; } = [];

    private IReadOnlyList<Row> previewRows = [];

    [ObservableProperty] private string? filePath;
    [ObservableProperty] private bool isDirty;
    [ObservableProperty] private string yamlText = "";
    [ObservableProperty] private string? fileWarning;
    [ObservableProperty] private string? secretsStatus;
    [ObservableProperty] private string? previewStatus;
    [ObservableProperty] private string? selectedPreviewFile;
    [ObservableProperty] private int rowIndex;
    [ObservableProperty] private bool showYaml = true;
    [ObservableProperty] private bool showRunSettings;
    [ObservableProperty] private bool isRunning;
    [ObservableProperty] private int bottomTab;

    public string Title => $"{(FilePath is null ? "untitled" : Path.GetFileName(FilePath))}{(IsDirty ? " *" : "")} - Taskus Studio for Taskus {TaskusCli.CoreVersion}";

    public string ProblemsHeader => Problems.Count == 0 ? "Problems" : $"Problems ({Problems.Count})";

    public bool IsValid => Problems.Count == 0;

    public string StatusText => $"{(IsValid ? "Valid" : $"{Problems.Count} problem(s)")}  |  settings: {Settings.FilePath}";

    public bool HasPreview => previewRows.Count > 0;

    public bool IsExcelSource => Source.Type == "excel";

    public string RowLabel => previewRows.Count == 0 ? "no rows" : $"row {RowIndex + 1} of {previewRows.Count}";

    public IReadOnlyList<PreviewCell> PreviewRow =>
        previewRows.Count == 0 ? [] : previewRows[RowIndex].Select(kv => new PreviewCell(kv.Key, Preview.Format(kv.Value))).ToList();

    partial void OnFilePathChanged(string? value) => OnPropertyChanged(nameof(Title));

    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(Title));

    partial void OnSelectedPreviewFileChanged(string? value) => LoadPreviewRows();

    partial void OnRowIndexChanged(int value)
    {
        OnPropertyChanged(nameof(RowLabel));
        OnPropertyChanged(nameof(PreviewRow));
        RefreshMapPreview();
    }

    private void Changed()
    {
        if (loading)
            return;
        IsDirty = true;
        Refresh();
    }

    /// <summary>Rebuilds the YAML text, validates it and recomputes the preview.</summary>
    public void Refresh()
    {
        YamlText = TaskYaml.Write(ToYaml());
        Validate();
        OnPropertyChanged(nameof(IsExcelSource));
        RefreshPreviewSource();
        RefreshMapPreview();
        foreach (var field in Source.AllFields.Concat(Sink.AllFields).Concat(Run.AllFields))
            field.SecretNames = SecretNames;
        // Sink templates see the mapped row; without a map, the source row passes through.
        var mapped = MapEntries.Select(e => e.Field.Trim()).Where(f => f.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        IReadOnlyList<string> sinkColumns = mapped.Count > 0 ? mapped : Columns.ToList();
        foreach (var field in Sink.AllFields.Where(f => f.IsTemplate))
            field.TemplateColumns = sinkColumns;
    }

    public YamlMappingNode ToYaml()
    {
        var root = new YamlMappingNode();
        var run = Run.ToYaml();
        if (run.Children.FirstOrDefault(kv => TaskYaml.Text(kv.Key) == "name") is { Key: not null } name)
            root.Add(name.Key, name.Value);
        root.Add("source", Source.ToYaml());
        var map = new YamlMappingNode();
        foreach (var entry in MapEntries.Where(e => e.Field.Trim().Length > 0))
            map.Add(entry.Field.Trim(), TaskYaml.Scalar(entry.Template));
        if (map.Children.Count > 0)
            root.Add("map", map);
        root.Add("sink", Sink.ToYaml());
        foreach (var (key, value) in run.Children.Where(kv => TaskYaml.Text(kv.Key) != "name"))
            root.Add(key, value);
        return root;
    }

    /// <summary>Replaces the document with a task file text; returns false when it cannot be read.</summary>
    public bool LoadText(string text, string? path)
    {
        var root = TaskYaml.Read(text);
        if (root.IsFailure)
        {
            FileWarning = $"Cannot read the file: {root.Error}";
            return false;
        }

        loading = true;
        try
        {
            Source.Clear();
            Sink.Clear();
            Run.Clear();
            MapEntries.Clear();
            Source.Type = "excel";
            Sink.Type = "excel";
            var unknown = new List<string>();
            foreach (var (keyNode, value) in root.Value.Children)
            {
                var key = TaskYaml.Text(keyNode);
                switch (key, value)
                {
                    case ("source", YamlMappingNode map):
                        unknown.AddRange(Source.Load(map, "source"));
                        break;
                    case ("sink", YamlMappingNode map):
                        unknown.AddRange(Sink.Load(map, "sink"));
                        break;
                    case ("map", YamlMappingNode map):
                        foreach (var (field, template) in map.Children)
                            MapEntries.Add(new MapEntryViewModel(Changed, TaskYaml.Text(field), TaskYaml.Text(template)));
                        break;
                    default:
                        if (Run.Field(key) is { } setting)
                            unknown.AddRange(setting.Load(value, key));
                        else
                            unknown.Add(key);
                        break;
                }
            }

            var warnings = new List<string>();
            if (path is not null && TaskYaml.HasComments(text))
                warnings.Add("This file has comments; saving it from Studio removes them.");
            if (unknown.Count > 0)
                warnings.Add($"Not loaded, unknown to this version: {string.Join(", ", unknown)}. Saving drops them.");
            FileWarning = warnings.Count == 0 ? null : string.Join(" ", warnings);
            FilePath = path;
        }
        finally
        {
            loading = false;
        }

        IsDirty = false;
        RowIndex = 0;
        Refresh();
        return true;
    }

    private void Validate()
    {
        foreach (var field in Source.AllFields.Concat(Sink.AllFields).Concat(Run.AllFields))
            field.Error = null;
        foreach (var entry in MapEntries)
            entry.Error = null;

        var name = FilePath is null ? "untitled" : Path.GetFileNameWithoutExtension(FilePath);
        Problems.Clear();
        foreach (var problem in Validation.Check(YamlText, name, secrets))
        {
            Problems.Add(problem);
            switch (Target(problem.Path))
            {
                case FieldViewModel field:
                    field.Error = problem.Message;
                    break;
                case MapEntryViewModel entry:
                    entry.Error = problem.Message;
                    break;
            }
        }

        OnPropertyChanged(nameof(ProblemsHeader));
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(StatusText));
    }

    /// <summary>The field or map line a validation path points to.</summary>
    public object? Target(string path)
    {
        var parts = path.Split('.', 2);
        if (parts[0] == "map")
            return parts.Length == 2 ? MapEntries.FirstOrDefault(e => string.Equals(e.Field.Trim(), parts[1], StringComparison.OrdinalIgnoreCase)) : null;

        var (section, rest) = parts[0] switch
        {
            "source" => (Source, parts.ElementAtOrDefault(1)),
            "sink" => (Sink, parts.ElementAtOrDefault(1)),
            _ => (Run, path),
        };
        if (rest is null)
            return null;

        FieldViewModel? field = null;
        foreach (var name in rest.Split('.'))
        {
            var next = field is null ? section.Field(name) : field.Nested?.Field(name);
            if (next is null)
                break;
            field = next;
        }

        return field;
    }

    [RelayCommand]
    private void GoToProblem(Problem problem)
    {
        switch (Target(problem.Path))
        {
            case FieldViewModel field:
                if (Run.AllFields.Contains(field))
                    ShowRunSettings = true;
                field.IsFocusRequested = true;
                break;
            case MapEntryViewModel entry:
                entry.IsFocusRequested = true;
                break;
        }
    }

    // ---- map ----

    [RelayCommand]
    private void AddMapEntry() => MapEntries.Add(new MapEntryViewModel(Changed) { IsFocusRequested = true });

    [RelayCommand]
    private void RemoveMapEntry(MapEntryViewModel entry) => MapEntries.Remove(entry);

    /// <summary>Adds a map line that copies a source column; the field name is the column name.</summary>
    [RelayCommand]
    public void AddColumn(string column)
    {
        var field = column.Trim();
        var unique = field;
        for (var i = 2; MapEntries.Any(e => string.Equals(e.Field, unique, StringComparison.OrdinalIgnoreCase)); i++)
            unique = $"{field}{i}";
        MapEntries.Add(new MapEntryViewModel(Changed, unique, Preview.ColumnTemplate(column)));
    }

    [RelayCommand]
    private void AddAllColumns()
    {
        foreach (var column in Columns.ToList())
            AddColumn(column);
    }

    // ---- preview ----

    [RelayCommand]
    private void NextRow()
    {
        if (RowIndex < previewRows.Count - 1)
            RowIndex++;
    }

    [RelayCommand]
    private void PreviousRow()
    {
        if (RowIndex > 0)
            RowIndex--;
    }

    [RelayCommand]
    private async Task BrowsePreviewFileAsync()
    {
        if (await dialogs.OpenExcelFileAsync() is { } file)
        {
            if (!PreviewFiles.Contains(file))
                PreviewFiles.Add(file);
            SelectedPreviewFile = file;
        }
    }

    private string SourceText(string name) => Source.Field(name)?.Text.Trim() ?? "";

    private void RefreshPreviewSource(bool force = false)
    {
        var key = (Source.Type, SourceText("folder"), SourceText("pattern"), SourceText("sheet"));
        if (!force && key == previewKey)
            return;
        var sheetOnly = key.Item1 == previewKey.Type && key.Item2 == previewKey.Folder && key.Item3 == previewKey.Pattern;
        previewKey = key;
        if (sheetOnly && !force)
        {
            LoadPreviewRows();
            return;
        }

        var browsed = PreviewFiles.Where(f => !fs.Path.GetDirectoryName(f)!.Equals(key.Item2, StringComparison.OrdinalIgnoreCase)).ToList();
        PreviewFiles.Clear();
        if (key.Type == "excel")
        {
            foreach (var file in Preview.Files(fs, key.Item2, key.Item3).Concat(browsed.Where(fs.File.Exists)).Distinct())
                PreviewFiles.Add(file);
        }

        var selected = SelectedPreviewFile is { } current && PreviewFiles.Contains(current) ? current : PreviewFiles.FirstOrDefault();
        if (selected == SelectedPreviewFile)
            LoadPreviewRows();
        else
            SelectedPreviewFile = selected;
    }

    private void LoadPreviewRows()
    {
        previewRows = [];
        PreviewStatus = null;
        if (Source.Type != "excel")
            PreviewStatus = "Data preview works for the excel source; type the column names into the map.";
        else if (SelectedPreviewFile is null)
            PreviewStatus = SourceText("folder").Length == 0
                ? "Set source.folder or browse for a sample .xlsx file."
                : $"No file matching the pattern in {SourceText("folder")}. Browse for a sample .xlsx file.";
        else
        {
            var rows = Preview.Rows(fs, SelectedPreviewFile, SourceText("sheet"));
            if (rows.IsSuccess)
                previewRows = rows.Value;
            else
                PreviewStatus = rows.Error;
            if (rows.IsSuccess && previewRows.Count == 0)
                PreviewStatus = "The sheet has no data rows.";
        }

        Columns.Clear();
        foreach (var column in Preview.Columns(previewRows))
            Columns.Add(column);
        if (RowIndex >= previewRows.Count)
            RowIndex = Math.Max(0, previewRows.Count - 1);
        foreach (var field in Sink.AllFields.Where(f => f.IsTemplate && MapEntries.All(e => e.Field.Trim().Length == 0)))
            field.TemplateColumns = Columns.ToList();
        OnPropertyChanged(nameof(HasPreview));
        OnPropertyChanged(nameof(RowLabel));
        OnPropertyChanged(nameof(PreviewRow));
        RefreshMapPreview();
    }

    private void RefreshMapPreview()
    {
        var row = previewRows.Count == 0 ? null : previewRows[Math.Min(RowIndex, previewRows.Count - 1)];
        foreach (var entry in MapEntries)
        {
            if (row is null || entry.Error is not null) // a template error is shown once, under the line
            {
                entry.Preview = "";
                entry.PreviewFailed = false;
                continue;
            }

            var value = Preview.Evaluate(entry.Field.Trim(), entry.Template, row);
            entry.PreviewFailed = value.IsFailure;
            entry.Preview = value.IsSuccess ? Preview.Format(value.Value) : value.Error;
        }
    }

    // ---- secrets ----

    public string SecretsFile =>
        Settings.SecretsPath.Length > 0 ? Settings.SecretsPath
        : Settings.TaskusPath.Length > 0 ? Path.Combine(Path.GetDirectoryName(Settings.TaskusPath) ?? "", "secrets.yaml")
        : "";

    public void LoadSecrets()
    {
        secrets = null;
        SecretNames = [];
        var file = SecretsFile;
        if (file.Length == 0)
            SecretsStatus = "No secrets file set: secret references are not checked.";
        else if (!fs.File.Exists(file) && !fs.File.Exists(Secrets.ProtectedFile(file)))
            SecretsStatus = $"{file} not found: secret references are not checked.";
        else
        {
            var loaded = Secrets.Load(fs, file, OperatingSystem.IsWindows() ? SecretsProtection.Unprotect : null);
            if (loaded.IsSuccess)
            {
                secrets = loaded.Value;
                SecretNames = secrets.Select(kv => kv.Key).Order(StringComparer.OrdinalIgnoreCase).ToList();
                SecretsStatus = $"{SecretNames.Count} secrets in {file}";
            }
            else
            {
                SecretsStatus = loaded.Error;
            }
        }

        OnPropertyChanged(nameof(SecretNames));
    }

    // ---- files ----

    /// <summary>Asks to save unsaved changes; false when the user cancels.</summary>
    public async Task<bool> ConfirmDiscardAsync()
    {
        if (!IsDirty)
            return true;
        return await dialogs.AskSaveAsync(FilePath is null ? "untitled" : Path.GetFileName(FilePath)) switch
        {
            SaveChoice.Save => await SaveAsync(),
            SaveChoice.Discard => true,
            _ => false,
        };
    }

    [RelayCommand]
    private async Task NewAsync()
    {
        if (!await ConfirmDiscardAsync())
            return;
        var name = await dialogs.ChooseTemplateAsync(TemplateInfo.All);
        if (name is null)
            return;
        var template = TemplateInfo.All.First(t => t.Name == name);
        LoadText(template.Text, null);
        FileWarning = null;
    }

    [RelayCommand]
    private async Task OpenAsync()
    {
        if (!await ConfirmDiscardAsync())
            return;
        if (await dialogs.OpenTaskFileAsync() is { } file)
            OpenFile(file);
    }

    [RelayCommand]
    private async Task OpenRecentAsync(string file)
    {
        if (await ConfirmDiscardAsync())
            OpenFile(file);
    }

    public void OpenFile(string file)
    {
        string text;
        try
        {
            text = fs.File.ReadAllText(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            FileWarning = $"Cannot open {file}: {ex.Message}";
            return;
        }

        if (LoadText(text, file))
            Remember(file);
    }

    [RelayCommand]
    public async Task<bool> SaveAsync() => FilePath is null ? await SaveAsAsync() : await SaveToAsync(FilePath);

    [RelayCommand]
    public async Task<bool> SaveAsAsync()
    {
        var suggested = FilePath is null ? $"{(Run.Field("name")?.Text is { Length: > 0 } n ? n : "task")}.yaml" : Path.GetFileName(FilePath);
        return await dialogs.SaveTaskFileAsync(suggested) is { } file && await SaveToAsync(file);
    }

    private async Task<bool> SaveToAsync(string file)
    {
        if (!IsValid && !await dialogs.ConfirmAsync("Save an invalid task?", $"The task has {Problems.Count} problem(s); taskus will refuse to run it.\n\n{string.Join("\n", Problems.Take(5))}"))
            return false;
        try
        {
            fs.File.WriteAllText(file, YamlText);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            FileWarning = $"Cannot save {file}: {ex.Message}";
            return false;
        }

        // Self-check: what was written reads back to the same task.
        var check = TaskYaml.Read(fs.File.ReadAllText(file));
        FileWarning = check.IsFailure ? $"The saved file does not read back: {check.Error}" : null;
        FilePath = file;
        IsDirty = false;
        Remember(file);
        Validate();
        return true;
    }

    private void Remember(string file)
    {
        Settings.AddRecent(file);
        TrySaveSettings();
        RefreshRecent();
    }

    public void RefreshRecent()
    {
        RecentFiles.Clear();
        foreach (var file in Settings.RecentFiles)
            RecentFiles.Add(file);
    }

    private void TrySaveSettings()
    {
        try
        {
            Settings.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SecretsStatus = $"Settings not saved: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task EditSettingsAsync()
    {
        var edit = new SettingsViewModel(Settings);
        await dialogs.ShowSettingsAsync(edit);
        if (!edit.Accepted)
            return;
        try
        {
            edit.Apply();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SecretsStatus = $"Settings not saved: {ex.Message}";
        }

        LoadSecrets();
        Refresh();
        OnPropertyChanged(nameof(StatusText));
        SettingsChanged?.Invoke();
    }

    public event Action? SettingsChanged;

    // ---- taskus CLI ----

    [RelayCommand]
    private Task ValidateWithTaskusAsync() => RunTaskusAsync(dryRun: false);

    [RelayCommand]
    private Task DryRunAsync() => RunTaskusAsync(dryRun: true);

    [RelayCommand]
    private void Stop() => running?.Cancel();

    private async Task RunTaskusAsync(bool dryRun)
    {
        BottomTab = 1;
        Output.Clear();
        if (Settings.TaskusPath.Length == 0 || !fs.File.Exists(Settings.TaskusPath))
        {
            Output.Add(new OutputLine("Set the path to the taskus executable in Settings.", true, false));
            return;
        }

        if ((IsDirty || FilePath is null) && !await SaveAsync())
            return;

        var method = Source.Field("method")?.Text;
        if (dryRun && TaskusCli.SourceMayChangeData(Source.Type, string.IsNullOrWhiteSpace(method) && Source.Type == "http" ? "GET" : method, Source.Field("query")?.Text)
            && !await dialogs.ConfirmAsync("Source makes a real call", "A dry run writes nothing to the sink, but it reads the source for real: this source call can change data. Run it?"))
            return;

        IsRunning = true;
        running = new CancellationTokenSource();
        try
        {
            if (TaskusCli.VersionMismatch(await TaskusCli.VersionAsync(Settings.TaskusPath, running.Token)) is { } mismatch)
                Output.Add(new OutputLine($"Warning: {mismatch}", false, true));

            var args = new List<string> { dryRun ? "run" : "validate", FilePath! };
            if (dryRun)
                args.Add("--dry-run");
            if (SecretsFile.Length > 0)
                args.Add($"--secrets={SecretsFile}");
            Output.Add(new OutputLine($"> {Settings.TaskusPath} {string.Join(' ', args)}", false, false));
            var code = await TaskusCli.RunAsync(Settings.TaskusPath, args, line => Avalonia.Threading.Dispatcher.UIThread.Post(() => Output.Add(line)), running.Token);
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Output.Add(new OutputLine($"Exit code {code}", code != 0, false)));
        }
        catch (OperationCanceledException)
        {
            Output.Add(new OutputLine("Stopped.", false, true));
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Output.Add(new OutputLine($"Cannot start {Settings.TaskusPath}: {ex.Message}", true, false));
        }
        finally
        {
            IsRunning = false;
            running.Dispose();
            running = null;
        }
    }
}
