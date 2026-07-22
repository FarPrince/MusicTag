namespace MusicTag.Core.Services;

/// <summary>
/// Thrown by <see cref="AudioFileService"/> when ATL's <c>Track.Save()</c> returns
/// <see langword="false"/> without itself throwing — ATL reports tag-write failures via a
/// bool return rather than an exception, so this wraps that into the same exception-based
/// failure path <see cref="IAudioFileService.SaveManyAsync"/> uses to isolate per-file
/// failures (locked file, permission denied, unsupported write, etc.) into
/// <see cref="BatchSaveResult.Failed"/>.
/// </summary>
public sealed class TagSaveException(string message) : Exception(message);
