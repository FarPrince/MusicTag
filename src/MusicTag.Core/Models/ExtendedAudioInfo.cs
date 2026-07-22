namespace MusicTag.Core.Models;

/// <summary>
/// Read-only technical metadata (bitrate, sample rate, duration, etc.) shown to the
/// user as reference information only — never editable, never part of <see cref="TagFieldSet"/>.
/// </summary>
public sealed record ExtendedAudioInfo(
    string Codec,
    int BitrateKbps,
    int SampleRateHz,
    int Channels,
    TimeSpan Duration,
    long FileSizeBytes,
    bool IsVbr,
    string TagFormatsPresent,
    DateTime FileModifiedUtc);
