using Microsoft.Extensions.Logging;
using Vidarr.ChapterSplit;
using Vidarr.Contracts.Models;

namespace Vidarr.Importer;

public sealed record ChapterAwareImportRequest(
    string SourceFile,
    string ArtistName,
    string RootFolderPath,
    /// <summary>Candidate videos (catalog id + title) to match chapter titles against.</summary>
    IReadOnlyList<ChapterMatchCandidate> Candidates,
    Vidarr.Contracts.Models.Quality Quality,
    Vidarr.Contracts.Models.NamingConfig NamingConfig,
    FileOperation FileOperation = FileOperation.Move);

public sealed record ChapterAwareImportResult(
    bool UsedChapterSplit,
    int SuccessfulChapters,
    int UnmatchedChapters,
    int FailedSplits,
    IReadOnlyList<ChapterImportRow> Rows);

public sealed record ChapterImportRow(
    MediaChapter Chapter,
    int? MatchedCandidateId,
    string? MatchedCandidateTitle,
    double? MatchScore,
    string? DestinationPath,
    bool Imported,
    string? FailureReason);

public interface IChapterAwareImportPipeline
{
    Task<ChapterAwareImportResult> ImportAsync(ChapterAwareImportRequest request, CancellationToken ct);
}

public sealed class ChapterAwareImportPipeline : IChapterAwareImportPipeline
{
    private readonly IMediaInspector _inspector;
    private readonly IChapterSplitter _splitter;
    private readonly IChapterTitleMatcher _matcher;
    private readonly IImporterService _importer;
    private readonly ILogger<ChapterAwareImportPipeline> _logger;

    public ChapterAwareImportPipeline(
        IMediaInspector inspector,
        IChapterSplitter splitter,
        IChapterTitleMatcher matcher,
        IImporterService importer,
        ILogger<ChapterAwareImportPipeline> logger)
    {
        _inspector = inspector;
        _splitter = splitter;
        _matcher = matcher;
        _importer = importer;
        _logger = logger;
    }

    public async Task<ChapterAwareImportResult> ImportAsync(ChapterAwareImportRequest request, CancellationToken ct)
    {
        var info = await _inspector.InspectAsync(request.SourceFile, ct);
        if (info is null || info.Chapters.Count < 2)
        {
            return new ChapterAwareImportResult(
                UsedChapterSplit: false,
                SuccessfulChapters: 0,
                UnmatchedChapters: 0,
                FailedSplits: 0,
                Rows: []);
        }

        var assignments = _matcher.Assign(info.Chapters, request.Candidates, request.ArtistName);
        var rows = new List<ChapterImportRow>(assignments.Count);
        var splitTempDir = Path.Combine(Path.GetTempPath(), $"vidarr-split-{Guid.NewGuid():N}");

        var success = 0;
        var unmatched = 0;
        var failedSplits = 0;

        foreach (var (chapter, match) in assignments)
        {
            ct.ThrowIfCancellationRequested();

            if (match is null)
            {
                unmatched++;
                rows.Add(new ChapterImportRow(chapter, null, null, null, null, false, "no matching catalog video"));
                continue;
            }

            var ext = Path.GetExtension(request.SourceFile);
            var splitOut = Path.Combine(splitTempDir, $"chapter-{chapter.Id}{ext}");
            var splitResult = await _splitter.SplitAsync(new ChapterSplitRequest(request.SourceFile, chapter, splitOut), ct);
            if (!splitResult.Success)
            {
                failedSplits++;
                rows.Add(new ChapterImportRow(chapter, match.CandidateId, match.CandidateTitle, match.Score, null, false, splitResult.FailureReason));
                continue;
            }

            var importResult = await _importer.ImportAsync(new ImportRequest(
                SourceFile: splitOut,
                RootFolderPath: request.RootFolderPath,
                ArtistName: request.ArtistName,
                Title: match.CandidateTitle,
                Year: null,
                Quality: request.Quality,
                NamingConfig: request.NamingConfig,
                FileOperation: request.FileOperation), ct);

            if (importResult.Success)
            {
                success++;
                rows.Add(new ChapterImportRow(chapter, match.CandidateId, match.CandidateTitle, match.Score, importResult.DestinationPath, true, null));
            }
            else
            {
                rows.Add(new ChapterImportRow(chapter, match.CandidateId, match.CandidateTitle, match.Score, null, false, importResult.FailureReason));
            }
        }

        _logger.LogInformation(
            "ChapterSplit import: {SourceFile} → {Success} imported, {Unmatched} unmatched, {Failed} failed splits",
            request.SourceFile, success, unmatched, failedSplits);

        return new ChapterAwareImportResult(
            UsedChapterSplit: true,
            SuccessfulChapters: success,
            UnmatchedChapters: unmatched,
            FailedSplits: failedSplits,
            Rows: rows);
    }
}
