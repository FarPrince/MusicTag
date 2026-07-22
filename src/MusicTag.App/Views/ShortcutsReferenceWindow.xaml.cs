using Wpf.Ui.Controls;

namespace MusicTag.App.Views;

/// <summary>
/// M8: static keyboard-shortcuts reference (Help menu → "Keyboard Shortcuts"). No view model —
/// every row in ShortcutsReferenceWindow.xaml is fixed, compile-time content, so there's no
/// bound state for a view model to own (see the .xaml file's own doc comment).
/// </summary>
public partial class ShortcutsReferenceWindow : FluentWindow
{
    public ShortcutsReferenceWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object sender, System.Windows.RoutedEventArgs e) => Close();
}
