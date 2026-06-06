using System.Diagnostics.CodeAnalysis;
using Vidarr.Contracts.Models;

namespace Vidarr.Api;

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record ArtistLookupRequest(string Query);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record ArtistLookupResult(string ProviderId, string Name, string? Disambiguation, string? Country, string? ThumbnailUrl);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record AddArtistRequest(string Provider, string ProviderId, string RootFolderPath, int QualityProfileId, MonitorMode MonitorMode);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record ArtistDto(int Id, string Name, string SortName, string? Country, bool Monitored, MonitorMode MonitorMode, string RootFolderPath, DateTimeOffset Added, IReadOnlyList<string> YouTubeChannelIds);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record YouTubeChannelsRequest(IReadOnlyList<string> ChannelIds);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record MusicVideoDto(int Id, int ArtistId, string Title, int? Year, MusicVideoType Type, bool Monitored, bool HasFile);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record CommandRequest(string Name, int? ArtistId, int? MusicVideoId);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record CommandResponse(string Status, string Message);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record SystemStatusDto(string Version, string Buildtime, bool Authenticated);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record ApiErrorResponse(IReadOnlyList<ApiError> Errors);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record ApiError(string PropertyName, string ErrorMessage);
