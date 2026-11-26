using CommunityToolkit.Mvvm.ComponentModel;

namespace TaskusStudio.ViewModels;

/// <summary>One line of the map: target field name, Scriban template, and the value it gives for the preview row.</summary>
public sealed partial class MapEntryViewModel(Action changed, string field = "", string template = "")
    : ObservableObject
{
    [ObservableProperty] private string field = field;
    [ObservableProperty] private string template = template;
    [ObservableProperty] private string preview = "";
    [ObservableProperty] private bool previewFailed;
    [ObservableProperty] private string? error;
    [ObservableProperty] private bool isFocusRequested;

    partial void OnFieldChanged(string value) => changed();

    partial void OnTemplateChanged(string value) => changed();
}
