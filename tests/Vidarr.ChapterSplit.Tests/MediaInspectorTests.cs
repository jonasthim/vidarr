using Vidarr.ChapterSplit;
using Vidarr.Contracts.Abstractions;
using Vidarr.Tests.Common;

namespace Vidarr.ChapterSplit.Tests;

public class MediaInspectorTests
{
    private const string SampleFfprobeJson = """
    {
      "format": { "format_name": "matroska,webm", "duration": "5400.0", "filename": "concert.mkv" },
      "chapters": [
        { "id": 0, "time_base": "1/1000", "start_time": "0.000", "end_time": "180.000",
          "tags": { "title": "Around the World" } },
        { "id": 1, "time_base": "1/1000", "start_time": "180.000", "end_time": "360.000",
          "tags": { "title": "One More Time" } },
        { "id": 2, "time_base": "1/1000", "start_time": "360.000", "end_time": "540.000" }
      ],
      "streams": [
        { "index": 0, "codec_type": "video", "codec_name": "h264", "tags": { "language": "eng" } },
        { "index": 1, "codec_type": "audio", "codec_name": "aac", "tags": { "language": "eng" } }
      ]
    }
    """;

    [Fact]
    public async Task Inspect_invokes_ffprobe_with_expected_args()
    {
        var runner = new FakeProcessRunner().SetDefault(new ProcessResult(0, SampleFfprobeJson, "", TimeSpan.Zero));
        var sut = new MediaInspector(runner);

        var info = await sut.InspectAsync("/library/concert.mkv", default);

        info.Should().NotBeNull();
        var inv = runner.Invocations.Single();
        inv.Executable.Should().Be("ffprobe");
        inv.Arguments.Should().Contain("-show_chapters").And.Contain("-show_streams").And.Contain("-show_format");
        inv.Arguments.Last().Should().Be("/library/concert.mkv");
    }

    [Fact]
    public void Parse_extracts_duration_format_chapters_streams()
    {
        var info = MediaInspector.Parse(SampleFfprobeJson);
        info.Should().NotBeNull();
        info!.Duration.Should().Be(TimeSpan.FromSeconds(5400));
        info.Container.Should().Be("matroska,webm");
        info.Chapters.Should().HaveCount(3);
        info.Chapters[0].Title.Should().Be("Around the World");
        info.Chapters[0].Start.Should().Be(TimeSpan.Zero);
        info.Chapters[0].End.Should().Be(TimeSpan.FromSeconds(180));
        info.Chapters[0].Duration.Should().Be(TimeSpan.FromSeconds(180));
        info.Chapters[2].Title.Should().BeNull(); // no tags
        info.Streams.Should().HaveCount(2);
        info.Streams[0].CodecType.Should().Be("video");
        info.Streams[1].Language.Should().Be("eng");
    }

    [Fact]
    public void Parse_skips_chapters_with_zero_or_negative_duration()
    {
        const string json = """
        {
          "chapters": [
            { "id": 0, "start_time": "0.0", "end_time": "0.0", "tags": { "title": "x" } },
            { "id": 1, "start_time": "10.0", "end_time": "5.0", "tags": { "title": "backwards" } },
            { "id": 2, "start_time": "0.0", "end_time": "30.0", "tags": { "title": "real" } }
          ]
        }
        """;
        var info = MediaInspector.Parse(json);
        info!.Chapters.Should().ContainSingle().Which.Title.Should().Be("real");
    }

    [Fact]
    public async Task Inspect_returns_null_when_ffprobe_fails()
    {
        var runner = new FakeProcessRunner().SetDefault(new ProcessResult(1, "", "boom", TimeSpan.Zero));
        var sut = new MediaInspector(runner);
        (await sut.InspectAsync("/x", default)).Should().BeNull();
    }

    [Fact]
    public async Task Inspect_returns_null_when_ffprobe_emits_empty_stdout()
    {
        var runner = new FakeProcessRunner().SetDefault(new ProcessResult(0, "", "", TimeSpan.Zero));
        (await new MediaInspector(runner).InspectAsync("/x", default)).Should().BeNull();
    }

    [Fact]
    public void Parse_returns_null_on_malformed_json()
    {
        MediaInspector.Parse("not json").Should().BeNull();
        MediaInspector.Parse("{").Should().BeNull();
    }

    [Fact]
    public void Parse_tolerates_missing_format_chapters_streams_sections()
    {
        var info = MediaInspector.Parse("{}");
        info.Should().NotBeNull();
        info!.Duration.Should().Be(TimeSpan.Zero);
        info.Chapters.Should().BeEmpty();
        info.Streams.Should().BeEmpty();
        info.Container.Should().BeNull();
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("Infinity")]
    [InlineData("-1.5")]
    public void Parse_rejects_unparseable_or_invalid_durations(string raw)
    {
        var json = $$"""{ "format": { "duration": "{{raw}}" } }""";
        var info = MediaInspector.Parse(json);
        info!.Duration.Should().Be(TimeSpan.Zero);
    }
}
