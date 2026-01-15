namespace Core.Spotify.Models;

public sealed record PlaybackState(
    bool IsPlaying,
    int? VolumePercent,
    Track? Track,
    string? DeviceId,
    string? DeviceName
);
