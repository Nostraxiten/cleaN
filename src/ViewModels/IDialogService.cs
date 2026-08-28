namespace CleaN.ViewModels;

/// <summary>
/// The only way the view models talk to the user. Keeping it behind an interface is what
/// lets every view model stay free of WPF types.
/// </summary>
public interface IDialogService
{
    bool Confirm(string title, string message);

    void Alert(string title, string message);

    /// <summary>Opens a folder or file with the shell.</summary>
    void OpenInExplorer(string path);
}
