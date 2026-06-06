namespace Vidarr.Contracts.Models;

public enum DownloadProtocol
{
    Unknown = 0,
    Torrent = 1,
    Usenet = 2,
    Streaming = 3,
}

public enum MonitorMode
{
    None = 0,
    NewOnly = 1,
    All = 2,
}

public enum NotificationEventType
{
    OnGrab = 1,
    OnImport = 2,
    OnUpgrade = 3,
    OnDelete = 4,
    OnHealthIssue = 5,
    OnApplicationUpdate = 6,
    OnTest = 7,
}

public enum Source
{
    Unknown = 0,
    Webdl = 1,
    Bluray = 2,
    Hdtv = 3,
    Dvd = 4,
    Raw = 5,
}

public enum Resolution
{
    Unknown = 0,
    R480p = 480,
    R720p = 720,
    R1080p = 1080,
    R2160p = 2160,
}

public enum MusicVideoType
{
    Unknown = 0,
    Official = 1,
    Live = 2,
    Lyric = 3,
    Acoustic = 4,
    Alternative = 5,
    Cover = 6,
    Remix = 7,
}
