using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MusicTag.App.Views.Dialogs;

/// <summary>
/// Shown before Refresh (F5) or Open Folder proceeds while at least one file in the currently
/// open folder is dirty — proceeding discards pending tag/album-art edits on the AudioFile
/// instances about to be replaced by the rescan and clears EditHistory (see
/// MainWindowViewModel.LoadFolder / ConfirmDiscardIfDirtyAsync).
/// </summary>
public partial class DiscardChangesDialog : Window
{
    public DiscardChangesDialog()
    {
        InitializeComponent();
    }

    public bool Confirmed { get; private set; }

    private void OnDiscardClick(object? sender, RoutedEventArgs e)
    {
        Confirmed = true;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }
}
