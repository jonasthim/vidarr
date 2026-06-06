using System.Collections.Concurrent;
using Vidarr.Contracts.Abstractions;

namespace Vidarr.Tests.Common;

public sealed class FakeFileSystem : IFileSystem
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new(StringComparer.Ordinal);
    private readonly HashSet<string> _directories = new(StringComparer.Ordinal);
    private long _totalBytes = 1_000_000_000_000L;
    private long _freeBytes = 500_000_000_000L;

    public IReadOnlyDictionary<string, byte[]> Files => _files;

    public IReadOnlyCollection<string> Directories => _directories;

    public bool FileExists(string path) => _files.ContainsKey(Normalize(path));

    public bool DirectoryExists(string path) => _directories.Contains(Normalize(path));

    public void CreateDirectory(string path)
    {
        var n = Normalize(path);
        _directories.Add(n);
        var parent = Path.GetDirectoryName(n);
        while (!string.IsNullOrEmpty(parent))
        {
            _directories.Add(parent);
            parent = Path.GetDirectoryName(parent);
        }
    }

    public void DeleteFile(string path) => _files.TryRemove(Normalize(path), out _);

    public void MoveFile(string source, string destination, bool overwrite)
    {
        var s = Normalize(source);
        var d = Normalize(destination);
        if (!_files.TryRemove(s, out var contents))
        {
            throw new FileNotFoundException($"Source not found: {s}");
        }
        if (_files.ContainsKey(d) && !overwrite)
        {
            _files[s] = contents;
            throw new IOException($"Destination exists: {d}");
        }
        EnsureParentExists(d);
        _files[d] = contents;
    }

    public void CopyFile(string source, string destination, bool overwrite)
    {
        var s = Normalize(source);
        var d = Normalize(destination);
        if (!_files.TryGetValue(s, out var contents))
        {
            throw new FileNotFoundException($"Source not found: {s}");
        }
        if (_files.ContainsKey(d) && !overwrite)
        {
            throw new IOException($"Destination exists: {d}");
        }
        EnsureParentExists(d);
        _files[d] = contents;
    }

    public bool TryHardlink(string source, string destination)
    {
        var s = Normalize(source);
        var d = Normalize(destination);
        if (!_files.TryGetValue(s, out var contents))
        {
            return false;
        }
        EnsureParentExists(d);
        _files[d] = contents;
        return true;
    }

    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct)
    {
        var n = Normalize(path);
        if (!_files.TryGetValue(n, out var contents))
        {
            throw new FileNotFoundException($"Not found: {n}");
        }
        return Task.FromResult(contents);
    }

    public Task WriteAllBytesAsync(string path, byte[] contents, CancellationToken ct)
    {
        var n = Normalize(path);
        EnsureParentExists(n);
        _files[n] = contents;
        return Task.CompletedTask;
    }

    public async Task<string> ReadAllTextAsync(string path, CancellationToken ct) =>
        System.Text.Encoding.UTF8.GetString(await ReadAllBytesAsync(path, ct));

    public Task WriteAllTextAsync(string path, string contents, CancellationToken ct) =>
        WriteAllBytesAsync(path, System.Text.Encoding.UTF8.GetBytes(contents), ct);

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, bool recursive)
    {
        var prefix = Normalize(path) + Path.DirectorySeparatorChar;
        var glob = WildcardToRegex(searchPattern);
        foreach (var file in _files.Keys)
        {
            if (!file.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }
            var relative = file[prefix.Length..];
            if (!recursive && relative.Contains(Path.DirectorySeparatorChar))
            {
                continue;
            }
            if (System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(file), glob))
            {
                yield return file;
            }
        }
    }

    public IEnumerable<string> EnumerateDirectories(string path)
    {
        var prefix = Normalize(path) + Path.DirectorySeparatorChar;
        foreach (var dir in _directories)
        {
            if (dir.StartsWith(prefix, StringComparison.Ordinal) &&
                !dir[prefix.Length..].Contains(Path.DirectorySeparatorChar))
            {
                yield return dir;
            }
        }
    }

    public long GetFileSize(string path) => _files.TryGetValue(Normalize(path), out var c) ? c.LongLength : 0;

    public DiskInfo GetDiskInfo(string path) => new(_totalBytes, _freeBytes, "fake-fs");

    public void WriteFakeFile(string path, byte[] contents)
    {
        var n = Normalize(path);
        EnsureParentExists(n);
        _files[n] = contents;
    }

    public void WriteFakeText(string path, string contents) =>
        WriteFakeFile(path, System.Text.Encoding.UTF8.GetBytes(contents));

    public void SetDisk(long totalBytes, long freeBytes)
    {
        _totalBytes = totalBytes;
        _freeBytes = freeBytes;
    }

    private void EnsureParentExists(string path)
    {
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            CreateDirectory(parent);
        }
    }

    private static string Normalize(string path) =>
        Path.GetFullPath(path).Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

    private static string WildcardToRegex(string pattern) =>
        "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
}
