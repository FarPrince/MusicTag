using System.Windows;
using Wpf.Ui.Controls;

namespace MusicTag.App.Views.Dialogs;

/// <summary>
/// Generic "something failed" dialog shared by every unguarded-operation error path that
/// isn't specific enough to warrant its own dialog class (folder-load failure, Explorer
/// integration register/unregister failure, settings-save failure) — see IDialogService.ShowError.
/// RenameErrorDialog stays separate since it's tied to the specific TryExecute/TryUndo/TryRedo
/// rename flow rather than a plain "an exception happened" report.
/// </summary>
public partial class ErrorDialog : FluentWindow
{
    public ErrorDialog(string title, string message)
    {
        InitializeComponent();

        Title = title; // base Window.Title — also the taskbar/Alt-Tab text
        Message = message;
        DataContext = this;
    }

    public string Message { get; }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
