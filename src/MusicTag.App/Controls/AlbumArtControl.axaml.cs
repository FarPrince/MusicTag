using System.IO;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace MusicTag.App.Controls;

/// <summary>
/// Real album-art editing control — Replace/Copy/Paste/Extract/Remove via a right-click context
/// menu (see <see cref="OnPointerReleased"/>), Ctrl+V paste/Ctrl+C copy, and drag-drop of an
/// image file. Every property is set explicitly by the caller (MainWindow.axaml, against
/// <see cref="ViewModels.AlbumArtViewModel"/>) rather than this control adopting
/// AlbumArtViewModel as its own DataContext — kept "dumb", so byte[]-to-Bitmap conversion (and
/// image-file-to-byte[] extraction for drag-drop) stays here rather than in a view model, which
/// can stay plain data and easy to unit test without an Avalonia UI-thread host.
/// </summary>
public partial class AlbumArtControl : UserControl
{
    private static readonly HashSet<string> ImageFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif",
    };

    public static readonly StyledProperty<byte[]?> ImageBytesProperty =
        AvaloniaProperty.Register<AlbumArtControl, byte[]?>(nameof(ImageBytes));

    /// <summary>True when the current (multi-file) selection's effective album art disagrees
    /// across files — see <see cref="ViewModels.AlbumArtViewModel.IsMixed"/>. Takes priority
    /// over <see cref="ImageBytes"/> when deciding what to show (a mixed selection never shows a
    /// specific image nor the "No Album Art" placeholder — it shows its own distinct state
    /// instead).</summary>
    public static readonly StyledProperty<bool> IsMixedProperty =
        AvaloniaProperty.Register<AlbumArtControl, bool>(nameof(IsMixed));

    public static readonly StyledProperty<ICommand?> ReplaceCommandProperty =
        AvaloniaProperty.Register<AlbumArtControl, ICommand?>(nameof(ReplaceCommand));

    public static readonly StyledProperty<ICommand?> RemoveCommandProperty =
        AvaloniaProperty.Register<AlbumArtControl, ICommand?>(nameof(RemoveCommand));

    /// <summary>Saves the currently-displayed art to a standalone file. Bound to
    /// <see cref="ViewModels.AlbumArtViewModel"/>'s ExtractCommand.</summary>
    public static readonly StyledProperty<ICommand?> ExtractCommandProperty =
        AvaloniaProperty.Register<AlbumArtControl, ICommand?>(nameof(ExtractCommand));

    /// <summary>A one-line summary of the currently-displayed art, or empty when there's
    /// nothing to summarize. Sourced from <see cref="ViewModels.AlbumArtViewModel.ArtDetails"/>.
    /// Drives <see cref="DetailsText"/>'s visibility.</summary>
    public static readonly StyledProperty<string> ArtDetailsProperty =
        AvaloniaProperty.Register<AlbumArtControl, string>(nameof(ArtDetails), string.Empty);

    /// <summary>Shared sink for every non-file-picker image input source (Ctrl+V paste,
    /// drag-drop) — takes the new image's raw bytes as its command parameter. Bound (in
    /// MainWindow.axaml) to the same <see cref="ViewModels.AlbumArtViewModel"/> command Replace
    /// itself funnels into, so all three input sources produce an identical batch-apply
    /// edit.</summary>
    public static readonly StyledProperty<ICommand?> ApplyImageBytesCommandProperty =
        AvaloniaProperty.Register<AlbumArtControl, ICommand?>(nameof(ApplyImageBytesCommand));

    public AlbumArtControl()
    {
        InitializeComponent();

        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    /// <summary>ImageBytes/IsMixed/ArtDetails always change together (see
    /// AlbumArtViewModel.RefreshArt) — refreshing the whole visual state on any one of them
    /// changing is simpler and no more expensive than three separate targeted updates.</summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ImageBytesProperty || change.Property == IsMixedProperty || change.Property == ArtDetailsProperty)
        {
            UpdateVisualState();
        }
    }

    public byte[]? ImageBytes
    {
        get => GetValue(ImageBytesProperty);
        set => SetValue(ImageBytesProperty, value);
    }

    public bool IsMixed
    {
        get => GetValue(IsMixedProperty);
        set => SetValue(IsMixedProperty, value);
    }

    public ICommand? ReplaceCommand
    {
        get => GetValue(ReplaceCommandProperty);
        set => SetValue(ReplaceCommandProperty, value);
    }

    public ICommand? RemoveCommand
    {
        get => GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }

    public ICommand? ExtractCommand
    {
        get => GetValue(ExtractCommandProperty);
        set => SetValue(ExtractCommandProperty, value);
    }

    public string ArtDetails
    {
        get => GetValue(ArtDetailsProperty);
        set => SetValue(ArtDetailsProperty, value);
    }

    public ICommand? ApplyImageBytesCommand
    {
        get => GetValue(ApplyImageBytesCommandProperty);
        set => SetValue(ApplyImageBytesCommandProperty, value);
    }

    private void UpdateVisualState()
    {
        DetailsText.IsVisible = !string.IsNullOrEmpty(ArtDetails);

        if (IsMixed)
        {
            ArtImage.Source = null;
            ArtImage.IsVisible = false;
            Placeholder.IsVisible = false;
            MixedPlaceholder.IsVisible = true;
            return;
        }

        MixedPlaceholder.IsVisible = false;
        ApplyImage(ImageBytes);
    }

    private void ApplyImage(byte[]? bytes)
    {
        if (bytes is not { Length: > 0 })
        {
            ShowPlaceholder();
            return;
        }

        try
        {
            using var stream = new MemoryStream(bytes);
            var bitmap = new Bitmap(stream);

            ArtImage.Source = bitmap;
            ArtImage.IsVisible = true;
            Placeholder.IsVisible = false;
        }
        catch (Exception)
        {
            // Corrupt/unsupported embedded image data shouldn't crash the app — fall back to
            // the placeholder instead of propagating a decode exception.
            ShowPlaceholder();
        }
    }

    private void ShowPlaceholder()
    {
        ArtImage.Source = null;
        ArtImage.IsVisible = false;
        Placeholder.IsVisible = true;
    }

    /// <summary>Grabs keyboard focus on click so Ctrl+V works as soon as the user has clicked
    /// anywhere on the control (the image, the placeholder text, etc.).</summary>
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e) => Focus();

    /// <summary>The album-art context menu — Paste is an entry sharing
    /// <see cref="Behaviors.ClipboardImageHelper"/> with the Ctrl+V behavior so both stay in
    /// sync. Replace/Extract/Remove bind their existing Commands directly (so enabled/disabled
    /// state comes for free from each RelayCommand's own CanExecute); Copy/Paste are plain Click
    /// handlers since they have no ICommand backing of their own.
    ///
    /// Deliberately NOT a <see cref="MenuFlyout"/>/<see cref="Popup"/> — live testing on the real
    /// desktop showed the Popup positioner ignoring/misplacing the horizontal component of an
    /// explicit AnchorAndGravity offset for this control specifically, and that held regardless
    /// of which control the popup was anchored to (this control, or the top-level window with a
    /// pre-translated offset) or which windowing backend was forced (X11 or native Wayland) — see
    /// gleaming-squishing-gizmo.md's investigation notes. That symptom matches a known class of
    /// Avalonia Popup bug (e.g. GH #1573, GH #9845) rather than anything fixable from the offset
    /// math here. A small owned, undecorated <see cref="Window"/> positioned via a plain
    /// <see cref="Window.Position"/> (PixelPoint, physical pixels — the same units
    /// <see cref="Visual.PointToScreen"/> returns) uses a completely different code path than
    /// Popup and sidesteps the bug entirely. The menu content is a real
    /// <see cref="MenuFlyoutPresenter"/> (the same presenter type a genuine
    /// <see cref="MenuFlyout"/> uses, and the one FluentAvaloniaTheme already styles — see the
    /// header column-chooser menu) hosted directly as this window's content, so it keeps the
    /// same look (background, rounded corners, item hover highlight) as every other menu in the
    /// app despite not going through MenuFlyout itself.</summary>
    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Right)
            return;

        Focus();

        var screenPoint = this.PointToScreen(e.GetPosition(this));
        var owner = TopLevel.GetTopLevel(this) as Window;

        // Paste starts disabled and is enabled asynchronously once the clipboard check resolves.
        var pasteItem = new MenuItem { Header = "Paste", IsEnabled = false };
        var copyItem = new MenuItem { Header = "Copy", IsEnabled = !IsMixed && ImageBytes is { Length: > 0 } };
        var replaceItem = new MenuItem { Header = "Replace...", Command = ReplaceCommand };
        var extractItem = new MenuItem { Header = "Extract...", Command = ExtractCommand };
        var removeItem = new MenuItem { Header = "Remove", Command = RemoveCommand };

        var presenter = new MenuFlyoutPresenter
        {
            ItemsSource = new Control[] { replaceItem, pasteItem, copyItem, extractItem, new Separator(), removeItem },
        };

        var menuWindow = new Window
        {
            SystemDecorations = SystemDecorations.None,
            ShowInTaskbar = false,
            CanResize = false,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Topmost = true,
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
            Background = Brushes.Transparent,
            Position = screenPoint,
            Content = presenter,
        };

        menuWindow.Deactivated += (_, _) => menuWindow.Close();
        pasteItem.Click += async (_, _) => { menuWindow.Close(); await TryPasteFromClipboardAsync(); };
        copyItem.Click += async (_, _) => { menuWindow.Close(); await TryCopyImageToClipboardAsync(); };
        foreach (var item in new[] { replaceItem, extractItem, removeItem })
            item.Click += (_, _) => menuWindow.Close();

        if (owner is not null)
            menuWindow.Show(owner);
        else
            menuWindow.Show();

        e.Handled = true;

        _ = UpdatePasteEnabledAsync(pasteItem);
    }

    private async Task UpdatePasteEnabledAsync(MenuItem pasteItem)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        pasteItem.IsEnabled = clipboard is not null && (await clipboard.GetDataFormatsAsync()).Count > 0;
    }

    private async Task TryPasteFromClipboardAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        var bytes = await Behaviors.ClipboardImageHelper.TryGetImageBytesAsync(clipboard);
        if (bytes is null)
            return;

        if (ApplyImageBytesCommand?.CanExecute(bytes) == true)
        {
            ApplyImageBytesCommand.Execute(bytes);
        }
    }

    /// <summary>Ctrl+C — copies the currently-displayed album art to the clipboard as an image.
    /// Mirrors <see cref="Behaviors.ClipboardPasteImageBehavior"/>'s Ctrl+V handling (same
    /// tunneling scoping: this only fires while focus is somewhere inside this control), but
    /// lives directly on the control rather than as a separate behavior class — unlike paste,
    /// copy never mutates any editable state, so there's no ICommand indirection to a view model
    /// to justify a reusable behavior.</summary>
    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.C || e.KeyModifiers != KeyModifiers.Control)
            return;

        if (await TryCopyImageToClipboardAsync())
            e.Handled = true;
    }

    /// <summary>Re-decodes <see cref="ImageBytes"/> (rather than reusing <c>ArtImage.Source</c>
    /// directly) so this works identically regardless of what's currently on screen. Returns
    /// false when there's nothing sensible to copy — a mixed selection or "No Album Art" state.</summary>
    private async Task<bool> TryCopyImageToClipboardAsync()
    {
        if (IsMixed || ImageBytes is not { Length: > 0 } bytes)
            return false;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        await Behaviors.ClipboardImageHelper.SetImageAsync(clipboard, bytes);
        return true;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = IsImageFileDrag(e) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (!TryGetDroppedImagePath(e, out var path))
            return;

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception)
        {
            // Unreadable file dropped (locked, permission denied, vanished between drag-over
            // and drop) — silently ignore rather than crash; matches ApplyImage's own
            // "corrupt input never propagates an exception" stance.
            return;
        }

        if (ApplyImageBytesCommand?.CanExecute(bytes) == true)
        {
            ApplyImageBytesCommand.Execute(bytes);
        }

        e.Handled = true;
    }

    private static bool IsImageFileDrag(DragEventArgs e) => TryGetDroppedImagePath(e, out _);

    private static bool TryGetDroppedImagePath(DragEventArgs e, out string path)
    {
        path = string.Empty;

        if (!e.Data.Contains(DataFormats.Files))
            return false;

        if (e.Data.Get(DataFormats.Files) is not IEnumerable<IStorageItem> items)
            return false;

        var candidate = items.FirstOrDefault()?.Path.LocalPath;
        if (candidate is null || !ImageFileExtensions.Contains(Path.GetExtension(candidate)))
            return false;

        path = candidate;
        return true;
    }
}
