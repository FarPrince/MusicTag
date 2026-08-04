using Avalonia.Controls;
using Avalonia.Interactivity;
using MusicTag.App.ViewModels;

namespace MusicTag.App.Views.Dialogs;

/// <summary>
/// The "Search Lyrics" progress popup — see <see cref="LyricsSearchDialogViewModel"/> for the
/// actual search/progress/cancel logic this just displays. Kicks the search off from
/// <see cref="Window.Opened"/> (not the constructor, so DataContext/bindings are wired before
/// the first progress update lands) and cancels on close so a dismissed popup doesn't leave a
/// search still hammering LRCLib in the background.
/// </summary>
public partial class LyricsSearchDialog : Window
{
    private readonly LyricsSearchDialogViewModel _viewModel;

    public LyricsSearchDialog(LyricsSearchDialogViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private async void OnOpened(object? sender, EventArgs e) => await _viewModel.RunAsync();

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_viewModel.IsRunning)
        {
            _viewModel.CancelCommand.Execute(null);
        }
    }

    private void OnPrimaryButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.IsRunning)
        {
            _viewModel.CancelCommand.Execute(null);
        }
        else
        {
            Close();
        }
    }
}
