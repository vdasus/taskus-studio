using System.Reflection;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Headless;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Editing;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TaskusStudio;
using TaskusStudio.Model;
using TaskusStudio.ViewModels;
using TaskusStudio.Views;

// Opens the excel-to-api-post template on the sample clients.xlsx and saves one PNG per state to the
// folder given as the first argument (default temp/screenshots, git-ignored). make-gif.py turns them into the README GIF.
var root = FindRepoRoot();
var output = Path.GetFullPath(args.FirstOrDefault() ?? Path.Combine(root, "temp", "screenshots"));
Directory.CreateDirectory(output);

// A fixed folder name, so the path shown in the screenshots does not change between runs.
var data = Path.Combine(Path.GetTempPath(), "taskus-demo");
Directory.CreateDirectory(data);
File.Copy(Path.Combine(root, "tests", "TaskusStudio.Tests", "data", "clients.xlsx"), Path.Combine(data, "clients.xlsx"), true);
var task = Path.Combine(data, "api-post.yaml");
var template = File.ReadAllText(Path.Combine(root, "src", "TaskusStudio", "Templates", "excel-to-api-post.yaml"));
foreach (var (from, to) in new[] { (@"C:\Taskus\in\clients\done", "done"), (@"C:\Taskus\in\clients\failed", "failed"), (@"C:\Taskus\reports", "reports"), (@"C:\Taskus\in\clients", "") })
    template = template.Replace(from, Path.Combine(data, to).TrimEnd(Path.DirectorySeparatorChar));
File.WriteAllText(task, template);

AppBuilder.Configure<App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .WithInterFont()
    .SetupWithoutStarting();
// Headless popups are separate top levels that a window capture misses; the overlay layer puts the completion list in the frame.
// The overlay host has no background of its own, so the list gets the one a popup window would give it.
Popup.ShouldUseOverlayLayerProperty.OverrideDefaultValue<CompletionWindowBase>(true);
Application.Current!.Styles.Add(new Style(x => x.OfType<CompletionList>())
{
    Setters =
    {
        new Setter(TemplatedControl.BackgroundProperty, Brushes.White),
        new Setter(TemplatedControl.BorderBrushProperty, Brushes.Gray),
        new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(1)),
    },
});

var window = new MainWindow(new AppSettings(), task) { Width = 1440, Height = 900 };
var model = (MainViewModel)window.DataContext!;
window.Show();
window.Activate();

Save("01-editor");
model.RowIndex = 1;
Save("02-preview-next-row");
model.SourceCollapsed = true;
Save("03-source-collapsed");
model.AddMapEntryCommand.Execute(null);
var entry = model.MapEntries[^1];
entry.Field = "lastName";
entry.Template = "{{ row";
Type(entry, ".", "Pavardė");
Save("04-complete-columns");
entry.Template = "{{ row['Pavardė'] ";
Type(entry, "|", "string.upcase");
Save("05-complete-functions");
entry.Template = "{{ row['Pavardė'] | string.upcase }}";
Save("06-completed");
model.MapEntries.Remove(entry);
model.SourceCollapsed = false;
var url = model.Sink.Field("url")!;
var urlText = url.Text;
url.Text = "";
Save("07-validation");
url.Text = urlText;
model.ShowRunSettings = true;
Save("08-run-settings");
model.ShowRunSettings = false;
App.ApplyTheme(ThemeChoice.Dark);
Save("09-dark");
Console.WriteLine($"Frames saved to {output}");
return;

// Types into the entry's template editor, as a user would, so the completion window opens.
void Type(MapEntryViewModel target, string text, string select)
{
    Dispatcher.UIThread.RunJobs();
    var editor = window.GetVisualDescendants().OfType<MapEntryView>().First(v => v.DataContext == target)
        .GetVisualDescendants().OfType<TemplateEditor>().First();
    editor.FocusEditor(); // caret to the end
    editor.GetVisualDescendants().OfType<TextArea>().First().Focus(); // headless: focusing the TextEditor does not reach its TextArea
    window.KeyTextInput(text);
    Render();
    // The completion window is private to TemplateEditor and not in the window's visual tree.
    var list = ((CompletionWindow)typeof(TemplateEditor).GetField("completion", BindingFlags.NonPublic | BindingFlags.Instance)!
        .GetValue(editor)!).CompletionList;
    list.SelectedItem = list.CompletionData.First(d => d.Text == select);
}

void Save(string name)
{
    Render();

    var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame rendered.");
    frame.Save(Path.Combine(output, name + ".png"));
}

static void Render()
{
    for (var i = 0; i < 3; i++)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
}

static string FindRepoRoot()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
    {
        if (File.Exists(Path.Combine(dir.FullName, "TaskusStudio.slnx")))
            return dir.FullName;
    }

    throw new DirectoryNotFoundException("TaskusStudio.slnx not found above " + AppContext.BaseDirectory);
}
