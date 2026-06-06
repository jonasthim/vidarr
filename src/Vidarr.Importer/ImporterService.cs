using System.Diagnostics.CodeAnalysis;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Models;
using Vidarr.Naming;

namespace Vidarr.Importer;

public interface IImporterService
{
    Task<ImportResult> ImportAsync(ImportRequest request, CancellationToken ct);
}

public sealed record ImportRequest(
    string SourceFile,
    string RootFolderPath,
    string ArtistName,
    string Title,
    int? Year,
    Quality Quality,
    NamingConfig NamingConfig,
    FileOperation FileOperation = FileOperation.Move,
    string? SourceLabel = null);

public enum FileOperation
{
    Move = 0,
    Copy = 1,
    HardlinkWithFallback = 2,
}

[ExcludeFromCodeCoverage(Justification = "Plain data record; exercised via integration tests.")]
public sealed record ImportResult(
    bool Success,
    string? DestinationPath,
    string? FailureReason,
    long SizeBytes,
    FileOperation OperationPerformed);

public sealed class ImporterService : IImporterService
{
    private readonly IFileSystem _fileSystem;
    private readonly INamingService _naming;

    public ImporterService(IFileSystem fileSystem, INamingService naming)
    {
        _fileSystem = fileSystem;
        _naming = naming;
    }

    public Task<ImportResult> ImportAsync(ImportRequest request, CancellationToken ct)
    {
        if (!_fileSystem.FileExists(request.SourceFile))
        {
            return Task.FromResult(new ImportResult(false, null, $"Source file '{request.SourceFile}' not found", 0, request.FileOperation));
        }

        var extension = Path.GetExtension(request.SourceFile);
        var namingInput = new NamingInput(
            ArtistName: request.ArtistName,
            Title: request.Title,
            Year: request.Year,
            Quality: request.Quality,
            Extension: extension,
            ExtraTokens: request.SourceLabel is null ? null : new Dictionary<string, string> { ["Source"] = request.SourceLabel });
        var relativePath = _naming.BuildRelativePath(namingInput, request.NamingConfig);
        var destination = Path.Combine(request.RootFolderPath, relativePath);

        var destDir = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(destDir))
        {
            _fileSystem.CreateDirectory(destDir);
        }

        var operation = request.FileOperation;
        try
        {
            switch (operation)
            {
                case FileOperation.Move:
                    _fileSystem.MoveFile(request.SourceFile, destination, overwrite: true);
                    break;
                case FileOperation.Copy:
                    _fileSystem.CopyFile(request.SourceFile, destination, overwrite: true);
                    break;
                case FileOperation.HardlinkWithFallback:
                    if (!_fileSystem.TryHardlink(request.SourceFile, destination))
                    {
                        _fileSystem.CopyFile(request.SourceFile, destination, overwrite: true);
                        operation = FileOperation.Copy;
                    }
                    break;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(new ImportResult(false, null, ex.Message, 0, request.FileOperation));
        }

        var size = _fileSystem.GetFileSize(destination);
        return Task.FromResult(new ImportResult(true, destination, null, size, operation));
    }
}
