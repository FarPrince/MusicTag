using Avalonia.Controls;
using MusicTag.App.ViewModels;

namespace MusicTag.App.Views;

/// <summary>
/// Default startup folder, theme choice, file-manager-integration toggle. Code-behind is thin —
/// its only job is closing itself when <see cref="SettingsViewModel.RequestClose"/> fires (Save
/// or Cancel), the same "view model owns behavior, code-behind just bridges to the UI framework"
/// split used throughout the rest of the app.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        _viewModel.RequestClose += OnRequestClose;
        Closed += (_, _) => _viewModel.RequestClose -= OnRequestClose;
    }

    private void OnRequestClose(object? sender, EventArgs e) => Close();
}
