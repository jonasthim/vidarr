using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Vidarr.Contracts.Abstractions;

namespace Vidarr.Infrastructure;

[ExcludeFromCodeCoverage(Justification = "Boundary adapter; exercised by integration tests touching the real file system.")]
public sealed class FileSystemAdapter : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public void DeleteFile(string path) => File.Delete(path);

    public void MoveFile(string source, string destination, bool overwrite) =>
        File.Move(source, destination, overwrite);

    public void CopyFile(string source, string destination, bool overwrite) =>
        File.Copy(source, destination, overwrite);

    public bool TryHardlink(string source, string destination)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return CreateHardLink(destination, source, IntPtr.Zero);
            }

            return link(source, destination) == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct) =>
        File.ReadAllBytesAsync(path, ct);

    public Task WriteAllBytesAsync(string path, byte[] contents, CancellationToken ct) =>
        File.WriteAllBytesAsync(path, contents, ct);

    public Task<string> ReadAllTextAsync(string path, CancellationToken ct) =>
        File.ReadAllTextAsync(path, ct);

    public Task WriteAllTextAsync(string path, string contents, CancellationToken ct) =>
        File.WriteAllTextAsync(path, contents, ct);

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, bool recursive) =>
        Directory.EnumerateFiles(
            path,
            searchPattern,
            recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

    public IEnumerable<string> EnumerateDirectories(string path) =>
        Directory.EnumerateDirectories(path);

    public long GetFileSize(string path) => new FileInfo(path).Length;

    public DiskInfo GetDiskInfo(string path)
    {
        var info = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path)) ?? "/");
        return new DiskInfo(info.TotalSize, info.AvailableFreeSpace, info.Name);
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    [DllImport("libc", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern int link([MarshalAs(UnmanagedType.LPStr)] string oldpath, [MarshalAs(UnmanagedType.LPStr)] string newpath);
}
