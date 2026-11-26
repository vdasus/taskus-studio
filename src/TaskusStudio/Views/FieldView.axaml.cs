using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using TaskusStudio.ViewModels;

namespace TaskusStudio.Views;

public sealed partial class FieldView : UserControl
{
    private FieldViewModel? field;

    public FieldView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (field is not null)
                field.PropertyChanged -= OnFieldChanged;
            field = DataContext as FieldViewModel;
            if (field is not null)
                field.PropertyChanged += OnFieldChanged;
        };
        BrowseFolder.Click += async (_, _) =>
        {
            if (field is null || TopLevel.GetTopLevel(this) is not { } top)
                return;
            var start = field.Text.Length > 0 && Directory.Exists(field.Text) ? await top.StorageProvider.TryGetFolderFromPathAsync(field.Text) : null;
            var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = field.Name, SuggestedStartLocation = start });
            if (folders.FirstOrDefault()?.TryGetLocalPath() is { } path)
                field.Text = path;
        };
    }

    // A click on a problem asks the field to show itself.
    private void OnFieldChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FieldViewModel.IsFocusRequested) || field is not { IsFocusRequested: true })
            return;
        field.IsFocusRequested = false;
        this.BringIntoView();
        if (field.IsTemplate)
        {
            TemplateInput.FocusEditor();
            return;
        }

        Control target = field.IsMultiline ? MultilineInput : field.IsEnum ? EnumInput : field.IsObject ? ObjectInput : TextBoxInput;
        target.Focus();
    }
}
