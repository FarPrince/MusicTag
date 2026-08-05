using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MusicTag.App.Views;

/// <summary>
/// Static keyboard-shortcuts reference (toolbar's "Keyboard Shortcuts" button). No view model —
/// every row in ShortcutsReferenceWindow.axaml is fixed, compile-time content, so there's no
/// bound state for a view model to own.
/// </summary>
public partial class ShortcutsReferenceWindow : Window
{
    public ShortcutsReferenceWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
