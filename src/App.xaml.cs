using System.Windows;
using System.Windows.Threading;
using CleaN.Core;
using CleaN.ViewModels;
using CleaN.Views;

namespace CleaN;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = AppSettings.Load();
        var themes = new ThemeService();
        themes.Apply(settings.Theme);

        var viewModel = new MainViewModel(new DialogService(), themes, settings);
        var window = new MainWindow { DataContext = viewModel };

        DispatcherUnhandledException += OnUnhandledException;
        MainWindow = window;
        window.Show();
    }

    /// <summary>
    /// A cleaner that crashes mid-run is alarming. Anything that escapes a view model is
    /// reported and swallowed so the window stays usable.
    /// </summary>
    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"cleaN hit an unexpected error and stopped what it was doing.\n\n{e.Exception.Message}",
            "cleaN", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }
}
