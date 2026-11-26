using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Taskus.Config;

namespace TaskusStudio.Model;

/// <summary>A validation error. <see cref="Path"/> is the setting it belongs to (source.folder, map.email), empty when unknown.</summary>
public sealed record Problem(string Path, string Message)
{
    public override string ToString() => Path.Length == 0 ? Message : $"{Path}: {Message}";
}

/// <summary>Validates task text with Taskus.Core, the same way taskus validate does.</summary>
public static partial class Validation
{
    /// <param name="text">The task file text.</param>
    /// <param name="name">Task name used when the file has none.</param>
    /// <param name="secrets">The loaded secrets, or null when no secrets file is set: then every reference gets a stand-in value.</param>
    public static IReadOnlyList<Problem> Check(string text, string name, JsonObject? secrets)
    {
        var values = secrets?.DeepClone().AsObject() ?? [];
        if (secrets is null)
        {
            foreach (Match m in Reference().Matches(text))
                values[m.Groups["name"].Value] = StandIn(m.Groups["key"].Value);
        }

        var result = TaskLoader.Parse(text, name, values);
        return result.IsSuccess ? [] : Split(result.Error);
    }

    public static IReadOnlyList<Problem> Split(string error) =>
        // Only split where a setting path follows: Scriban joins its own parse errors with "; " too.
        ErrorSeparator().Split(error).Where(e => e.Length > 0).Select(Parse).ToList();

    private static Problem Parse(string error)
    {
        var m = PathAndMessage().Match(error);
        if (!m.Success)
            return new Problem("", error);

        var path = m.Groups["path"].Value;
        var message = m.Groups["message"].Value;
        if (MissingSetting().Match(message) is { Success: true } missing)
            path = $"{path}.{missing.Groups["name"].Value}";
        if (path == "map" && MapField().Match(message) is { Success: true } field)
            (path, message) = ($"map.{field.Groups["name"].Value}", field.Groups["message"].Value);
        return new Problem(path, message);
    }

    // A whole-value reference under smtp stands for the smtp mapping.
    private static JsonNode StandIn(string key) => key == "smtp"
        ? new JsonObject { ["server"] = "smtp.example.com", ["from"] = "taskus@example.com" }
        : JsonValue.Create("secret")!;

    [GeneratedRegex(@"(?:^|\n)\s*(?<key>[\w-]+):\s*\$\{secret:(?<name>[^}]+)\}\s*$|\$\{secret:(?<name>[^}]+)\}", RegexOptions.Multiline)]
    private static partial Regex Reference();

    [GeneratedRegex(@"^(?<path>(?:source|sink|map|report|smtp|name|onError|maxErrors|logExclude)(?:\.[^:]+)?): (?<message>.+)$", RegexOptions.Singleline)]
    private static partial Regex PathAndMessage();

    [GeneratedRegex(@"; (?=(?:source|sink|map|report|smtp|name|onError|maxErrors|logExclude|secret)[.: ])")]
    private static partial Regex ErrorSeparator();

    [GeneratedRegex(@"^field '(?<name>[^']+)': (?<message>.+)$", RegexOptions.Singleline)]
    private static partial Regex MapField();

    [GeneratedRegex(@"missing required setting '(?<name>\w+)'")]
    private static partial Regex MissingSetting();
}
