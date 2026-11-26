using Taskus.Config;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace TaskusStudio.Model;

/// <summary>YAML helpers: the editor writes its own text, only the settings that are set, in a fixed key order.</summary>
public static class TaskYaml
{
    public static readonly string SchemaHeader = $"# yaml-language-server: $schema={TaskSchema.Url}";

    /// <summary>A scalar; multi-line text (SQL, bodies) as a literal block, the emitter quotes the rest when needed.</summary>
    public static YamlScalarNode Scalar(string text) =>
        new(text) { Style = text.Contains('\n') ? ScalarStyle.Literal : ScalarStyle.Any };

    public static string Text(YamlNode node) => node is YamlScalarNode scalar ? scalar.Value ?? "" : node.ToString();

    public static string Write(YamlMappingNode root, bool header = true)
    {
        var writer = new StringWriter { NewLine = "\n" };
        new YamlStream(new YamlDocument(root)).Save(writer, assignAnchors: false);
        var body = writer.ToString().Replace("\r\n", "\n");
        // YamlStream ends a document with "...": not wanted in a task file.
        if (body.EndsWith("...\n", StringComparison.Ordinal))
            body = body[..^4];
        return header ? $"{SchemaHeader}\n{body}" : body;
    }

    public static Result<YamlMappingNode> Read(string text)
    {
        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(text));
            return stream.Documents.Count == 0
                ? new YamlMappingNode()
                : stream.Documents[0].RootNode is YamlMappingNode map ? map : Result.Failure<YamlMappingNode>("a task file must be a mapping");
        }
        catch (YamlException ex)
        {
            return Result.Failure<YamlMappingNode>($"line {ex.Start.Line}: {ex.Message}");
        }
    }

    /// <summary>Comments other than the schema line; they are lost when the editor saves the file.</summary>
    public static bool HasComments(string text)
    {
        var reader = new Scanner(new StringReader(text), skipComments: false);
        try
        {
            while (reader.MoveNext())
            {
                if (reader.Current is YamlDotNet.Core.Tokens.Comment comment && !comment.Value.TrimStart().StartsWith("yaml-language-server:", StringComparison.Ordinal))
                    return true;
            }
        }
        catch (YamlException)
        {
            // A broken file shows its parse error elsewhere.
        }

        return false;
    }
}
