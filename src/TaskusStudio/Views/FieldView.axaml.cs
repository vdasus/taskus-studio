using System.ComponentModel;
using Avalonia.Controls;
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
    }

    // A click on a problem asks the field to show itself.
    private void OnFieldChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FieldViewModel.IsFocusRequested) || field is not { IsFocusRequested: true })
            return;
        field.IsFocusRequested = false;
        this.BringIntoView();
        Control target = field.IsMultiline ? MultilineInput : field.IsEnum ? EnumInput : field.IsObject ? ObjectInput : TextBoxInput;
        target.Focus();
    }
}
