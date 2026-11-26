using Avalonia.Controls;
using TaskusStudio.ViewModels;

namespace TaskusStudio.Views;

public sealed partial class TemplateDialog : Window
{
    public TemplateDialog() : this(TemplateInfo.All)
    {
    }

    public TemplateDialog(IReadOnlyList<TemplateInfo> templates)
    {
        InitializeComponent();
        List.ItemsSource = templates;
        List.SelectedIndex = 0;
        CreateButton.Click += (_, _) => Close((List.SelectedItem as TemplateInfo)?.Name);
        CancelButton.Click += (_, _) => Close(null);
        List.DoubleTapped += (_, _) => Close((List.SelectedItem as TemplateInfo)?.Name);
    }
}
