using System.Diagnostics;
using System.Windows;
using CleaN.ViewModels;

namespace CleaN.Views;

public sealed class DialogService : IDialogService
{
    public bool Confirm(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
            == MessageBoxResult.Yes;

    public void Alert(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void OpenInExplorer(string path)
    {
        try
        {
            if (!Directory.Exists(path) && !File.Exists(path))
            {
                Alert("Not found", $"'{path}' no longer exists.");
                return;
            }

            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException
                                       or FileNotFoundException)
        {
            Alert("Could not open the folder", ex.Message);
        }
    }
}
