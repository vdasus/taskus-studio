using System.Reflection;
using TaskusStudio.Model;

namespace TaskusStudio.ViewModels;

/// <summary>A task to start from: the Taskus examples, embedded in the app.</summary>
public sealed record TemplateInfo(string Name, string Description, string Text)
{
    public const string BLANK = "blank";

    public static IReadOnlyList<TemplateInfo> All { get; } = Load();

    private static List<TemplateInfo> Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var list = new List<TemplateInfo>
        {
            new(BLANK, "An empty task: excel source, no map, excel sink.", $"{TaskYaml.SchemaHeader}\nsource:\n  type: excel\nsink:\n  type: excel\n"),
        };
        foreach (var resource in assembly.GetManifestResourceNames().Where(n => n.StartsWith("Templates/", StringComparison.Ordinal)).Order())
        {
            using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
            var text = reader.ReadToEnd();
            list.Add(new TemplateInfo(Path.GetFileNameWithoutExtension(resource), Describe(text), text));
        }

        return list;
    }

    // The leading comment block of an example says what it does.
    private static string Describe(string text) =>
        string.Join(' ', text.Split('\n')
            .Select(l => l.Trim())
            .SkipWhile(l => l.StartsWith("# yaml-language-server", StringComparison.Ordinal))
            .TakeWhile(l => l.StartsWith('#'))
            .Select(l => l.TrimStart('#').Trim()));
}
