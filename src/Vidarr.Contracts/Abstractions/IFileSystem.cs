namespace Vidarr.Contracts.Abstractions;

public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    void DeleteFile(string path);
    void MoveFile(string source, string destination, bool overwrite);
    void CopyFile(string source, string destination, bool overwrite);
    bool TryHardlink(string source, string destination);
    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct);
    Task WriteAllBytesAsync(string path, byte[] contents, CancellationToken ct);
    Task<string> ReadAllTextAsync(string path, CancellationToken ct);
    Task WriteAllTextAsync(string path, string contents, CancellationToken ct);
    IEnumerable<string> EnumerateFiles(string path, string searchPattern, bool recursive);
    IEnumerable<string> EnumerateDirectories(string path);
    long GetFileSize(string path);
    DiskInfo GetDiskInfo(string path);
}

public sealed record DiskInfo(long TotalBytes, long FreeBytes, string DriveLabel);
