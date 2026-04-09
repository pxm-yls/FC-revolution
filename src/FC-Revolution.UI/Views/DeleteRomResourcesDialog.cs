using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace FC_Revolution.UI.Views;

public enum DeleteRomResourcesChoice
{
    Cancel,
    DeleteRomOnly,
    DeleteRomWithResources
}

public static class DeleteRomResourcesDialog
{
    public static async Task<DeleteRomResourcesChoice> ShowAsync(Window owner, string romName, string resourceSummary)
    {
        var deleteRomOnlyButton = new Button
        {
            Content = "只删除 ROM",
            Background = new SolidColorBrush(Color.Parse("#4B3423")),
            Foreground = Brushes.White,
            Padding = new Thickness(14, 8)
        };

        var deleteAllButton = new Button
        {
            Content = "删除 ROM 和资源",
            Background = new SolidColorBrush(Color.Parse("#7A3B2B")),
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

        var dialog = new Window
        {
            Width = 520,
            Height = 270,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#17100D")),
            Title = "删除游戏",
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
                            Text = "删除游戏",
                            FontSize = 22,
                            Foreground = new SolidColorBrush(Color.Parse("#F5EBDD"))
                        },
                        new TextBlock
                        {
                            Text = $"确定要删除 `{romName}` 吗？\n\n你可以只删除 ROM 本体，或同时删除它的关联资源。\n\n{resourceSummary}",
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = new SolidColorBrush(Color.Parse("#D8C2A9")),
                            FontSize = 13
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 10,
                            Children =
                            {
                                cancelButton,
                                deleteRomOnlyButton,
                                deleteAllButton
                            }
                        }
                    }
                }
            }
        };

        Grid.SetRow(((Grid)((Border)dialog.Content!).Child!).Children[1], 1);
        Grid.SetRow(((Grid)((Border)dialog.Content!).Child!).Children[2], 2);

        cancelButton.Click += (_, _) => dialog.Close(DeleteRomResourcesChoice.Cancel);
        deleteRomOnlyButton.Click += (_, _) => dialog.Close(DeleteRomResourcesChoice.DeleteRomOnly);
        deleteAllButton.Click += (_, _) => dialog.Close(DeleteRomResourcesChoice.DeleteRomWithResources);

        return await dialog.ShowDialog<DeleteRomResourcesChoice>(owner);
    }
}
