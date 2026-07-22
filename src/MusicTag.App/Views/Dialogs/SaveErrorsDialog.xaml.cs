using MusicTag.Core.Models;
using Wpf.Ui.Controls;

namespace MusicTag.App.Views.Dialogs;

/// <summary>
/// End-of-batch-save error report, shown when <c>BatchSaveResult.Failed</c> is non-empty
/// (see MainWindowViewModel.SaveAllCommand / IDialogService.ShowSaveErrors). One row per
/// failed file with its exception message; the files that did save are simply not listed
/// here (the whole point is only the failures need the user's attention).
/// </summary>
public partial class SaveErrorsDialog : FluentWindow
{
    public SaveErrorsDialog(IReadOnlyList<(AudioFile File, Exception Error)> failures)
    {
        InitializeComponent();

        Failures = failures
            .Select(f => new SaveErrorItem(f.File.FileName, f.Error.Message))
            .ToList();

        DataContext = this;
    }

    public IReadOnlyList<SaveErrorItem> Failures { get; }

    private void OnCloseClick(object sender, System.Windows.RoutedEventArgs e) => Close();
}

public sealed record SaveErrorItem(string FileName, string Message);
