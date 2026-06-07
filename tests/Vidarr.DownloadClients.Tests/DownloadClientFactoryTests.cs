using Vidarr.DownloadClients;
using Vidarr.Tests.Common;

namespace Vidarr.DownloadClients.Tests;

public class DownloadClientFactoryTests
{
    private static FakeHttpClient Http() => new();
    private static FakeProcessRunner Procs() => new();
    private static FakeFileSystem Fs() => new();

    [Fact]
    public void QBittorrent_factory_schema_and_create()
    {
        var f = new QBittorrentFactory(Http());
        f.Implementation.Should().Be("QBittorrent");
        f.SettingsSchema.Should().NotBeEmpty();
        var c = f.Create(1, "qbit", "{\"baseUrl\":\"http://host:1\",\"username\":\"u\",\"password\":\"p\",\"category\":\"vidarr\"}");
        c.Name.Should().Be("qbit");
    }

    [Fact]
    public void QBittorrent_factory_falls_back_to_defaults_for_blank_settings()
    {
        var f = new QBittorrentFactory(Http());
        f.Create(2, "qbit-default", "{}").Should().NotBeNull();
    }

    [Fact]
    public void Transmission_factory_create_with_and_without_credentials()
    {
        var f = new TransmissionFactory(Http());
        f.Implementation.Should().Be("Transmission");
        f.Create(1, "tx-auth", "{\"baseUrl\":\"http://h:9\",\"username\":\"u\",\"password\":\"p\",\"downloadDir\":\"/data\"}").Should().NotBeNull();
        f.Create(2, "tx-anon", "{}").Should().NotBeNull();
    }

    [Fact]
    public void Deluge_factory_create()
    {
        var f = new DelugeFactory(Http());
        f.Implementation.Should().Be("Deluge");
        f.Create(1, "deluge", "{\"baseUrl\":\"http://h:8\",\"password\":\"p\",\"category\":\"vidarr\",\"downloadLocation\":\"/d\"}").Should().NotBeNull();
        f.Create(2, "deluge-default", "{}").Should().NotBeNull();
    }

    [Fact]
    public void SABnzbd_factory_create()
    {
        var f = new SABnzbdFactory(Http());
        f.Implementation.Should().Be("SABnzbd");
        f.Create(1, "sab", "{\"baseUrl\":\"http://h:8\",\"apiKey\":\"k\",\"category\":\"vidarr\",\"priority\":1}").Should().NotBeNull();
        f.Create(2, "sab-default", "{}").Should().NotBeNull();
    }

    [Fact]
    public void NZBGet_factory_create()
    {
        var f = new NZBGetFactory(Http());
        f.Implementation.Should().Be("NZBGet");
        f.Create(1, "nzbget", "{\"baseUrl\":\"http://h:6\",\"username\":\"u\",\"password\":\"p\",\"category\":\"vidarr\",\"priority\":50}").Should().NotBeNull();
        f.Create(2, "nzbget-default", "{}").Should().NotBeNull();
    }

    [Fact]
    public void YtDlp_factory_create()
    {
        var f = new YtDlpFactory(Procs(), Fs());
        f.Implementation.Should().Be("YtDlp");
        var c = f.Create(1, "ytdlp", "{\"incompleteFolder\":\"/data/inc\",\"formatSelector\":\"bv*+ba/b\",\"outputContainer\":\"mp4\"}");
        c.Name.Should().Be("ytdlp");
        f.Create(2, "ytdlp-default", "{}").Should().NotBeNull();
    }
}
