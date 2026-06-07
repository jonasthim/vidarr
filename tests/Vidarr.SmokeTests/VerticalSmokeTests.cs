using System.Net;
using System.Net.Http.Json;
using Vidarr.Api;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Models;
using Vidarr.Tests.Common;

namespace Vidarr.SmokeTests;

/// <summary>
/// One golden-path scenario: add an artist via IMVDb fixture, then perform a manual
/// grab against the default download client (yt-dlp), and verify the download was
/// invoked with the source URL.
/// </summary>
[Collection(nameof(SmokeCollection))]
public class VerticalSmokeTests
{
    private readonly SmokeFactory _factory;
    private readonly HttpClient _client;

    public VerticalSmokeTests(SmokeFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", "smoke-key");
    }

    [Fact]
    public async Task Golden_path_add_artist_then_manual_grab_invokes_yt_dlp()
    {
        // 1. Stub the IMVDb backend so /artist with provider=imvdb returns canned data.
        const string artistJson = """
        { "id": 42, "name": "Smoke Test Band", "country": "SE", "social_links": [] }
        """;
        _factory.HttpClient.WhenRequest(
            r => r.Uri.AbsolutePath.Contains("/artist/42", StringComparison.Ordinal),
            HttpClientResponseFactory.Json(artistJson));

        var add = new AddArtistRequest("imvdb", "42", "/library", 1, MonitorMode.All);
        var addResp = await _client.PostAsJsonAsync(new Uri("/api/v1/artist", UriKind.Relative), add);
        addResp.StatusCode.Should().Be(HttpStatusCode.Created);

        // 2. Stub yt-dlp so the download appears to succeed and "produces" a file
        //    in the FakeFileSystem. The real yt-dlp client writes via IFileSystem
        //    when the process emits the "[download] Destination: ..." line — we
        //    don't need that level of fidelity here; the process result is enough.
        _factory.ProcessRunner.WhenInvocation(
            inv => inv.Arguments.Any(a => a.Contains("https://example.invalid/cc-clip", StringComparison.Ordinal)),
            new ProcessResult(0, "[download] 100% of 5.00MiB", string.Empty, TimeSpan.FromSeconds(2)));

        // 3. Manual grab: the title parser turns this into a RemoteRelease, the
        //    download client gets handed the URL.
        var grab = new ReleaseGrabRequest(
            Title: "Smoke Test Band - Smoke Song (Official Video) 2026",
            SourceUrl: "https://example.invalid/cc-clip",
            Magnet: null,
            SizeBytes: null,
            PublishedAt: null,
            Seeders: null,
            Leechers: null,
            Protocol: "Streaming",
            IndexerName: "smoke",
            IndexerCategory: null,
            MusicVideoIds: null,
            ExtraMetadata: null);
        var grabResp = await _client.PostAsJsonAsync(new Uri("/api/v1/release/grab", UriKind.Relative), grab);
        grabResp.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var grabDto = await grabResp.Content.ReadFromJsonAsync<ReleaseGrabResponse>();
        grabDto.Should().NotBeNull();
        grabDto!.Title.Should().Be(grab.Title);

        // 4. Assert: yt-dlp got invoked with the URL we passed.
        _factory.ProcessRunner.Invocations.Should().Contain(i =>
            i.Arguments.Any(a => a.Contains("https://example.invalid/cc-clip", StringComparison.Ordinal)));
    }
}
