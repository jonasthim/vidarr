using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Models;

namespace Vidarr.DownloadClients;

public sealed partial class YtDlpDownloadClient : IDownloadClient
{
    private const string YtDlpExecutable = "yt-dlp";

    private readonly IProcessRunner _processes;
    private readonly IFileSystem _fileSystem;
    private readonly YtDlpDownloadClientSettings _settings;
    private readonly ConcurrentDictionary<string, YtDlpDownload> _downloads = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Task> _activeTasks = new(StringComparer.Ordinal);

    public YtDlpDownloadClient(int id, string name, YtDlpDownloadClientSettings settings, IProcessRunner processes, IFileSystem fileSystem)
    {
        Id = id;
        Name = name;
        _settings = settings;
        _processes = processes;
        _fileSystem = fileSystem;
    }

    public int Id { get; }
    public string Name { get; }
    public DownloadProtocol Protocol => DownloadProtocol.Streaming;

    public Task<DownloadClientItemId> DownloadAsync(RemoteRelease release, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("N");
        var outputDir = Path.Combine(_settings.IncompleteFolder, id);
        _fileSystem.CreateDirectory(outputDir);

        var record = new YtDlpDownload(id, release.Info.Title, release.Info.SizeBytes, outputDir);
        _downloads[id] = record;
        _activeTasks[id] = RunDownloadInternalAsync(record, release.Info.SourceUrl, ct);
        return Task.FromResult(new DownloadClientItemId(id));
    }

