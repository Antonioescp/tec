using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;

namespace Tec.App.Services;

/// <summary>User and shell interaction, kept out of the ViewModel so it can be replaced.</summary>
public interface IUserDialogs
{
    string? PickFolder(string initialDirectory);
    string? PickExecutable();
    void ShowError(string message);
    void OpenInShell(string path);
    void MinimizeMainWindow();
    void RestoreMainWindow();
}

public sealed class WpfUserDialogs : IUserDialogs
{
    public string? PickFolder(string initialDirectory)
    {
        var dialog = new OpenFolderDialog { InitialDirectory = initialDirectory, Title = "Carpeta de evidencias" };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public string? PickExecutable()
    {
        var dialog = new OpenFileDialog { Filter = "Aplicaciones (*.exe)|*.exe", Title = "Aplicación a probar" };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public void ShowError(string message) =>
        MessageBox.Show(message, "TEC", MessageBoxButton.OK, MessageBoxImage.Error);

    public void OpenInShell(string path) =>
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

    public void MinimizeMainWindow()
    {
        if (Application.Current.MainWindow is { } window) window.WindowState = WindowState.Minimized;
    }

    public void RestoreMainWindow()
    {
        if (Application.Current.MainWindow is not { } window) return;
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Activate();
    }
}
