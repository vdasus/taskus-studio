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
    }

    private void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        var offset = editor.CaretOffset;
        var before = editor.Document.GetText(0, offset);
        if (e.Text == "." && before.EndsWith("row.", StringComparison.Ordinal))
            ShowCompletion(offset - 1, column => Preview.ColumnAccess(column)[3..]); // ".Name" or "['Name']" in place of the dot
        else if (e.Text == "[" && before.EndsWith("row[", StringComparison.Ordinal))
            ShowCompletion(offset - 1, column => $"['{column.Replace("'", "\\'")}']");
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            ShowCompletion(editor.CaretOffset, Preview.ColumnAccess);
            e.Handled = true;
        }
    }

    private void ShowCompletion(int start, Func<string, string> insert)
    {
        if (Columns.Count == 0)
            return;
        completion = new CompletionWindow(editor.TextArea) { StartOffset = start };
        foreach (var column in Columns)
            completion.CompletionList.CompletionData.Add(new ColumnCompletion(column, insert(column)));
        completion.Show();
        completion.Closed += (_, _) => completion = null;
    }

    private sealed class ColumnCompletion(string column, string insert) : ICompletionData
    {
        public IImage? Image => null;

        public string Text => column;

        public object Content => column;

        public object Description => $"Inserts {insert}";

        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs) =>
            textArea.Document.Replace(completionSegment, insert);
    }
}