    public async Task RunDownloadInternalAsync(YtDlpDownload record, Uri sourceUrl, CancellationToken ct)
    {
        var args = new List<string>
        {
            "--no-warnings",
            "--ignore-config",
            "--newline",
            "-f", _settings.FormatSelector,
            "--merge-output-format", _settings.OutputContainer,
            "--no-playlist",
            "-o", Path.Combine(record.OutputDir, "%(title)s.%(ext)s"),
            sourceUrl.AbsoluteUri,
        };
        var invocation = new ProcessInvocation(YtDlpExecutable, args, Timeout: _settings.Timeout);

        try
        {
            var result = await _processes.RunStreamingAsync(invocation, (line, _) =>
            {
                UpdateProgressFromLine(record, line);
                return Task.CompletedTask;
            }, null, ct);
            FinaliseRecord(record, result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            record.Status = DownloadItemStatus.Failed;
            record.Message = ex.Message;
        }
    }

    public Task WaitForCompletionAsync(DownloadClientItemId id) =>
        _activeTasks.TryGetValue(id.Value, out var t) ? t : Task.CompletedTask;

    public Task<IReadOnlyList<DownloadClientItem>> GetItemsAsync(CancellationToken ct)
    {
        IReadOnlyList<DownloadClientItem> items = [.. _downloads.Values.Select(d => new DownloadClientItem(
            Id: new DownloadClientItemId(d.Id),
            Title: d.Title,
            TotalBytes: d.TotalBytes,
            RemainingBytes: d.RemainingBytes,
            Status: d.Status,
            OutputPath: d.Status == DownloadItemStatus.CompletedReadyToImport ? d.OutputPath : null,
            Eta: d.Eta,
            Message: d.Message))];
        return Task.FromResult(items);
    }

    public Task RemoveAsync(DownloadClientItemId id, bool deleteData, CancellationToken ct)
    {
        if (_downloads.TryRemove(id.Value, out var record) && deleteData && _fileSystem.DirectoryExists(record.OutputDir))
        {
            foreach (var file in _fileSystem.EnumerateFiles(record.OutputDir, "*", recursive: true))
            {
                _fileSystem.DeleteFile(file);
            }
        }
        return Task.CompletedTask;
    }

    public async Task<DownloadClientTestResult> TestAsync(CancellationToken ct)
    {
        var result = await _processes.RunAsync(new ProcessInvocation(YtDlpExecutable, ["--version"], Timeout: _settings.Timeout), ct);
        return result.ExitCode == 0
            ? new DownloadClientTestResult(true, $"yt-dlp {result.StdOut.Trim()}")
            : new DownloadClientTestResult(false, result.StdErr.Trim());
    }

    public static void UpdateProgressFromLine(YtDlpDownload record, string line)
    {
        var match = ProgressRegex().Match(line);
        if (!match.Success)
        {
            return;
        }

        if (double.TryParse(match.Groups["pct"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var pct))
        {
            record.ProgressPercent = pct;
            if (record.TotalBytes is { } total)
            {
                record.RemainingBytes = (long)Math.Max(0, total * (1 - pct / 100.0));
            }
            else if (TryParseSize(match.Groups["size"].Value, out var size))
            {
                record.TotalBytes = size;
                record.RemainingBytes = (long)Math.Max(0, size * (1 - pct / 100.0));
            }
        }

        if (match.Groups["eta"].Success)
        {
            record.Eta = ParseEta(match.Groups["eta"].Value);
        }

        record.Status = pct >= 100 ? DownloadItemStatus.CompletedReadyToImport : DownloadItemStatus.Downloading;
    }

    private static void FinaliseRecord(YtDlpDownload record, ProcessResult result)
    {
        if (result.ExitCode == 0)
        {
            record.Status = DownloadItemStatus.CompletedReadyToImport;
            record.OutputPath = record.OutputDir;
            record.RemainingBytes = 0;
            record.ProgressPercent = 100;
        }
        else
        {
            record.Status = DownloadItemStatus.Failed;
            record.Message = string.IsNullOrEmpty(result.StdErr) ? "yt-dlp exited non-zero" : result.StdErr;
        }
    }

    private static TimeSpan? ParseEta(string raw)
    {
        if (string.IsNullOrEmpty(raw) || raw == "Unknown")
        {
            return null;
        }
        var parts = raw.Split(':');
        try
        {
            return parts.Length switch
            {
                1 => TimeSpan.FromSeconds(int.Parse(parts[0], CultureInfo.InvariantCulture)),
                2 => new TimeSpan(0, int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture)),
                3 => new TimeSpan(int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture), int.Parse(parts[2], CultureInfo.InvariantCulture)),
                _ => null,
            };
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static bool TryParseSize(string raw, out long bytes)
    {
        bytes = 0;
        var m = SizeRegex().Match(raw);
        // raw is already a substring matched by ProgressRegex's size group, so the
        // inner regex always succeeds — but we still need to extract value + unit.
        var value = double.Parse(m.Groups["v"].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
        var unit = m.Groups["u"].Value.ToUpperInvariant();
        var multiplier = unit switch
        {
            "K" or "KIB" => 1024.0,
            "M" or "MIB" => 1024.0 * 1024,
            "G" or "GIB" => 1024.0 * 1024 * 1024,
            "T" or "TIB" => 1024.0 * 1024 * 1024 * 1024,
            _ => 1.0,
        };
        bytes = (long)(value * multiplier);
        return true;
    }

    [GeneratedRegex(@"\[download\]\s+(?<pct>\d+(?:\.\d+)?)%\s+of\s+~?\s*(?<size>[\d\.]+\s*(?:[KMGT]i?B)?)?\s*(?:at\s+\S+)?(?:\s+ETA\s+(?<eta>[\d:]+|Unknown))?", RegexOptions.Compiled)]
    private static partial Regex ProgressRegex();

    [GeneratedRegex(@"(?<v>\d+(?:\.\d+)?)\s*(?<u>[KMGT]i?B)?", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex SizeRegex();
}

public sealed record YtDlpDownloadClientSettings(
    string IncompleteFolder,
    string FormatSelector = "bv*+ba/b",
    string OutputContainer = "mkv",
    TimeSpan? Timeout = null);

public sealed class YtDlpDownload
{
    public YtDlpDownload(string id, string title, long? totalBytes, string outputDir)
    {
        Id = id;
        Title = title;
        TotalBytes = totalBytes;
        OutputDir = outputDir;
        Status = DownloadItemStatus.Queued;
    }

    public string Id { get; }
    public string Title { get; }
    public string OutputDir { get; }
    public string? OutputPath { get; set; }
    public long? TotalBytes { get; set; }
    public long? RemainingBytes { get; set; }
    public double ProgressPercent { get; set; }
    public TimeSpan? Eta { get; set; }
    public DownloadItemStatus Status { get; set; }
    public string? Message { get; set; }
}
