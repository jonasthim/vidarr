using Microsoft.Extensions.Logging.Abstractions;
using Vidarr.ChapterSplit;
using Vidarr.Contracts.Models;
using Vidarr.Importer;
using Vidarr.Naming;
using Vidarr.Tests.Common;

namespace Vidarr.Importer.Tests;

public class ChapterAwareImportPipelineTests
{
    private static MediaInfo InfoWithChapters(int count)
    {
        var chapters = Enumerable.Range(0, count).Select(i =>
            new MediaChapter(
                Id: i,
                Start: TimeSpan.FromMinutes(i * 3),
                End: TimeSpan.FromMinutes((i + 1) * 3),
                Title: $"Chapter {i}")).ToList();
        return new MediaInfo(TimeSpan.FromMinutes(count * 3), chapters, [], "matroska");
    }

    private static ChapterAwareImportRequest BuildRequest(params ChapterMatchCandidate[] candidates) =>
        new(
            SourceFile: "/library/concert.mkv",
            ArtistName: "Daft Punk",
            RootFolderPath: "/library",
            Candidates: candidates,
            Quality: Quality.Webdl1080p,
            NamingConfig: NamingConfig.Default,
            FileOperation: FileOperation.Move);

    private sealed class StubInspector : IMediaInspector
    {
        public MediaInfo? Response { get; set; }
        public Task<MediaInfo?> InspectAsync(string filePath, CancellationToken ct) => Task.FromResult(Response);
    }

    private sealed class StubSplitter : IChapterSplitter
    {
        public Func<ChapterSplitRequest, ChapterSplitResult>? Override { get; set; }
        public List<ChapterSplitRequest> Calls { get; } = [];
        public Task<ChapterSplitResult> SplitAsync(ChapterSplitRequest request, CancellationToken ct)
        {
            Calls.Add(request);
            return Task.FromResult(Override?.Invoke(request)
                ?? new ChapterSplitResult(request.Chapter, request.OutputPath, true, null));
        }
    }

    private sealed class StubImporter : IImporterService
    {
        public List<ImportRequest> Calls { get; } = [];
        public Func<ImportRequest, ImportResult>? Override { get; set; }
        public Task<ImportResult> ImportAsync(ImportRequest request, CancellationToken ct)
        {
            Calls.Add(request);
            return Task.FromResult(Override?.Invoke(request)
                ?? new ImportResult(true, $"/library/{Guid.NewGuid():N}.mkv", null, 1024, FileOperation.Move));
        }
    }

    private static ChapterAwareImportPipeline Build(
        StubInspector inspector,
        StubSplitter splitter,
        StubImporter importer)
    {
        return new ChapterAwareImportPipeline(
            inspector,
            splitter,
            new ChapterTitleMatcher(),
            importer,
            NullLogger<ChapterAwareImportPipeline>.Instance);
    }

    [Fact]
    public async Task Pipeline_skips_when_inspector_returns_null()
    {
        var inspector = new StubInspector { Response = null };
        var splitter = new StubSplitter();
        var importer = new StubImporter();
        var sut = Build(inspector, splitter, importer);

        var result = await sut.ImportAsync(BuildRequest(), default);

        result.UsedChapterSplit.Should().BeFalse();
        splitter.Calls.Should().BeEmpty();
        importer.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Pipeline_skips_when_only_one_chapter_present()
    {
        var inspector = new StubInspector { Response = InfoWithChapters(1) };
        var splitter = new StubSplitter();
        var importer = new StubImporter();
        var sut = Build(inspector, splitter, importer);

        var result = await sut.ImportAsync(BuildRequest(), default);

        result.UsedChapterSplit.Should().BeFalse();
        splitter.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Pipeline_splits_and_imports_matched_chapters_only()
    {
        var inspector = new StubInspector { Response = InfoWithChapters(3) };
        var splitter = new StubSplitter();
        var importer = new StubImporter();
        var sut = Build(inspector, splitter, importer);

        // Only chapters 0 and 1 have a matching candidate; chapter 2 is unmatched.
        var result = await sut.ImportAsync(
            BuildRequest(
                new ChapterMatchCandidate(100, "Chapter 0"),
                new ChapterMatchCandidate(101, "Chapter 1")),
            default);

        result.UsedChapterSplit.Should().BeTrue();
        result.SuccessfulChapters.Should().Be(2);
        result.UnmatchedChapters.Should().Be(1);
        result.FailedSplits.Should().Be(0);
        splitter.Calls.Should().HaveCount(2); // only the two matched chapters split
        importer.Calls.Should().HaveCount(2);
        importer.Calls.All(c => c.Title.StartsWith("Chapter ", StringComparison.Ordinal)).Should().BeTrue();
    }

    [Fact]
    public async Task Pipeline_records_failed_split_and_continues()
    {
        var inspector = new StubInspector { Response = InfoWithChapters(2) };
        var splitter = new StubSplitter
        {
            Override = req => req.Chapter.Id == 0
                ? new ChapterSplitResult(req.Chapter, req.OutputPath, false, "boom")
                : new ChapterSplitResult(req.Chapter, req.OutputPath, true, null),
        };
        var importer = new StubImporter();
        var sut = Build(inspector, splitter, importer);

        var result = await sut.ImportAsync(
            BuildRequest(
                new ChapterMatchCandidate(100, "Chapter 0"),
                new ChapterMatchCandidate(101, "Chapter 1")),
            default);

        result.FailedSplits.Should().Be(1);
        result.SuccessfulChapters.Should().Be(1);
        importer.Calls.Should().ContainSingle(); // only the chapter that successfully split
        result.Rows.Should().Contain(r => r.Chapter.Id == 0 && r.FailureReason == "boom");
    }

    [Fact]
    public async Task Pipeline_records_failed_import_with_reason()
    {
        var inspector = new StubInspector { Response = InfoWithChapters(1) };
        // single-chapter media isn't split — bump to 2 chapters
        inspector.Response = InfoWithChapters(2);
        var splitter = new StubSplitter();
        var importer = new StubImporter
        {
            Override = _ => new ImportResult(false, null, "destination unwritable", 0, FileOperation.Move),
        };
        var sut = Build(inspector, splitter, importer);

        var result = await sut.ImportAsync(
            BuildRequest(
                new ChapterMatchCandidate(100, "Chapter 0"),
                new ChapterMatchCandidate(101, "Chapter 1")),
            default);

        result.SuccessfulChapters.Should().Be(0);
        result.Rows.Where(r => r.MatchedCandidateId is not null)
            .All(r => r.FailureReason == "destination unwritable").Should().BeTrue();
    }

    [Fact]
    public async Task Pipeline_emits_per_chapter_rows_with_match_score()
    {
        var inspector = new StubInspector { Response = InfoWithChapters(2) };
        var splitter = new StubSplitter();
        var importer = new StubImporter();
        var sut = Build(inspector, splitter, importer);

        var result = await sut.ImportAsync(
            BuildRequest(
                new ChapterMatchCandidate(100, "Chapter 0"),
                new ChapterMatchCandidate(101, "Chapter 1")),
            default);

        result.Rows.Should().HaveCount(2);
        result.Rows.Should().AllSatisfy(r =>
        {
            r.MatchScore.Should().NotBeNull();
            r.MatchedCandidateTitle.Should().NotBeNullOrEmpty();
        });
    }
}
