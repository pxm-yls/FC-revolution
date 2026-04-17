using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FCRevolution.Storage;
using FCRevolution.Core.Workbench.ViewModels;
using FCRevolution.Core.Workbench.Views;

namespace FCRevolution.Core.Workbench;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        AppObjectStorage.EnsureDefaults();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new CoreWorkbenchWindow
            {
                DataContext = new CoreWorkbenchViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
