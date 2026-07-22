using MusicTag.App.ViewModels;
using Wpf.Ui.Controls;

namespace MusicTag.App.Views;

/// <summary>
/// M7: default startup folder, theme choice, Explorer-integration toggle. Code-behind is thin —
/// its only job is closing itself when <see cref="SettingsViewModel.RequestClose"/> fires (Save
/// or Cancel), the same "view model owns behavior, code-behind just bridges to WPF" split used
/// throughout the rest of the app.
/// </summary>
public partial class SettingsWindow : FluentWindow
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
