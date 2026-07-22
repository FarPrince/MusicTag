namespace MusicTag.Core.Services;

/// <summary>
/// Thrown by <see cref="AudioFileService.Rename"/> when the requested target filename already
/// belongs to a different file in the same folder. Per plan section 3 ("validates the target
/// doesn't already exist first and surfaces a clear error rather than a raw IO exception for
/// that common case") — this is the one collision case worth its own type/message rather than
/// letting a raw <see cref="IOException"/> from <see cref="File.Move(string, string)"/> bubble
/// up to the App-layer RenameErrorDialog (other failures — locked file, permission denied,
/// invalid characters — are left to surface with their natural .NET exception message, which
/// is already clear enough for that dialog).
/// </summary>
public sealed class RenameTargetExistsException(string message) : Exception(message);
