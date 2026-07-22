using System.Windows.Data;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;
using UserControl = System.Windows.Controls.UserControl;

namespace MusicTag.App.Views;

/// <summary>
/// Wires two cross-cutting keyboard behaviors for every field TextBox in this panel: commit-on-
/// Enter, matching the plan's "field loses focus/Enter" commit trigger (LostFocus alone is
/// already handled by each TextBox's own binding), and (M8) Delete-to-clear-field, per plan
/// section 8's "Delete (clear a field's value when the edit panel has focus)". Everything else
/// is plain data-binding to EditPanelViewModel; code-behind stays otherwise empty.
///
/// (WinForms is also referenced by MusicTag.App — see MusicTag.App.csproj — as a fallback
/// folder-picker path, which is why System.Windows.Forms is an implicit global using here
/// too and UserControl/TextBox/KeyEventArgs need explicit aliases to disambiguate from
/// their WinForms namesakes.)
/// </summary>
public partial class EditPanelView : UserControl
{
    public EditPanelView()
    {
        InitializeComponent();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is not TextBox textBox)
            return;

        switch (e.Key)
        {
            case Key.Enter:
                // No field in this panel is multi-line anymore (Comment used to be — see
                // EditPanelView.xaml), but this guard is kept in case a future field needs
                // AcceptsReturn: such a field should get a newline instead of committing.
                if (textBox.AcceptsReturn)
                    return;

                BindingOperations.GetBindingExpression(textBox, TextBox.TextProperty)?.UpdateSource();
                e.Handled = true;
                break;

            case Key.Delete:
                // M8: this deliberately overrides Delete's ordinary "remove the character
                // after the caret" behavior for every field here (including Comment) rather
                // than only acting when the whole field is already selected — these are
                // metadata-tagger fields typically replaced wholesale (retype/paste a new
                // value), not surgically edited character-by-character, matching Mp3tag's own
                // per-field-clear convention (Backspace remains available for ordinary
                // character-level edits). Clearing to empty string (rather than directly to
                // null) then pushing it through the binding keeps this converter-agnostic —
                // NullableIntToStringConverter already maps an empty string back to null for
                // Year/Track#/Disc#, the same path a manually-cleared textbox already takes.
                textBox.Text = string.Empty;
                BindingOperations.GetBindingExpression(textBox, TextBox.TextProperty)?.UpdateSource();
                e.Handled = true;
                break;
        }
    }
}
