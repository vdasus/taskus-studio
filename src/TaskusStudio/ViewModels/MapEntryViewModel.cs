using CommunityToolkit.Mvvm.ComponentModel;

namespace TaskusStudio.ViewModels;

/// <summary>One line of the map: target field name, Scriban template, and the value it gives for the preview row.</summary>
public sealed partial class MapEntryViewModel : ObservableObject
{
    private readonly Action changed;

    public MapEntryViewModel(Action changed, string field = "", string template = "")
    {
        this.changed = changed;
        this.field = field;
        this.template = template;
    }

    [ObservableProperty] private string field;
    [ObservableProperty] private string template;
    [ObservableProperty] private string preview = "";
    [ObservableProperty] private bool previewFailed;
    [ObservableProperty] private string? error;
    [ObservableProperty] private bool isFocusRequested;

    partial void OnFieldChanged(string value) => changed();

    partial void OnTemplateChanged(string value) => changed();
}
