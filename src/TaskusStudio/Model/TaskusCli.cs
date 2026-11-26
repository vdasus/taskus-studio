using System.Diagnostics;
using System.Text.RegularExpressions;

namespace TaskusStudio.Model;

public sealed record OutputLine(string Text, bool IsError, bool IsWarning);

/// <summary>Runs the configured taskus executable for Validate and Dry run.</summary>
public static partial class TaskusCli
{
    /// <summary>The major.minor of Taskus.Core that this build of Studio was made for.</summary>
    public static string CoreVersion { get; } = typeof(Taskus.Config.TaskLoader).Assembly.GetName().Version is { } v ? $"{v.Major}.{v.Minor}" : "?";

    public static async Task<int> RunAsync(string exe, IReadOnlyList<string> args, Action<OutputLine> output, CancellationToken ct)
    {
        var start = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args)
            start.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = start };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) output(Classify(e.Data, false)); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) output(Classify(e.Data, true)); };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        return process.ExitCode;
    }

    /// <summary>The version printed by taskus --version, or null.</summary>
    public static async Task<string?> VersionAsync(string exe, CancellationToken ct)
    {
        var lines = new List<string>();
        try
        {
            var code = await RunAsync(exe, ["--version"], l => lines.Add(l.Text), ct);
            return code == 0 ? lines.FirstOrDefault()?.Trim() : null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>A warning when taskus and this Studio differ in major.minor, else null.</summary>
    public static string? VersionMismatch(string? taskusVersion) =>
        taskusVersion is null
            ? "taskus --version gave no version: the executable is older than 0.8 or not taskus."
            : MajorMinor(taskusVersion) == CoreVersion
                ? null
                : $"taskus is {taskusVersion}, this Studio is made for Taskus {CoreVersion}.x: settings may be rejected or missing.";

    public static string MajorMinor(string version) => string.Join('.', version.Split('.', '-', '+').Take(2));

    /// <summary>
    /// True when the source call can change data: an http source with a method other than GET,
    /// or a SQL query that does not start with SELECT or WITH.
    /// </summary>
    public static bool SourceMayChangeData(string type, string? method, string? query) => type switch
    {
        "http" => !string.IsNullOrWhiteSpace(method) && !method.Trim().Equals("GET", StringComparison.OrdinalIgnoreCase),
        "postgres" or "odbc" => !ReadOnlySql().IsMatch(query ?? ""),
        _ => false,
    };

    // Serilog console lines look like "[09:25:06 WRN] text".
    private static OutputLine Classify(string text, bool stderr) => new(
        text,
        stderr || text.Contains(" ERR]", StringComparison.Ordinal) || text.Contains(" FTL]", StringComparison.Ordinal),
        text.Contains(" WRN]", StringComparison.Ordinal));

    [GeneratedRegex(@"^\s*(--[^\n]*\n\s*)*\(?\s*(select|with)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ReadOnlySql();
}
