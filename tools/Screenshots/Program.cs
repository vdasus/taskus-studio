using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
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

var window = new MainWindow(new AppSettings(), task) { Width = 1440, Height = 900 };
var model = (MainViewModel)window.DataContext!;
window.Show();

Save("01-editor");
model.RowIndex = 1;
Save("02-preview-next-row");
var url = model.Sink.Field("url")!;
var urlText = url.Text;
url.Text = "";
Save("03-validation");
url.Text = urlText;
model.ShowRunSettings = true;
Save("04-run-settings");
model.ShowRunSettings = false;
App.ApplyTheme(ThemeChoice.Dark);
Save("05-dark");
Console.WriteLine($"Frames saved to {output}");
return;

void Save(string name)
{
    for (var i = 0; i < 3; i++)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame rendered.");
    frame.Save(Path.Combine(output, name + ".png"));
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
