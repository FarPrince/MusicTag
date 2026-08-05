using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace MusicTag.App.Views;

/// <summary>
/// Wires two cross-cutting keyboard behaviors for every field TextBox in this panel: commit-on-
/// Enter, matching the "field loses focus/Enter" commit trigger (LostFocus alone is already
/// handled by each TextBox's own binding — see EditPanelView.axaml's UpdateSourceTrigger), and
/// Delete-to-clear-field. Everything else is plain data-binding to EditPanelViewModel;
/// code-behind stays otherwise empty.
/// </summary>
public partial class EditPanelView : UserControl
{
    public EditPanelView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is not TextBox textBox)
            return;

        switch (e.Key)
        {
            case Key.Enter:
                // No field in this panel is multi-line (AcceptsReturn), but this guard is kept
                // in case a future field needs it: such a field should get a newline instead of
                // committing.
                if (textBox.AcceptsReturn)
                    return;

                BindingOperations.GetBindingExpressionBase(textBox, TextBox.TextProperty)?.UpdateSource();
                e.Handled = true;
                break;

            case Key.Delete:
                // Deliberately overrides Delete's ordinary "remove the character after the
                // caret" behavior for every field here (including Comment) rather than only
                // acting when the whole field is already selected — these are metadata-tagger
                // fields typically replaced wholesale (retype/paste a new value), not
                // surgically edited character-by-character, matching Mp3tag's own per-field-
                // clear convention (Backspace remains available for ordinary character-level
                // edits). Clearing to empty string (rather than directly to null) then pushing
                // it through the binding keeps this converter-agnostic — NullableIntToStringConverter
                // already maps an empty string back to null for Year/Track#/Disc#.
                textBox.Text = string.Empty;
                BindingOperations.GetBindingExpressionBase(textBox, TextBox.TextProperty)?.UpdateSource();
                e.Handled = true;
                break;
        }
    }
}
