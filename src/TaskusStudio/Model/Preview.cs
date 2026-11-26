using System.Globalization;
using System.IO.Abstractions;
using Taskus.Mapping;
using Taskus.Transports;

namespace TaskusStudio.Model;

public sealed record PreviewCell(string Column, string Value);

/// <summary>Sample rows of an excel source and the value each map template gives for them, as Taskus computes it.</summary>
public static class Preview
{
    public const int MAX_ROWS = 200;

    public static IReadOnlyList<string> Files(IFileSystem fs, string folder, string pattern)
    {
        if (folder.Length == 0 || !fs.Directory.Exists(folder))
            return [];
        try
        {
            return fs.Directory.EnumerateFiles(folder, pattern.Length == 0 ? "*.xlsx" : pattern)
                .Where(f => !fs.Path.GetFileName(f).StartsWith("~$", StringComparison.Ordinal))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return [];
        }
    }

    public static Result<IReadOnlyList<Row>> Rows(IFileSystem fs, string file, string? sheet)
    {
        try
        {
            return ExcelSource.ReadFile(fs, file, string.IsNullOrWhiteSpace(sheet) ? null : sheet).Take(MAX_ROWS).Select(r => r.Values).ToList();
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<Row>>($"{fs.Path.GetFileName(file)}: {ex.Message}");
        }
    }

    /// <summary>Column names in sheet order, from the first rows.</summary>
    public static IReadOnlyList<string> Columns(IReadOnlyList<Row> rows) =>
        rows.Take(20).SelectMany(r => r.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>The value of one template for one row, or its error.</summary>
    public static Result<object?> Evaluate(string field, string template, Row row) =>
        RowMapper.Create([KeyValuePair.Create(field.Length == 0 ? "field" : field, template)])
            .Bind(mapper => mapper.Map(row))
            .Map(mapped => mapped.Values.Single());

    public static string Format(object? value) => value switch
    {
        null => "null",
        string s => $"\"{s}\"",
        DateTime d => d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "  (date)",
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture) + $"  ({value.GetType().Name.ToLowerInvariant()})",
        _ => value.ToString() ?? "",
    };

    /// <summary>A template that inserts one column: row.Name, or row['Name'] for names that are not identifiers.</summary>
    public static string ColumnTemplate(string column) =>
        IsIdentifier(column) ? $"{{{{ row.{column} }}}}" : $"{{{{ row['{column.Replace("'", "\\'")}'] }}}}";

    public static string ColumnAccess(string column) =>
        IsIdentifier(column) ? $"row.{column}" : $"row['{column.Replace("'", "\\'")}']";

    private static bool IsIdentifier(string name) =>
        name.Length > 0 && (char.IsAsciiLetter(name[0]) || name[0] == '_') && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');
}
