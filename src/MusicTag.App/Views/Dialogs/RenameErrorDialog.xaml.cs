using System.Windows;
using Wpf.Ui.Controls;

namespace MusicTag.App.Views.Dialogs;

/// <summary>
/// Shown when a live rename fails — the collision case has a clear message via
/// <see cref="MusicTag.Core.Services.RenameTargetExistsException"/>, other failures (locked
/// file, permission denied, invalid characters) show their natural .NET exception message.
/// Shown for both the grid's inline-rename commit (<see cref="MusicTag.Core.History.EditHistory.TryExecute"/>)
/// and a failed Undo/Redo of a rename (<see cref="MusicTag.Core.History.EditHistory.TryUndo"/>/
/// <see cref="MusicTag.Core.History.EditHistory.TryRedo"/>) per plan section 4 — in every case
/// the underlying <see cref="MusicTag.Core.Models.AudioFile.FileName"/> was left untouched by
/// the failed <c>Rename</c> call, so there's nothing to "revert" beyond just not applying the
/// edit — the grid cell already reflects the unchanged name.
/// </summary>
public partial class RenameErrorDialog : FluentWindow
{
    public RenameErrorDialog(string message)
    {
        InitializeComponent();

        Message = message;
        DataContext = this;
    }

    public string Message { get; }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
