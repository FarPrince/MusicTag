using System.Windows;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace MusicTag.App.Behaviors;

/// <summary>
/// M6: Ctrl+V clipboard-image-paste support for <see cref="Controls.AlbumArtControl"/>, per
/// plan section 5 ("clipboard-paste-image and drag-drop handling" alongside the multi-select
/// grid behavior, all via Microsoft.Xaml.Behaviors.Wpf). Attaches to a plain
/// <see cref="UIElement"/> (in practice the AlbumArtControl itself) rather than anything more
/// specific, keeping this behavior reusable.
///
/// "Scoped to when the album art control has focus" (the plan's exact wording) falls out of
/// WPF's routing model for free rather than needing any manual focus-tracking: PreviewKeyDown
/// is a tunneling event that starts at the root visual and tunnels *down the path to whichever
/// element currently has keyboard focus*. If focus isn't somewhere inside this behavior's
/// AssociatedObject's subtree, the event tunnel never passes through it at all — so Ctrl+V
/// typed while, say, the file grid has focus is simply never seen here, with no extra
/// bookkeeping required. AlbumArtControl makes itself focusable and grabs focus on click (see
/// its code-behind) so this fires even without an intervening button click.
/// </summary>
public sealed class ClipboardPasteImageBehavior : Behavior<UIElement>
{
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.PreviewKeyDown += OnPreviewKeyDown;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.PreviewKeyDown -= OnPreviewKeyDown;
        base.OnDetaching();
    }

    public static readonly DependencyProperty PasteImageCommandProperty = DependencyProperty.Register(
        nameof(PasteImageCommand),
        typeof(ICommand),
        typeof(ClipboardPasteImageBehavior),
        new PropertyMetadata(null));

    /// <summary>Invoked with the pasted image re-encoded as PNG bytes (see
    /// <see cref="ClipboardImageHelper"/>) whenever Ctrl+V is pressed while the clipboard holds
    /// an image and the associated element (or a descendant) has keyboard focus. In practice
    /// bound to <see cref="ViewModels.AlbumArtViewModel"/>'s ApplyImageBytesCommand, the same
    /// all-or-nothing batch-apply path Replace/drag-drop/the context menu's Paste entry use.</summary>
    public ICommand? PasteImageCommand
    {
        get => (ICommand?)GetValue(PasteImageCommandProperty);
        set => SetValue(PasteImageCommandProperty, value);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.V || Keyboard.Modifiers != ModifierKeys.Control)
            return;

        if (!ClipboardImageHelper.TryGetImageBytes(out var bytes) || bytes is null)
            return;

        if (PasteImageCommand?.CanExecute(bytes) == true)
        {
            PasteImageCommand.Execute(bytes);
            e.Handled = true;
        }
    }
}
