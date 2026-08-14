namespace Core.Music;

/// <summary>
/// Current-track metadata exposed by a music player. Fields are optional because backends differ.
/// </summary>
/// <param name="Id">Gets an optional backend-specific track identifier.</param>
/// <param name="Title">Gets an optional track title.</param>
/// <param name="Artist">Gets an optional artist name.</param>
/// <param name="Album">Gets an optional album name.</param>
public sealed record MusicTrack(
    string? Id,
    string? Title,
    string? Artist,
    string? Album);
