namespace Vidarr.Contracts.Models;

public sealed record Quality(int Id, string Name, Resolution Resolution, Source Source)
{
    public static readonly Quality Unknown = new(1, "Unknown", Resolution.Unknown, Source.Unknown);
    public static readonly Quality Webdl480p = new(2, "WEBDL-480p", Resolution.R480p, Source.Webdl);
    public static readonly Quality Webdl720p = new(3, "WEBDL-720p", Resolution.R720p, Source.Webdl);
    public static readonly Quality Webdl1080p = new(4, "WEBDL-1080p", Resolution.R1080p, Source.Webdl);
    public static readonly Quality Webdl2160p = new(5, "WEBDL-2160p", Resolution.R2160p, Source.Webdl);
    public static readonly Quality Hdtv720p = new(6, "HDTV-720p", Resolution.R720p, Source.Hdtv);
    public static readonly Quality Hdtv1080p = new(7, "HDTV-1080p", Resolution.R1080p, Source.Hdtv);
    public static readonly Quality Dvd = new(8, "DVD", Resolution.R480p, Source.Dvd);
    public static readonly Quality Bluray720p = new(9, "BluRay-720p", Resolution.R720p, Source.Bluray);
    public static readonly Quality Bluray1080p = new(10, "BluRay-1080p", Resolution.R1080p, Source.Bluray);
    public static readonly Quality Bluray2160p = new(11, "BluRay-2160p", Resolution.R2160p, Source.Bluray);
    public static readonly Quality RawHd = new(12, "Raw-HD", Resolution.Unknown, Source.Raw);

    public static IReadOnlyList<Quality> All { get; } =
    [
        Unknown,
        Webdl480p, Webdl720p, Webdl1080p, Webdl2160p,
        Hdtv720p, Hdtv1080p,
        Dvd,
        Bluray720p, Bluray1080p, Bluray2160p,
        RawHd,
    ];

    public static Quality? FromId(int id) => All.FirstOrDefault(q => q.Id == id);
}
