using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Clipboard = System.Windows.Clipboard;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using UserControl = System.Windows.Controls.UserControl;

namespace MusicTag.App.Controls;

/// <summary>
/// M6: real album-art editing control — Replace/Copy/Paste/Extract/Remove via a right-click
/// context menu (see <see cref="OnPreviewMouseRightButtonUp"/>), Ctrl+V paste/Ctrl+C copy, and
/// drag-drop of an image file (handled directly here, since it's simple, control-local logic
/// with nothing to share with another behavior consumer). Every DP is set explicitly by the
/// caller (MainWindow.xaml, against <see cref="ViewModels.AlbumArtViewModel"/>) rather than this
/// control adopting AlbumArtViewModel as its own DataContext — kept "dumb" per the M2 doc
/// comment this class started with, so byte[]-to-BitmapImage conversion (and now
/// image-file-to-byte[] extraction for drag-drop) stays here rather than in a view model,
/// which can stay plain data and easy to unit test without a WPF/STA host.
/// </summary>
public partial class AlbumArtControl : UserControl
{
    private static readonly HashSet<string> ImageFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif",
    };

    public static readonly DependencyProperty ImageBytesProperty = DependencyProperty.Register(
        nameof(ImageBytes),
        typeof(byte[]),
        typeof(AlbumArtControl),
        new PropertyMetadata(null, OnVisualStateInputChanged));

    /// <summary>M6 addition: true when the current (multi-file) selection's effective album
    /// art disagrees across files — see <see cref="ViewModels.AlbumArtViewModel.IsMixed"/>.
    /// Takes priority over <see cref="ImageBytes"/> when deciding what to show (a mixed
    /// selection never shows a specific image nor the "No Album Art" placeholder — it shows
    /// its own distinct state instead).</summary>
    public static readonly DependencyProperty IsMixedProperty = DependencyProperty.Register(
        nameof(IsMixed),
        typeof(bool),
        typeof(AlbumArtControl),
        new PropertyMetadata(false, OnVisualStateInputChanged));

    public static readonly DependencyProperty ReplaceCommandProperty = DependencyProperty.Register(
        nameof(ReplaceCommand), typeof(ICommand), typeof(AlbumArtControl), new PropertyMetadata(null));

    public static readonly DependencyProperty RemoveCommandProperty = DependencyProperty.Register(
        nameof(RemoveCommand), typeof(ICommand), typeof(AlbumArtControl), new PropertyMetadata(null));

    /// <summary>Per user feedback ("provide an option to extract album art as an image") —
    /// saves the currently-displayed art to a standalone file. Bound to
    /// <see cref="ViewModels.AlbumArtViewModel"/>'s ExtractCommand.</summary>
    public static readonly DependencyProperty ExtractCommandProperty = DependencyProperty.Register(
        nameof(ExtractCommand), typeof(ICommand), typeof(AlbumArtControl), new PropertyMetadata(null));

    /// <summary>Per user feedback ("show album art details — image type, size, resolution,
    /// etc") — a one-line summary of the currently-displayed art, or empty when there's
    /// nothing to summarize. Sourced from <see cref="ViewModels.AlbumArtViewModel.ArtDetails"/>.
    /// Drives <see cref="DetailsText"/>'s Visibility (see <see cref="UpdateVisualState"/>) —
    /// registered with the same OnVisualStateInputChanged callback as ImageBytes/IsMixed since
    /// all three always change together (see AlbumArtViewModel.RefreshArt).</summary>
    public static readonly DependencyProperty ArtDetailsProperty = DependencyProperty.Register(
        nameof(ArtDetails), typeof(string), typeof(AlbumArtControl), new PropertyMetadata(string.Empty, OnVisualStateInputChanged));

    /// <summary>Shared sink for every non-file-picker image input source (Ctrl+V paste,
    /// drag-drop) — takes the new image's raw bytes as its command parameter. Bound (in
    /// MainWindow.xaml) to the same <see cref="ViewModels.AlbumArtViewModel"/> command Replace
    /// itself funnels into, so all three input sources produce an identical batch-apply
    /// edit.</summary>
    public static readonly DependencyProperty ApplyImageBytesCommandProperty = DependencyProperty.Register(
        nameof(ApplyImageBytesCommand), typeof(ICommand), typeof(AlbumArtControl), new PropertyMetadata(null));

    public AlbumArtControl()
    {
        InitializeComponent();
    }

    public byte[]? ImageBytes
    {
        get => (byte[]?)GetValue(ImageBytesProperty);
        set => SetValue(ImageBytesProperty, value);
    }

    public bool IsMixed
    {
        get => (bool)GetValue(IsMixedProperty);
        set => SetValue(IsMixedProperty, value);
    }

    public ICommand? ReplaceCommand
    {
        get => (ICommand?)GetValue(ReplaceCommandProperty);
        set => SetValue(ReplaceCommandProperty, value);
    }

    public ICommand? RemoveCommand
    {
        get => (ICommand?)GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }

    public ICommand? ExtractCommand
    {
        get => (ICommand?)GetValue(ExtractCommandProperty);
        set => SetValue(ExtractCommandProperty, value);
    }

    public string ArtDetails
    {
        get => (string)GetValue(ArtDetailsProperty);
        set => SetValue(ArtDetailsProperty, value);
    }

    public ICommand? ApplyImageBytesCommand
    {
        get => (ICommand?)GetValue(ApplyImageBytesCommandProperty);
        set => SetValue(ApplyImageBytesCommandProperty, value);
    }

    private static void OnVisualStateInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((AlbumArtControl)d).UpdateVisualState();

    private void UpdateVisualState()
    {
        DetailsText.Visibility = string.IsNullOrEmpty(ArtDetails) ? Visibility.Collapsed : Visibility.Visible;

        if (IsMixed)
        {
            ArtImage.Source = null;
            ArtImage.Visibility = Visibility.Collapsed;
            Placeholder.Visibility = Visibility.Collapsed;
            MixedPlaceholder.Visibility = Visibility.Visible;
            return;
        }

        MixedPlaceholder.Visibility = Visibility.Collapsed;
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
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            ArtImage.Source = bitmap;
            ArtImage.Visibility = Visibility.Visible;
            Placeholder.Visibility = Visibility.Collapsed;
        }
        catch (Exception)
        {
            // Corrupt/unsupported embedded image data shouldn't crash the app — fall back
            // to the placeholder instead of propagating a decode exception.
            ShowPlaceholder();
        }
    }

    private void ShowPlaceholder()
    {
        ArtImage.Source = null;
        ArtImage.Visibility = Visibility.Collapsed;
        Placeholder.Visibility = Visibility.Visible;
    }

    /// <summary>Grabs keyboard focus on click so Ctrl+V works as soon as the user has clicked
    /// anywhere on the control (the image, the placeholder text, etc.) — not only after
    /// clicking specifically into a focusable child like one of the buttons. Pairs with
    /// <see cref="Behaviors.ClipboardPasteImageBehavior"/>'s reliance on WPF's tunneling
    /// PreviewKeyDown routing for "scoped to when this control has focus."</summary>
    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => Focus();

    /// <summary>The album-art context menu the user asked for ("move the album art options to
    /// a context menu (right click)"), replacing the old always-visible Replace/Copy/Extract/
    /// Remove button row (and the hint text explaining drag-drop/paste/copy, no longer needed
    /// once the menu spells the same actions out on demand) — same "build a ContextMenu in
    /// code" approach already established for the main grid's column chooser
    /// (MainWindow.xaml.cs). Paste is a new menu entry with no prior button equivalent (Ctrl+V
    /// always worked, but had no mouse-only discovery path once the hint text is gone), sharing
    /// <see cref="Behaviors.ClipboardImageHelper"/> with the Ctrl+V behavior so both stay in
    /// sync. Replace/Extract/Remove bind their existing Commands directly (so enabled/disabled
    /// state — e.g. Remove/Extract with nothing selected or no art — comes for free from each
    /// RelayCommand's own CanExecute); Copy/Paste are plain Click handlers since they have no
    /// ICommand backing of their own.</summary>
    private void OnPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        Focus();

        var pasteItem = new System.Windows.Controls.MenuItem { Header = "Paste", IsEnabled = Clipboard.ContainsImage() };
        pasteItem.Click += (_, _) => TryPasteFromClipboard();

        var copyItem = new System.Windows.Controls.MenuItem { Header = "Copy", IsEnabled = !IsMixed && ImageBytes is { Length: > 0 } };
        copyItem.Click += (_, _) => TryCopyImageToClipboard();

        var menu = new System.Windows.Controls.ContextMenu();
        menu.Items.Add(new System.Windows.Controls.MenuItem { Header = "Replace...", Command = ReplaceCommand });
        menu.Items.Add(pasteItem);
        menu.Items.Add(copyItem);
        menu.Items.Add(new System.Windows.Controls.MenuItem { Header = "Extract...", Command = ExtractCommand });
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(new System.Windows.Controls.MenuItem { Header = "Remove", Command = RemoveCommand });

        menu.PlacementTarget = this;
        menu.IsOpen = true;
        e.Handled = true;
    }

    /// <summary>Paste menu entry — same clipboard read/re-encode as
    /// <see cref="Behaviors.ClipboardPasteImageBehavior"/>'s Ctrl+V (via the shared
    /// <see cref="Behaviors.ClipboardImageHelper"/>), routed through the same
    /// <see cref="ApplyImageBytesCommand"/> sink as Ctrl+V/drag-drop/Replace.</summary>
    private void TryPasteFromClipboard()
    {
        if (!Behaviors.ClipboardImageHelper.TryGetImageBytes(out var bytes) || bytes is null)
            return;

        if (ApplyImageBytesCommand?.CanExecute(bytes) == true)
        {
            ApplyImageBytesCommand.Execute(bytes);
        }
    }

    /// <summary>Ctrl+C — copies the currently-displayed album art to the clipboard as an image,
    /// per user feedback ("unable to clear or copy album art"). Mirrors
    /// <see cref="Behaviors.ClipboardPasteImageBehavior"/>'s Ctrl+V handling (same
    /// tunneling-PreviewKeyDown scoping: this only fires while focus is somewhere inside this
    /// control), but lives directly on the control rather than as a separate behavior class —
    /// unlike paste, copy never mutates any editable state (no command/undo-history involvement
    /// at all), so there's no ICommand indirection to a view model to justify a reusable
    /// behavior. No-ops (does not set e.Handled) when there's nothing to copy, so Ctrl+C still
    /// falls through normally if some other control ends up focused with no art showing.</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.C || Keyboard.Modifiers != ModifierKeys.Control)
            return;

        if (TryCopyImageToClipboard())
            e.Handled = true;
    }

    /// <summary>Re-decodes <see cref="ImageBytes"/> (rather than reusing <c>ArtImage.Source</c>
    /// directly) so this works identically regardless of what's currently on screen, and returns
    /// whether there was actually an image to copy — a mixed selection or "No Album Art" state
    /// has nothing sensible to put on the clipboard.</summary>
    private bool TryCopyImageToClipboard()
    {
        if (IsMixed || ImageBytes is not { Length: > 0 } bytes)
            return false;

        try
        {
            using var stream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            Clipboard.SetImage(bitmap);
            return true;
        }
        catch (Exception)
        {
            // Corrupt embedded image data, or the clipboard held open by another process —
            // never let a copy attempt crash the app (same stance as ApplyImage/paste).
            return false;
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = IsImageFileDrag(e) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
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

        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return false;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } paths)
            return false;

        var candidate = paths[0];
        if (!ImageFileExtensions.Contains(Path.GetExtension(candidate)))
            return false;

        path = candidate;
        return true;
    }
}
