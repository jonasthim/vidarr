using System.Net;
using System.Net.Http.Json;
using Vidarr.Api;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Models;
using Vidarr.Tests.Common;

namespace Vidarr.IntegrationTests;

public class EndToEndTests : IClassFixture<VidarrTestFactory>
{
    private readonly VidarrTestFactory _factory;
    private readonly HttpClient _client;

    public EndToEndTests(VidarrTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", "test-key");
    }

    [Fact]
    public async Task System_status_is_reachable_without_api_key()
    {
        using var anon = _factory.CreateClient();
        var resp = await anon.GetAsync(new Uri("/api/v1/system/status", UriKind.Relative));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Adding_artist_uses_metadata_provider_and_persists()
    {
        var artistDetails = """
        { "id": 9999, "name": "Integration Test Artist", "country": "Wonderland",
          "social_links": [] }
        """;
        _factory.HttpClient.WhenRequest(
            r => r.Uri.AbsolutePath.Contains("/artist/9999", StringComparison.Ordinal),
            HttpClientResponseFactory.Json(artistDetails));

        var add = new AddArtistRequest("imvdb", "9999", "/tmp/library", 1, MonitorMode.All);
        var resp = await _client.PostAsJsonAsync(new Uri("/api/v1/artist", UriKind.Relative), add);

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await resp.Content.ReadFromJsonAsync<ArtistDto>();
        dto.Should().NotBeNull();
        dto!.Name.Should().Be("Integration Test Artist");

        // round-trip via GET
        var list = await _client.GetFromJsonAsync<ArtistDto[]>(new Uri("/api/v1/artist", UriKind.Relative));
        list.Should().NotBeNull();
        list!.Any(a => a.Name == "Integration Test Artist").Should().BeTrue();
    }

    [Fact]
    public async Task Artist_lookup_round_trips_through_metadata_provider()
    {
        const string searchBody = """
        { "results": [
            { "id": 1, "name": "Search Result Artist", "country": "ITland", "url": "https://imvdb.com/n/x" }
        ] }
        """;
        _factory.HttpClient.WhenRequest(
            r => r.Uri.AbsolutePath.Contains("/search/entities", StringComparison.Ordinal),
            HttpClientResponseFactory.Json(searchBody));

        var resp = await _client.PostAsJsonAsync(new Uri("/api/v1/artist/lookup", UriKind.Relative),
            new ArtistLookupRequest("Search"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var results = await resp.Content.ReadFromJsonAsync<ArtistLookupResult[]>();
        results!.Should().HaveCount(1);
        results![0].Name.Should().Be("Search Result Artist");
    }

    [Fact]
    public async Task Command_endpoint_enqueues_and_dispatcher_dequeues()
    {
        // Just verify the command pipeline returns Accepted; the dispatcher itself will
        // try to handle ArtistSearchCommand for a non-existent artist and log a warning.
        var resp = await _client.PostAsJsonAsync(new Uri("/api/v1/command", UriKind.Relative),
            new CommandRequest("ArtistSearch", ArtistId: 12345, MusicVideoId: null));
        resp.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Queue_endpoint_returns_array_even_when_empty()
    {
        var resp = await _client.GetAsync(new Uri("/api/v1/queue", UriKind.Relative));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().StartWith("[");
    }

    [Fact]
    public async Task Without_api_key_requests_are_rejected()
    {
        using var anon = _factory.CreateClient();
        var resp = await anon.GetAsync(new Uri("/api/v1/artist", UriKind.Relative));
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
