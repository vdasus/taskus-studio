using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskusStudio.Model;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace TaskusStudio.ViewModels;

/// <summary>A key and value of a dictionary setting (http headers).</summary>
public sealed partial class KeyValueViewModel : ObservableObject
{
    [ObservableProperty] private string key = "";
    [ObservableProperty] private string value = "";
}

/// <summary>One editable setting. Empty text means "not set": the writer leaves it out and Taskus uses its default.</summary>
public sealed partial class FieldViewModel : ObservableObject
{
    public FieldViewModel(FieldSpec spec, Action changed)
    {
        Spec = spec;
        Changed = changed;
        if (spec.Kind == FieldKind.Bool)
            boolValue = spec.DefaultText == "true";
        if (spec.Kind == FieldKind.Object)
            Nested = new SectionViewModel(spec.ObjectType!, changed);
        Entries.CollectionChanged += (_, e) =>
        {
            foreach (KeyValueViewModel item in e.NewItems ?? Array.Empty<KeyValueViewModel>())
                item.PropertyChanged += (_, _) => Changed();
            Changed();
        };
    }

    public FieldSpec Spec { get; }

    private Action Changed { get; }

    public string Name => Spec.Name;

    public string Label => Spec.Required ? Spec.Name + " *" : Spec.Name;

    public string Description => Spec.Description;

    public string Watermark => Spec.DefaultText is { Length: > 0 } d ? $"default: {d}" : Spec.Required ? "required" : "";

    public bool IsTemplate => Spec.IsTemplate;

    public bool IsText => !IsTemplate && Spec.Kind is FieldKind.Text or FieldKind.Integer or FieldKind.Number or FieldKind.List;

    public bool IsMultiline => !IsTemplate && Spec.Kind == FieldKind.MultilineText;

    public bool IsBool => Spec.Kind == FieldKind.Bool;

    public bool IsEnum => Spec.Kind == FieldKind.Enum;

    public bool IsDictionary => Spec.Kind == FieldKind.Dictionary;

    public bool IsObject => Spec.Kind == FieldKind.Object;

    /// <summary>The smtp section may be one ${secret:smtp} mapping instead of its settings.</summary>
    public bool AcceptsSecretReference => IsObject && Spec.Name == "smtp";

    public bool ExpectsSecret => Spec.ExpectsSecret || AcceptsSecretReference;

    public const string DEFAULT_CHOICE = "(default)";

    /// <summary>Choices of an enum field; the first one keeps the default.</summary>
    public IReadOnlyList<string> Options => [DEFAULT_CHOICE, .. Spec.EnumValues];

    public string EnumChoice
    {
        get => Text.Length == 0 ? DEFAULT_CHOICE : Text;
        set
        {
            // The hidden enum box of a non-enum field, and any box that loses its item, writes null: ignore it.
            if (!IsEnum || value is null)
                return;
            Text = value == DEFAULT_CHOICE ? "" : value;
        }
    }

    public SectionViewModel? Nested { get; }

    public ObservableCollection<KeyValueViewModel> Entries { get; } = [];

    /// <summary>Fields of the row a template sees, for completion; set by the document.</summary>
    public IReadOnlyList<string> TemplateColumns { get => templateColumns; set => SetProperty(ref templateColumns, value); }
    private IReadOnlyList<string> templateColumns = [];

    /// <summary>Secret names for the picker, set by the document.</summary>
    public IReadOnlyList<string> SecretNames { get => secretNames; set => SetProperty(ref secretNames, value); }
    private IReadOnlyList<string> secretNames = [];

    [ObservableProperty] private string text = "";
    [ObservableProperty] private bool boolValue;
    [ObservableProperty] private bool isEnabled; // object fields: the section is written
    [ObservableProperty] private string? error;
    [ObservableProperty] private bool isFocusRequested;

    /// <summary>A raw value in a field that should hold a secret reference.</summary>
    public string? Warning => Spec.ExpectsSecret && Text.Length > 0 && !Text.Contains("${secret:", StringComparison.Ordinal)
        ? "Keep this value in the secrets file and write ${secret:name} here."
        : null;

