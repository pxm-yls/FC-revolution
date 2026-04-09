using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.ViewModels;
using FC_Revolution.UI.Views;

namespace FC_Revolution.UI;

public partial class App : Application
{
    public override void Initialize()
    {
        StartupDiagnostics.Write("app", "Initialize begin");
        AvaloniaXamlLoader.Load(this);
        StartupDiagnostics.Write("app", "Initialize complete");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        StartupDiagnostics.Write("app", "OnFrameworkInitializationCompleted begin");
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            StartupDiagnostics.Write("app", "disabling Avalonia data annotation validation");
            DisableAvaloniaDataAnnotationValidation();
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            StartupDiagnostics.Write("app", "creating MainWindow shell");
            var mainWindow = new MainWindow();
            StartupDiagnostics.Write("app", "MainWindow shell created");
            StartupDiagnostics.Write("app", "creating MainWindowViewModel with deferred startup work");
            mainWindow.DataContext = new MainWindowViewModel(deferStartupWork: true);
            StartupDiagnostics.Write("app", "MainWindowViewModel assigned");
            desktop.MainWindow = mainWindow;
            StartupDiagnostics.Write("app", "desktop.MainWindow assigned");
        }

        base.OnFrameworkInitializationCompleted();
        StartupDiagnostics.Write("app", "OnFrameworkInitializationCompleted complete");
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
