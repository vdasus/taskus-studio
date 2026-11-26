using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TaskusStudio.Views;

/// <summary>A small modal question; returns the index of the pressed button, -1 when closed.</summary>
public static class MessageDialog
{
    public static Task<int> ShowAsync(Window owner, string title, string message, IReadOnlyList<string> buttons)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        for (var i = 0; i < buttons.Count; i++)
        {
            var index = i;
            var button = new Button { Content = buttons[i], MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
            if (i == 0)
                button.Classes.Add("accent");
            button.Click += (_, _) => dialog.Close(index);
            row.Children.Add(button);
        }

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = title, FontSize = 16, FontWeight = Avalonia.Media.FontWeight.SemiBold },
                new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                row,
            },
        };
        return dialog.ShowDialog<int?>(owner).ContinueWith(t => t.Result ?? -1, TaskScheduler.FromCurrentSynchronizationContext());
    }
}
