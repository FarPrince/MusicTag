using System.IO;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;

namespace MusicTag.App.Behaviors;

/// <summary>
/// Shared clipboard-image read/write logic used by both <see cref="ClipboardPasteImageBehavior"/>
/// (Ctrl+V) and <see cref="Controls.AlbumArtControl"/>'s right-click Paste/Copy menu entries.
///
/// Unlike WPF's <c>Clipboard.GetImage()</c>/<c>SetImage()</c> (which normalize to/from a single
/// typed <c>BitmapSource</c> regardless of what the source app copied), Avalonia's
/// <see cref="IClipboard"/> only exposes raw format-string/object pairs
/// (<see cref="IClipboard.GetFormatsAsync"/>/<see cref="IClipboard.GetDataAsync"/>) — there is no
/// single canonical "the clipboard image" accessor, and which MIME format string a real desktop
/// clipboard actually offers for an image is compositor/toolkit-dependent (GTK/Nautilus, Qt/KDE,
/// a browser, etc. don't all agree). <see cref="ImageFormats"/> lists every format this has been
/// observed to need in practice; reading is deliberately tolerant of the value coming back as
/// either raw <c>byte[]</c> or a <see cref="Stream"/> depending on backend. This is a genuine,
/// documented platform difference from the WPF original — see CLAUDE.md's Avalonia-porting notes.
/// </summary>
public static class ClipboardImageHelper
{
    private static readonly string[] ImageFormats =
    [
        "image/png", "PNG", "image/bmp", "image/jpeg", "image/gif", "image/x-mswindowsdib",
    ];

    /// <summary>Reads the clipboard's current image (if any) and re-encodes it as PNG bytes, so
    /// <see cref="MusicTag.Core.Models.AlbumArtEdit.NewImageBytes"/> always gets a concrete,
    /// ATL-readable byte format regardless of what format the source app/clipboard offered.
    /// Returns null whenever there's nothing to paste, no format this recognizes is offered, or
    /// reading/decoding throws — never lets a paste attempt crash the app.</summary>
    public static async Task<byte[]?> TryGetImageBytesAsync(IClipboard? clipboard)
    {
        if (clipboard is null)
            return null;

        try
        {
            var formats = await clipboard.GetFormatsAsync();
            var format = ImageFormats.FirstOrDefault(f => formats.Contains(f, StringComparer.OrdinalIgnoreCase));
            if (format is null)
                return null;

            var raw = await clipboard.GetDataAsync(format);
            var rawBytes = raw switch
            {
                byte[] b => b,
                Stream s => await ReadAllBytesAsync(s),
                _ => null,
            };

            if (rawBytes is not { Length: > 0 })
                return null;

            // Re-decode/re-encode so downstream code always sees a plain PNG regardless of the
            // clipboard's original format (e.g. a raw DIB) — mirrors the WPF original's
            // BitmapSource-normalize-to-PNG step.
            using var sourceStream = new MemoryStream(rawBytes);
            using var bitmap = new Bitmap(sourceStream);
            using var pngStream = new MemoryStream();
            bitmap.Save(pngStream);
            return pngStream.ToArray();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Writes <paramref name="pngBytes"/> to the clipboard under the "image/png" MIME
    /// format — the same format this class itself reads back, and the most broadly recognized
    /// image MIME type across Linux desktop clipboards.</summary>
    public static async Task SetImageAsync(IClipboard? clipboard, byte[] pngBytes)
    {
        if (clipboard is null)
            return;

        try
        {
            var dataObject = new DataObject();
            dataObject.Set("image/png", pngBytes);
            await clipboard.SetDataObjectAsync(dataObject);
        }
        catch (Exception)
        {
            // Never let a copy attempt crash the app — matches this class's own read-side stance.
        }
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream)
    {
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }
}
