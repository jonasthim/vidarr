using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vidarr.Api;
using Vidarr.Catalog;
using Vidarr.Catalog.Repositories;

namespace Vidarr.Api.Tests;

public class SettingsEndpointsTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly IHost _host;
    private readonly HttpClient _client;

    public SettingsEndpointsTests()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();

        _host = new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.ConfigureServices(s =>
            {
                s.AddRouting();
                s.AddDbContext<VidarrDbContext>(o => o.UseSqlite(_conn));
                s.AddScoped<ITagRepository, TagRepository>();
                s.AddScoped<IQualityProfileRepository, QualityProfileRepository>();
                s.AddScoped<ICustomFormatRepository, CustomFormatRepository>();
                s.AddScoped<IBlocklistRepository, BlocklistRepository>();
                s.AddScoped<IHistoryRepository, HistoryRepository>();
                s.AddScoped<IIndexerConfigRepository, IndexerConfigRepository>();
                s.AddScoped<IDownloadClientConfigRepository, DownloadClientConfigRepository>();
                s.AddScoped<INotificationConfigRepository, NotificationConfigRepository>();
                s.AddScoped<IDiscoveryRuleSetRepository, DiscoveryRuleSetRepository>();
                s.AddScoped<IApplicationConfigRepository, ApplicationConfigRepository>();
                s.AddScoped<IRootFolderRepository, RootFolderRepository>();
            });
            web.Configure(app =>
            {
                using var scope = app.ApplicationServices.CreateScope();
                scope.ServiceProvider.GetRequiredService<VidarrDbContext>().Database.EnsureCreated();
                app.UseRouting();
                app.UseEndpoints(e => e.MapVidarrSettingsApi());
            });
        }).Start();

        _client = _host.GetTestClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _host.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task QualityDefinition_lists_seeded_constants()
    {
        var resp = await _client.GetAsync(new Uri("http://localhost/api/v1/qualitydefinition"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var qs = await resp.Content.ReadFromJsonAsync<QualityDto[]>();
        qs.Should().NotBeNull();
        qs!.Length.Should().BeGreaterThanOrEqualTo(12);
    }

    [Fact]
    public async Task Tag_CRUD()
    {
        var create = await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/tag"), new TagRequest("favorites"));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var t = await create.Content.ReadFromJsonAsync<TagDto>();

        var list = await _client.GetFromJsonAsync<TagDto[]>(new Uri("http://localhost/api/v1/tag"));
        list.Should().NotBeNull();
        list!.Should().ContainSingle();

        var del = await _client.DeleteAsync(new Uri($"http://localhost/api/v1/tag/{t!.Id}"));
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Tag_blank_label_returns_400()
    {
        var resp = await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/tag"), new TagRequest(""));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task QualityProfile_full_CRUD_round_trip()
    {
        var req = new QualityProfileRequest("HD", [3, 4], 4, true, 0, null, null, []);
        var create = await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/qualityprofile"), req);
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var p = await create.Content.ReadFromJsonAsync<QualityProfileDto>();

        var get = await _client.GetFromJsonAsync<QualityProfileDto>(new Uri($"http://localhost/api/v1/qualityprofile/{p!.Id}"));
        get!.Name.Should().Be("HD");
        get.AllowedQualityIds.Should().Equal(3, 4);

        var put = await _client.PutAsJsonAsync(new Uri($"http://localhost/api/v1/qualityprofile/{p.Id}"),
            req with { Name = "HD-renamed", UpgradeAllowed = false });
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await put.Content.ReadFromJsonAsync<QualityProfileDto>();
        updated!.Name.Should().Be("HD-renamed");
        updated.UpgradeAllowed.Should().BeFalse();

        var del = await _client.DeleteAsync(new Uri($"http://localhost/api/v1/qualityprofile/{p.Id}"));
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var notFound = await _client.GetAsync(new Uri($"http://localhost/api/v1/qualityprofile/{p.Id}"));
        notFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task QualityProfile_invalid_request_returns_400()
    {
        var bad = new QualityProfileRequest("", [], 1, true, 0, null, null, []);
        var resp = await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/qualityprofile"), bad);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task QualityProfile_update_404s_when_missing()
    {
        var req = new QualityProfileRequest("X", [3], 3, true, 0, null, null, []);
        var resp = await _client.PutAsJsonAsync(new Uri("http://localhost/api/v1/qualityprofile/9999"), req);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CustomFormat_CRUD()
    {
        var create = await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/customformat"),
            new CustomFormatRequest("VEVO", false, "[]"));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var f = await create.Content.ReadFromJsonAsync<CustomFormatDto>();

        var get = await _client.GetFromJsonAsync<CustomFormatDto>(new Uri($"http://localhost/api/v1/customformat/{f!.Id}"));
        get!.Name.Should().Be("VEVO");

        var put = await _client.PutAsJsonAsync(new Uri($"http://localhost/api/v1/customformat/{f.Id}"),
            new CustomFormatRequest("VEVO-renamed", true, "[{\"name\":\"x\"}]"));
        var updated = await put.Content.ReadFromJsonAsync<CustomFormatDto>();
        updated!.IncludeCustomFormatWhenRenaming.Should().BeTrue();

        await _client.DeleteAsync(new Uri($"http://localhost/api/v1/customformat/{f.Id}"));
    }

    [Fact]
    public async Task CustomFormat_blank_name_returns_400()
    {
        var resp = await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/customformat"),
            new CustomFormatRequest("", false, "[]"));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CustomFormat_404s_when_missing()
    {
        (await _client.GetAsync(new Uri("http://localhost/api/v1/customformat/9999"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var resp = await _client.PutAsJsonAsync(new Uri("http://localhost/api/v1/customformat/9999"),
            new CustomFormatRequest("X", false, "[]"));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Blocklist_add_list_delete()
    {
        var create = await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/blocklist"),
            new BlocklistRequest(1, 2, "Bad Release", "X", "manual"));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var b = await create.Content.ReadFromJsonAsync<BlocklistDto>();

        var list = await _client.GetFromJsonAsync<BlocklistDto[]>(new Uri("http://localhost/api/v1/blocklist"));
        list!.Should().ContainSingle();

        var del = await _client.DeleteAsync(new Uri($"http://localhost/api/v1/blocklist/{b!.Id}"));
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Blocklist_blank_release_returns_400()
    {
        var resp = await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/blocklist"),
            new BlocklistRequest(null, null, "", "X", null));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task History_returns_empty_array_when_no_entries()
    {
        var resp = await _client.GetAsync(new Uri("http://localhost/api/v1/history"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await resp.Content.ReadFromJsonAsync<HistoryDto[]>();
        list.Should().NotBeNull();
        list!.Should().BeEmpty();
    }

    [Fact]
    public async Task Indexer_config_CRUD()
    {
        var req = new IndexerConfigRequest("NZBdrone", "Newznab",
            "{\"url\":\"https://nzbdrone.example.com\"}", 10, true, true, true, null, []);
        var create = await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/indexer"), req);
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var i = await create.Content.ReadFromJsonAsync<IndexerConfigDto>();
        i!.Name.Should().Be("NZBdrone");
        i.Tags.Should().BeEmpty();

        var list = await _client.GetFromJsonAsync<IndexerConfigDto[]>(new Uri("http://localhost/api/v1/indexer"));
        list!.Should().ContainSingle();

        var put = await _client.PutAsJsonAsync(new Uri($"http://localhost/api/v1/indexer/{i.Id}"),
            req with { Priority = 99 });
        var updated = await put.Content.ReadFromJsonAsync<IndexerConfigDto>();
        updated!.Priority.Should().Be(99);

        await _client.DeleteAsync(new Uri($"http://localhost/api/v1/indexer/{i.Id}"));
        var notFound = await _client.GetAsync(new Uri($"http://localhost/api/v1/indexer/{i.Id}"));
        notFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DownloadClient_config_CRUD()
    {
        var req = new DownloadClientConfigRequest("qBit", "QBittorrent", "{\"host\":\"localhost\"}",
            1, true, "vidarr", false, []);
        var create = await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/downloadclient"), req);
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var c = await create.Content.ReadFromJsonAsync<DownloadClientConfigDto>();
        c!.Category.Should().Be("vidarr");
    }

    [Fact]
    public async Task Notification_config_CRUD()
    {
        var req = new NotificationConfigRequest("Discord", "Discord", "{\"webhook\":\"https://...\"}",
            true, [2, 3], []);
        var create = await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/notification"), req);
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var n = await create.Content.ReadFromJsonAsync<NotificationConfigDto>();
        n!.SubscribedEvents.Should().Equal(2, 3);
    }

    [Fact]
    public async Task DiscoveryRule_CRUD()
    {
        var req = new DiscoveryRuleSetRequest("Synthwave 2020+", true,
            "[{\"type\":\"GenreIn\",\"values\":[\"Synthwave\"]}]",
            "{\"qualityProfileId\":1,\"monitorMode\":\"All\"}");
        var create = await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/discoveryrule"), req);
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var r = await create.Content.ReadFromJsonAsync<DiscoveryRuleSetDto>();
        r!.Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task RootFolder_add_list_delete()
    {
        var create = await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/rootfolder"),
            new RootFolderRequest("/library"));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var rf = await create.Content.ReadFromJsonAsync<RootFolderDto>();
        rf!.Path.Should().Be("/library");
        var list = await _client.GetFromJsonAsync<RootFolderDto[]>(new Uri("http://localhost/api/v1/rootfolder"));
        list!.Should().ContainSingle();
        await _client.DeleteAsync(new Uri($"http://localhost/api/v1/rootfolder/{rf.Id}"));
    }

    [Fact]
    public async Task RootFolder_blank_path_returns_400()
    {
        var resp = await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/rootfolder"),
            new RootFolderRequest(""));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Host_config_get_and_put_round_trip()
    {
        var get = await _client.GetFromJsonAsync<HostConfigDto>(new Uri("http://localhost/api/v1/config/host"));
        get!.InstanceName.Should().Be("Vidarr");

        var put = await _client.PutAsJsonAsync(new Uri("http://localhost/api/v1/config/host"),
            new HostConfigRequest("My Vidarr", "/vidarr", "Debug"));
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await put.Content.ReadFromJsonAsync<HostConfigDto>();
        updated!.InstanceName.Should().Be("My Vidarr");
        updated.UrlBase.Should().Be("/vidarr");
        updated.LogLevel.Should().Be("Debug");
    }

    [Fact]
    public async Task Naming_config_get_and_put_round_trip()
    {
        var get = await _client.GetFromJsonAsync<NamingConfigDto>(new Uri("http://localhost/api/v1/config/naming"));
        get!.ArtistFolderTemplate.Should().NotBeNullOrEmpty();

        var put = await _client.PutAsJsonAsync(new Uri("http://localhost/api/v1/config/naming"),
            new NamingConfigDto("{Artist Name}", "{Artist Name} - {Title}"));
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await put.Content.ReadFromJsonAsync<NamingConfigDto>();
        updated!.FileTemplate.Should().Be("{Artist Name} - {Title}");
    }

    [Fact]
    public async Task MediaManagement_config_get_and_put_round_trip()
    {
        var get = await _client.GetFromJsonAsync<MediaManagementConfigDto>(new Uri("http://localhost/api/v1/config/mediamanagement"));
        get!.FileOperation.Should().Be("Move");

        var put = await _client.PutAsJsonAsync(new Uri("http://localhost/api/v1/config/mediamanagement"),
            new MediaManagementConfigDto("HardlinkWithFallback", false, '-'));
        var updated = await put.Content.ReadFromJsonAsync<MediaManagementConfigDto>();
        updated!.FileOperation.Should().Be("HardlinkWithFallback");
        updated.ReplaceIllegalCharacters.Should().BeFalse();
        updated.IllegalCharacterReplacement.Should().Be('-');
    }
}
