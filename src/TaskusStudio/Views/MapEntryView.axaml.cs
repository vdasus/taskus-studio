using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using TaskusStudio.ViewModels;

namespace TaskusStudio.Views;

public sealed partial class MapEntryView : UserControl
{
    private MapEntryViewModel? entry;

    public MapEntryView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (entry is not null)
                entry.PropertyChanged -= OnEntryChanged;
            entry = DataContext as MapEntryViewModel;
            if (entry is not null)
            {
                entry.PropertyChanged += OnEntryChanged;
                if (entry.IsFocusRequested)
                    Dispatcher.UIThread.Post(FocusEntry, DispatcherPriority.Loaded);
            }
        };
    }

    private void OnEntryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapEntryViewModel.IsFocusRequested) && entry is { IsFocusRequested: true })
            FocusEntry();
    }

    private void FocusEntry()
    {
        if (entry is null)
            return;
        entry.IsFocusRequested = false;
        this.BringIntoView();
        if (entry.Field.Length == 0)
            FieldInput.Focus();
        else
            TemplateInput.FocusEditor();
    }
}
