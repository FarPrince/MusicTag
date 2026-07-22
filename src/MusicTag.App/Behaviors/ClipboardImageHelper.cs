using System.IO;
using System.Windows.Media.Imaging;
using Clipboard = System.Windows.Clipboard;

namespace MusicTag.App.Behaviors;

/// <summary>
/// Shared clipboard-image-read logic, factored out of <see cref="ClipboardPasteImageBehavior"/>
/// (Ctrl+V) once <see cref="Controls.AlbumArtControl"/>'s new right-click context menu (per user
/// feedback — "move the album art options to a context menu") needed a second "Paste" entry
/// point for the exact same operation. Both call sites need identical behavior — same clipboard
/// read, same PNG re-encode, same "never throw on a locked/empty clipboard" stance — so this
/// stays a single shared implementation rather than two near-duplicates.
/// </summary>
public static class ClipboardImageHelper
{
    /// <summary>Reads the clipboard's current image (if any) and re-encodes it as PNG bytes —
    /// clipboard images can arrive in whatever pixel format the source app used (DIB, PNG,
    /// etc. — WPF's <see cref="Clipboard.GetImage"/> normalizes all of them to a single
    /// <see cref="BitmapSource"/>), and PNG gives <see cref="MusicTag.Core.Models.AlbumArtEdit.NewImageBytes"/>
    /// a concrete, ATL-readable byte format regardless of what the source app copied. Returns
    /// false (with <paramref name="bytes"/> null) whenever there's nothing to paste or the
    /// clipboard/decode throws (e.g. another process holds the clipboard open) — never lets a
    /// paste attempt crash the app.</summary>
    public static bool TryGetImageBytes(out byte[]? bytes)
    {
        bytes = null;

        if (!Clipboard.ContainsImage())
            return false;

        BitmapSource? bitmapSource;
        try
        {
            bitmapSource = Clipboard.GetImage();
        }
        catch (Exception)
        {
            return false;
        }

        if (bitmapSource is null)
            return false;

        try
        {
            bytes = EncodeAsPng(bitmapSource);
        }
        catch (Exception)
        {
            return false;
        }

        return true;
    }

    private static byte[] EncodeAsPng(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
