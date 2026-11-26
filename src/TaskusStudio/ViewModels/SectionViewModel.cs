using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TaskusStudio.Model;
using YamlDotNet.RepresentationModel;

namespace TaskusStudio.ViewModels;

/// <summary>A group of settings: a nested record (report, retry), or source / sink with a type to choose.</summary>
public sealed partial class SectionViewModel : ObservableObject
{
    private readonly Func<string, Type>? typeOf;
    private readonly Action changed;

    /// <summary>A section with a fixed record type.</summary>
    public SectionViewModel(Type type, Action changed) : this(Schema.Fields(type), changed) => Description = Schema.Describe(type);

    /// <summary>The top-level settings.</summary>
    public SectionViewModel(IReadOnlyList<FieldSpec> fields, Action changed)
    {
        this.changed = changed;
        Types = [];
        Fields = new ObservableCollection<FieldViewModel>(fields.Select(f => new FieldViewModel(f, changed)));
    }

    /// <summary>source or sink: the fields follow the chosen type.</summary>
    public SectionViewModel(IReadOnlyList<string> types, Func<string, Type> typeOf, string type, Action changed)
    {
        this.typeOf = typeOf;
        this.changed = changed;
        Types = types;
        Fields = [];
        Type = type;
    }

    public IReadOnlyList<string> Types { get; }

    public ObservableCollection<FieldViewModel> Fields { get; }

    [ObservableProperty] private string description = "";
    [ObservableProperty] private string type = "";

    public IEnumerable<FieldViewModel> AllFields => Fields.SelectMany(f => f.Nested is null ? [f] : f.Nested.AllFields.Prepend(f));

    partial void OnTypeChanged(string value)
    {
        if (typeOf is null)
            return;

        // Settings that both types have (folder, connection, url...) keep their values.
        var old = Fields.ToDictionary(f => f.Name, f => f);
        var recordType = typeOf(value);
        Description = Schema.Describe(recordType);
        Fields.Clear();
        foreach (var spec in Schema.Fields(recordType))
        {
            var field = new FieldViewModel(spec, changed);
            if (old.TryGetValue(spec.Name, out var previous) && previous.Spec.Kind == spec.Kind && spec.Kind is not (FieldKind.Object or FieldKind.Dictionary))
            {
                field.Text = previous.Text;
                field.BoolValue = previous.BoolValue;
            }

            Fields.Add(field);
        }

        LinkFolderDefaults();
        changed();
    }

    private bool loadingFile;

    // Excel source: processed and failed follow folder (folder\done, folder\failed) while empty or still derived.
    private void LinkFolderDefaults()
    {
        if (Field("folder") is not { } folder)
            return;
        var linked = new[] { (Field: Field("processed"), Sub: "done"), (Field: Field("failed"), Sub: "failed") }.Where(l => l.Field is not null).ToList();
        if (linked.Count == 0)
            return;
        var previous = folder.Text;
        folder.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(FieldViewModel.Text))
                return;
            var old = previous;
            previous = folder.Text;
            if (loadingFile)
                return;
            foreach (var (field, sub) in linked)
            {
                if (field!.Text.Length == 0 || field.Text == Derived(old, sub))
                    field.Text = Derived(folder.Text, sub);
            }
        };
    }

    private static string Derived(string folder, string sub) => folder.Trim().Length == 0 ? "" : Path.Join(folder.Trim(), sub);

    public FieldViewModel? Field(string name) => Fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));

    public void Clear()
    {
        loadingFile = true;
        foreach (var field in Fields)
            field.Clear();
        loadingFile = false;
    }

    public YamlMappingNode ToYaml()
    {
        var map = new YamlMappingNode();
        if (typeOf is not null)
            map.Add("type", Type);
        foreach (var field in Fields)
        {
            if (field.ToYaml() is { } value)
                map.Add(field.Name, value);
        }

        return map;
    }

    /// <summary>Fills the section from a task file; returns the paths of keys it does not know.</summary>
    public IEnumerable<string> Load(YamlMappingNode map, string path)
    {
        loadingFile = true;
        try
        {
            return LoadFields(map, path);
        }
        finally
        {
            loadingFile = false;
        }
    }

    private List<string> LoadFields(YamlMappingNode map, string path)
    {
        var unknown = new List<string>();
        if (typeOf is not null)
        {
            var type = map.Children.FirstOrDefault(kv => TaskYaml.Text(kv.Key) == "type").Value;
            var name = type is null ? "" : TaskYaml.Text(type).ToLowerInvariant();
            if (Types.Contains(name))
                Type = name;
            else
                unknown.Add($"{path}.type: {name}");
        }

        foreach (var (keyNode, value) in map.Children)
        {
            var key = TaskYaml.Text(keyNode);
            if (typeOf is not null && key == "type")
                continue;
            if (Field(key) is { } field)
                unknown.AddRange(field.Load(value, $"{path}.{key}"));
            else
                unknown.Add(path.Length == 0 ? key : $"{path}.{key}");
        }

        return unknown;
    }
}