    /// <summary>The secret named by the whole value, shown in the picker; choosing one writes ${secret:name}.</summary>
    public string? SelectedSecret
    {
        get => SecretReference().Match(Text) is { Success: true } m ? m.Groups[1].Value : null;
        set
        {
            if (value is null || value == SelectedSecret)
                return;
            if (IsObject)
                IsEnabled = false; // the secret replaces the whole section
            Text = $"${{secret:{value}}}";
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^\$\{secret:([^}]+)\}$")]
    private static partial System.Text.RegularExpressions.Regex SecretReference();

    partial void OnTextChanged(string value)
    {
        OnPropertyChanged(nameof(Warning));
        OnPropertyChanged(nameof(EnumChoice));
        OnPropertyChanged(nameof(SelectedSecret));
        Changed();
    }

    partial void OnBoolValueChanged(bool value) => Changed();

    partial void OnIsEnabledChanged(bool value)
    {
        if (value && IsObject)
            Text = ""; // own settings instead of a secret reference
        Changed();
    }

    [RelayCommand]
    private void AddEntry() => Entries.Add(new KeyValueViewModel());

    [RelayCommand]
    private void RemoveEntry(KeyValueViewModel entry) => Entries.Remove(entry);

    public void Clear()
    {
        Text = "";
        BoolValue = Spec.DefaultText == "true";
        Entries.Clear();
        IsEnabled = false;
        Nested?.Clear();
    }

    /// <summary>The YAML value, or null when the field is not set.</summary>
    public YamlNode? ToYaml()
    {
        switch (Spec.Kind)
        {
            case FieldKind.Bool:
                return BoolValue == (Spec.DefaultText == "true") ? null : new YamlScalarNode(BoolValue ? "true" : "false");
            case FieldKind.Dictionary:
                var map = new YamlMappingNode();
                foreach (var entry in Entries.Where(e => e.Key.Trim().Length > 0))
                    map.Add(entry.Key.Trim(), TaskYaml.Scalar(entry.Value));
                return map.Children.Count == 0 ? null : map;
            case FieldKind.List:
                var items = Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return items.Length == 0 ? null : new YamlSequenceNode(items.Select(i => (YamlNode)TaskYaml.Scalar(i))) { Style = SequenceStyle.Flow };
            case FieldKind.Object:
                if (Text.Trim().Length > 0)
                    return TaskYaml.Scalar(Text.Trim()); // ${secret:smtp}
                return IsEnabled ? Nested!.ToYaml() : null;
            default:
                if (Text.Trim().Length == 0)
                    return null;
                // A multi-line value keeps its final line break, as "|" blocks read it back.
                return TaskYaml.Scalar(Text.Contains('\n') ? Text.TrimEnd() + "\n" : Text.Trim());
        }
    }

    /// <summary>Fills the field from a task file; returns the keys it did not know.</summary>
    public IEnumerable<string> Load(YamlNode node, string path)
    {
        switch (Spec.Kind, node)
        {
            case (FieldKind.Dictionary, YamlMappingNode map):
                foreach (var (key, value) in map.Children)
                    Entries.Add(new KeyValueViewModel { Key = TaskYaml.Text(key), Value = TaskYaml.Text(value) });
                return [];
            case (FieldKind.List, YamlSequenceNode list):
                Text = string.Join(", ", list.Children.Select(TaskYaml.Text));
                return [];
            case (FieldKind.Object, YamlMappingNode map):
                IsEnabled = true;
                return Nested!.Load(map, path);
            case (FieldKind.Bool, YamlScalarNode scalar):
                BoolValue = bool.TryParse(scalar.Value, out var b) && b;
                return [];
            case (_, YamlScalarNode scalar):
                Text = scalar.Value ?? "";
                return [];
            default:
                Text = node.ToString();
                return [];
        }
    }
}
