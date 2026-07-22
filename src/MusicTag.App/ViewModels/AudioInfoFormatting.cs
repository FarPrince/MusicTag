namespace MusicTag.App.ViewModels;

/// <summary>Shared presentation formatting for <see cref="MusicTag.Core.Models.ExtendedAudioInfo"/>
/// values, used by <see cref="FileListItemViewModel"/>, <see cref="EditPanelViewModel"/>, and
/// <see cref="AlbumArtViewModel"/> so file-size/channel-label wording can't drift between the
/// grid, the edit panel, and the album-art details text.</summary>
internal static class AudioInfoFormatting
{
    public static string FormatFileSize(long bytes)
    {
        const double kb = 1024;
        const double mb = kb * 1024;
        return bytes >= mb ? $"{bytes / mb:0.0} MB" : $"{bytes / kb:0.0} KB";
    }

    public static string FormatChannels(int channels) => channels switch
    {
        1 => "Mono",
        2 => "Stereo",
        0 => "Unknown",
        _ => $"{channels}ch",
    };
}
