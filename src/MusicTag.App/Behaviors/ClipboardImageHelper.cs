using System.IO;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;

namespace MusicTag.App.Behaviors;

/// <summary>
/// Shared clipboard-image read/write logic used by both <see cref="ClipboardPasteImageBehavior"/>
/// (Ctrl+V) and <see cref="Controls.AlbumArtControl"/>'s right-click Paste/Copy menu entries.
///
/// Built on Avalonia's newer <see cref="DataFormat.Bitmap"/>/<see cref="IClipboard.SetDataAsync"/>/
/// <see cref="IClipboard.TryGetDataAsync"/> API rather than the older <c>DataObject</c>/
/// <c>SetDataObjectAsync</c>/<c>GetFormatsAsync</c>/<c>GetDataAsync</c> API it replaced — the older
/// API round-trips a raw byte array under a hand-picked MIME string, while <c>DataFormat.Bitmap</c>
/// carries a real <see cref="Bitmap"/> value end to end, avoiding a guessed-MIME-string mismatch.
/// Also falls back to <see cref="IClipboard.TryGetInProcessDataAsync"/> (X11/Windows/macOS-only —
/// retrieves the exact <c>IAsyncDataTransfer</c> this process itself last placed on the clipboard
/// via <see cref="IClipboard.SetDataAsync"/>, bypassing the full cross-process round-trip) for the
/// common copy-then-paste-within-this-app case, since that's more robust than depending on every
/// Linux clipboard manager to correctly serve back a just-set image to its own owning process.
/// </summary>
public static class ClipboardImageHelper
{
    /// <summary>Reads the clipboard's current image (if any) and re-encodes it as PNG bytes, so
    /// <see cref="MusicTag.Core.Models.AlbumArtEdit.NewImageBytes"/> always gets a concrete,
    /// ATL-readable byte format regardless of what format the source app/clipboard offered.
    /// Returns null whenever there's nothing to paste, no bitmap format is offered, or
    /// reading/decoding throws — never lets a paste attempt crash the app.</summary>
    public static async Task<byte[]?> TryGetImageBytesAsync(IClipboard? clipboard)
    {
        if (clipboard is null)
            return null;

        try
        {
            // TryGetDataAsync's result is owned by the caller and must be disposed; the
            // TryGetInProcessDataAsync fallback's result is still owned by the clipboard and must
            // NOT be disposed here — only dispose when the first call actually produced a result.
            var dataTransfer = await clipboard.TryGetDataAsync();
            var ownsDataTransfer = dataTransfer is not null;
            dataTransfer ??= await clipboard.TryGetInProcessDataAsync();
            if (dataTransfer is null)
                return null;

            try
            {
                using var bitmap = await dataTransfer.TryGetBitmapAsync();
                if (bitmap is null)
                    return null;

                using var pngStream = new MemoryStream();
                bitmap.Save(pngStream);
                return pngStream.ToArray();
            }
            finally
            {
                if (ownsDataTransfer)
                    dataTransfer.Dispose();
            }
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Writes <paramref name="pngBytes"/> to the clipboard under the
    /// <see cref="DataFormat.Bitmap"/> format.</summary>
    public static async Task SetImageAsync(IClipboard? clipboard, byte[] pngBytes)
    {
        if (clipboard is null)
            return;

        try
        {
            using var sourceStream = new MemoryStream(pngBytes);
            var bitmap = new Bitmap(sourceStream);
            var dataTransfer = new DataTransfer();
            dataTransfer.Add(DataTransferItem.Create(DataFormat.Bitmap, bitmap));
            await clipboard.SetDataAsync(dataTransfer);
        }
        catch (Exception)
        {
            // Never let a copy attempt crash the app — matches this class's own read-side stance.
        }
    }
}
