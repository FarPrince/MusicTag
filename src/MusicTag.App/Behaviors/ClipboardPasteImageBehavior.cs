using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;

namespace MusicTag.App.Behaviors;

/// <summary>
/// Ctrl+V clipboard-image-paste support for <see cref="Controls.AlbumArtControl"/>, via
/// Avalonia.Xaml.Interactivity (the Avalonia fork of Microsoft.Xaml.Behaviors.Wpf — same
/// <c>Interaction.Behaviors</c> XAML attach syntax, same <c>Behavior&lt;T&gt;</c> shape).
///
/// "Scoped to when the album art control has focus" falls out of Avalonia's routing model the
/// same way it did in WPF: <see cref="InputElement.KeyDownEvent"/> is handled here in the
/// tunneling (Preview) phase via <c>AddHandler(..., RoutingStrategies.Tunnel)</c>, which only
/// reaches this element if focus is somewhere inside its subtree. AlbumArtControl makes itself
/// focusable and grabs focus on click (see its code-behind) so this fires even without an
/// intervening button click.
/// </summary>
public sealed class ClipboardPasteImageBehavior : Behavior<InputElement>
{
    public static readonly StyledProperty<ICommand?> PasteImageCommandProperty =
        AvaloniaProperty.Register<ClipboardPasteImageBehavior, ICommand?>(nameof(PasteImageCommand));

    /// <summary>Invoked with the pasted image re-encoded as PNG bytes (see
    /// <see cref="ClipboardImageHelper"/>) whenever Ctrl+V is pressed while the clipboard holds
    /// an image and the associated element (or a descendant) has keyboard focus. In practice
    /// bound to <see cref="ViewModels.AlbumArtViewModel"/>'s ApplyImageBytesCommand, the same
    /// all-or-nothing batch-apply path Replace/drag-drop/the context menu's Paste entry use.</summary>
    public ICommand? PasteImageCommand
    {
        get => GetValue(PasteImageCommandProperty);
        set => SetValue(PasteImageCommandProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject?.AddHandler(InputElement.KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDetaching()
    {
        AssociatedObject?.RemoveHandler(InputElement.KeyDownEvent, OnPreviewKeyDown);
        base.OnDetaching();
    }

    private async void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.V || e.KeyModifiers != KeyModifiers.Control)
            return;

        var clipboard = TopLevel.GetTopLevel(AssociatedObject)?.Clipboard;
        var bytes = await ClipboardImageHelper.TryGetImageBytesAsync(clipboard);
        if (bytes is null)
            return;

        if (PasteImageCommand?.CanExecute(bytes) == true)
        {
            PasteImageCommand.Execute(bytes);
            e.Handled = true;
        }
    }
}
