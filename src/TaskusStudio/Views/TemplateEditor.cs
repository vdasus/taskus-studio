using System.Xml;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using TaskusStudio.Model;

namespace TaskusStudio.Views;

/// <summary>A one-template editor: Scriban highlighting and completion of source columns after "row." or on Ctrl+Space.</summary>
public sealed class TemplateEditor : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<TemplateEditor, string>(nameof(Text), "", defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<IReadOnlyList<string>> ColumnsProperty =
        AvaloniaProperty.Register<TemplateEditor, IReadOnlyList<string>>(nameof(Columns), []);

    public static readonly StyledProperty<bool> IsMultilineProperty =
        AvaloniaProperty.Register<TemplateEditor, bool>(nameof(IsMultiline));

    private static readonly Lazy<IHighlightingDefinition> Scriban = new(() =>
    {
        using var stream = typeof(TemplateEditor).Assembly.GetManifestResourceStream("Scriban.xshd")!;
        using var reader = XmlReader.Create(stream);
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    });

    private readonly TextEditor editor;
    private CompletionWindow? completion;
    private bool syncing;

    public TemplateEditor()
    {
        editor = new TextEditor
        {
            SyntaxHighlighting = Scriban.Value,
            FontFamily = new FontFamily("Cascadia Mono, Consolas, Menlo, DejaVu Sans Mono, monospace"),
            FontSize = 13,
            ShowLineNumbers = false,
            WordWrap = true,
            MinHeight = 26,
            Padding = new Thickness(6, 4),
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
        editor.Options.EnableHyperlinks = false;
        editor.Options.EnableEmailHyperlinks = false;
        editor.TextChanged += (_, _) =>
        {
            if (syncing)
                return;
            syncing = true;
            SetCurrentValue(TextProperty, editor.Text);
            syncing = false;
        };
        editor.TextArea.TextEntered += OnTextEntered;
        editor.TextArea.KeyDown += OnKeyDown;
        var border = new Border { Child = editor, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4) };
        border.Bind(Border.BorderBrushProperty, border.GetResourceObservable("TextControlBorderBrush"));
        border.Bind(Border.BackgroundProperty, border.GetResourceObservable("TextControlBackground"));
        Content = border;
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public IReadOnlyList<string> Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>A taller editor with a scroll bar, for request bodies.</summary>
    public bool IsMultiline
    {
        get => GetValue(IsMultilineProperty);
        set => SetValue(IsMultilineProperty, value);
    }

    public void FocusEditor()
    {
        editor.Focus();
        editor.TextArea.Caret.Offset = editor.Document.TextLength;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty && !syncing && editor.Text != Text)
        {
            syncing = true;
            editor.Document = new TextDocument(Text ?? "");
            syncing = false;
        }
        else if (change.Property == IsMultilineProperty)
        {
            editor.MinHeight = IsMultiline ? 90 : 26;
            editor.MaxHeight = IsMultiline ? 260 : double.PositiveInfinity;
            editor.ShowLineNumbers = IsMultiline;
            editor.VerticalScrollBarVisibility = IsMultiline
                ? Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
                : Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
        }
    }

    private void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        var offset = editor.CaretOffset;
        var before = editor.Document.GetText(0, offset);
        if (e.Text == "." && before.EndsWith("row.", StringComparison.Ordinal))
            ShowCompletion(offset - 1, column => Preview.ColumnAccess(column)[3..]); // ".Name" or "['Name']" in place of the dot
        else if (e.Text == "[" && before.EndsWith("row[", StringComparison.Ordinal))
            ShowCompletion(offset - 1, column => $"['{column.Replace("'", "\\'")}']");
        else if (e.Text == "|" && InsideCode(before))
            ShowFilters(offset);
    }

    // Common Scriban functions for a pipe: "{{ row.Name | html.url_encode }}".
    internal static readonly IReadOnlyList<(string Name, string Insert, string Help)> Filters =
    [
        ("html.url_encode", "html.url_encode", "Encodes text for a URL"),
        ("html.escape", "html.escape", "Escapes HTML characters"),
        ("string.strip", "string.strip", "Removes leading and trailing spaces"),
        ("string.downcase", "string.downcase", "Lower case"),
        ("string.upcase", "string.upcase", "Upper case"),
        ("string.capitalize", "string.capitalize", "First letter upper case"),
        ("string.replace", "string.replace \"old\" \"new\"", "Replaces text"),
        ("string.truncate", "string.truncate 20", "Cuts to a length"),
        ("string.pad_left", "string.pad_left 10", "Pads on the left to a width"),
        ("date.to_string", "date.to_string '%Y-%m-%d'", "Formats a date"),
        ("date.add_days", "date.add_days 1", "Adds days to a date"),
        ("math.round", "math.round 2", "Rounds a number"),
        ("math.format", "math.format '0.00'", "Formats a number"),
        ("object.default", "object.default ''", "Value to use when empty"),
        ("array.join", "array.join ','", "Joins a list"),
    ];

    private static bool InsideCode(string before) =>
        before.LastIndexOf("{{", StringComparison.Ordinal) > before.LastIndexOf("}}", StringComparison.Ordinal);

    private void ShowFilters(int start)
    {
        completion = new CompletionWindow(editor.TextArea) { StartOffset = start };
        foreach (var (name, insert, help) in Filters)
            completion.CompletionList.CompletionData.Add(new ColumnCompletion(name, " " + insert + " ", help));
        completion.Show();
        completion.Closed += (_, _) => completion = null;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var before = editor.Document.GetText(0, editor.CaretOffset);
            if (before.TrimEnd().EndsWith('|') && InsideCode(before))
                ShowFilters(editor.CaretOffset);
            else if (InsideCode(before))
                ShowCompletion(editor.CaretOffset, Preview.ColumnAccess);
            else
                ShowCompletion(editor.CaretOffset, Preview.ColumnTemplate); // outside {{ }}: a whole {{ row.X }}
            e.Handled = true;
        }
    }

    private void ShowCompletion(int start, Func<string, string> insert)
    {
        if (Columns.Count == 0)
            return;
        completion = new CompletionWindow(editor.TextArea) { StartOffset = start };
        foreach (var column in Columns)
            completion.CompletionList.CompletionData.Add(new ColumnCompletion(column, insert(column), $"Inserts {insert(column)}"));
        completion.Show();
        completion.Closed += (_, _) => completion = null;
    }

    private sealed class ColumnCompletion(string column, string insert, string description) : ICompletionData
    {
        public IImage? Image => null;

        public string Text => column;

        public object Content => column;

        public object Description => description;

        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs) =>
            textArea.Document.Replace(completionSegment, insert);
    }
}
