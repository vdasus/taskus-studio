using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Taskus.Config;

namespace TaskusStudio.Model;

public enum FieldKind { Text, MultilineText, Integer, Number, Bool, Enum, Dictionary, List, Object }

/// <summary>One setting of a task file, read from the Taskus.Core model so the editor follows it without a copy.</summary>
public sealed record FieldSpec(
    string Name,
    FieldKind Kind,
    bool Required,
    string Description,
    string? DefaultText,
    IReadOnlyList<string> EnumValues,
    Type? ObjectType)
{
    /// <summary>Connection strings and passwords: the model says "Usually ${secret:name}".</summary>
    public bool ExpectsSecret => Description.Contains("${secret:", StringComparison.Ordinal);

    /// <summary>A Scriban template (urls, bodies, file names): edited with highlighting and completion.</summary>
    public bool IsTemplate => Kind is FieldKind.Text or FieldKind.MultilineText && Description.Contains("template", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<FieldSpec> Children => ObjectType is null ? [] : Schema.Fields(ObjectType);
}

/// <summary>Reads field specs from the Taskus.Core option records.</summary>
public static class Schema
{
    private static readonly Dictionary<Type, IReadOnlyList<FieldSpec>> Cache = [];

    // Multi-line editors for SQL and request bodies.
    private static readonly HashSet<string> Multiline = ["query", "body"];

    public static IReadOnlyList<string> SourceTypes => DerivedTypes(typeof(SourceOptions)).Keys.ToList();

    public static IReadOnlyList<string> SinkTypes => DerivedTypes(typeof(SinkOptions)).Keys.ToList();

    public static Type SourceType(string type) => DerivedTypes(typeof(SourceOptions))[type];

    public static Type SinkType(string type) => DerivedTypes(typeof(SinkOptions))[type];

    public static string Describe(Type type) => type.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";

    /// <summary>The top-level settings other than source, map and sink.</summary>
    public static IReadOnlyList<FieldSpec> RunSettings =>
        Fields(typeof(TaskOptions)).Where(f => f.Name is not ("source" or "map" or "sink")).ToList();

    public static IReadOnlyList<FieldSpec> Fields(Type type)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(type, out var cached))
                return cached;

            var defaults = Activator.CreateInstance(type);
            var fields = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && p.GetCustomAttribute<JsonIgnoreAttribute>() is null && p.DeclaringType != typeof(object))
                .Where(p => p.Name != "EqualityContract")
                .OrderBy(p => p.MetadataToken)
                .Select(p => Spec(p, defaults))
                .ToList();
            Cache[type] = fields;
            return fields;
        }
    }

    public static string YamlName(string member) => JsonNamingPolicy.CamelCase.ConvertName(member);

    public static string EnumText(object value)
    {
        var member = value.GetType().GetField(value.ToString()!)!;
        return member.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name ?? YamlName(value.ToString()!);
    }

    private static FieldSpec Spec(PropertyInfo p, object? defaults)
    {
        var name = YamlName(p.Name);
        var type = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
        var kind = type switch
        {
            _ when type == typeof(string) => Multiline.Contains(name) ? FieldKind.MultilineText : FieldKind.Text,
            _ when type == typeof(int) => FieldKind.Integer,
            _ when type == typeof(double) => FieldKind.Number,
            _ when type == typeof(bool) => FieldKind.Bool,
            { IsEnum: true } => FieldKind.Enum,
            _ when type == typeof(Dictionary<string, string>) => FieldKind.Dictionary,
            _ when type == typeof(List<string>) => FieldKind.List,
            _ => FieldKind.Object,
        };
        var value = defaults is null ? null : p.GetValue(defaults);
        var defaultText = value switch
        {
            null => null,
            bool b => b ? "true" : "false",
            Enum e => EnumText(e),
            double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
            string or int => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
            _ => null, // empty collections and nested records: "not set" is the default
        };
        var enumValues = type.IsEnum ? Enum.GetValues(type).Cast<object>().Select(EnumText).ToList() : [];
        return new FieldSpec(
            name,
            kind,
            p.GetCustomAttribute<RequiredMemberAttribute>() is not null,
            p.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "",
            defaultText,
            enumValues,
            kind == FieldKind.Object ? type : null);
    }

    private static Dictionary<string, Type> DerivedTypes(Type baseType) =>
        baseType.GetCustomAttributes<JsonDerivedTypeAttribute>().ToDictionary(a => (string)a.TypeDiscriminator!, a => a.DerivedType);
}
