using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace FC_Revolution.UI.Views;

public static class ProfileTrustDialog
{
    public static async Task<bool> ShowAsync(Window owner, string title, string message)
    {
        var acceptButton = new Button
        {
            Content = "继续并信任",
            Background = new SolidColorBrush(Color.Parse("#6A4C36")),
            Foreground = Brushes.White,
            Padding = new Thickness(14, 8)
        };

        var cancelButton = new Button
        {
            Content = "取消",
            Background = new SolidColorBrush(Color.Parse("#34231A")),
            Foreground = Brushes.White,
            Padding = new Thickness(14, 8)
        };

        var bodyText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.Parse("#D8C2A9")),
            FontSize = 13
        };
        Grid.SetRow(bodyText, 1);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Children =
            {
                cancelButton,
                acceptButton
            }
        };
        Grid.SetRow(actions, 2);

        var dialog = new Window
        {
            Width = 460,
            Height = 240,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#17100D")),
            Title = title,
            Content = new Border
            {
                Padding = new Thickness(18),
                Background = new SolidColorBrush(Color.Parse("#17100D")),
                BorderBrush = new SolidColorBrush(Color.Parse("#6C5037")),
                BorderThickness = new Thickness(1),
                Child = new Grid
                {
                    RowDefinitions = new RowDefinitions("Auto,*,Auto"),
                    RowSpacing = 14,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = title,
                            FontSize = 22,
                            Foreground = new SolidColorBrush(Color.Parse("#F5EBDD"))
                        },
                        bodyText,
                        actions
                    }
                }
            }
        };

        acceptButton.Click += (_, _) => dialog.Close(true);
        cancelButton.Click += (_, _) => dialog.Close(false);

        var result = await dialog.ShowDialog<bool>(owner);
        return result;
    }
}
