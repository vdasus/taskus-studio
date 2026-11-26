using System.IO.Abstractions;
using System.Text.Json.Nodes;
using NSubstitute;
using Taskus.Config;
using TaskusStudio.Model;
using TaskusStudio.ViewModels;

namespace TaskusStudio.Tests;

internal static class TestData
{
    public static string Folder { get; } = Path.Combine(AppContext.BaseDirectory, "data");

    public static string ClientsFile => Path.Combine(Folder, "clients.xlsx");

    /// <summary>The stand-in values of the Taskus secrets.yaml.example.</summary>
    public static JsonObject Secrets()
    {
        var file = Path.Combine(Folder, "secrets.yaml.example");
        var parsed = Taskus.Config.Secrets.Parse(File.ReadAllText(file), file);
        parsed.IsSuccess.Should().BeTrue(parsed.IsFailure ? parsed.Error : "");
        return parsed.Value;
    }

    public static MainViewModel NewDocument(IFileSystem? fs = null) =>
        new(new AppSettings(), Substitute.For<IDialogs>(), fs ?? new FileSystem());

    public static string Template(string name) => TemplateInfo.All.Single(t => t.Name == name).Text;
}

/// <summary>A folder under the temp path, deleted on dispose.</summary>
internal sealed class TempFolder : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"taskus-studio-tests-{Guid.NewGuid():N}");

    public TempFolder() => Directory.CreateDirectory(Path);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // best effort
        }
    }
}
