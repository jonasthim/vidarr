namespace Vidarr.Backup;

/// <summary>
/// Promotes staged .restore files into their live locations on next startup,
/// after backing up the current files to .pre-restore for safety.
/// </summary>
public static class RestoreBootstrap
{
    public static bool ApplyPendingRestore(string sqlitePath, string? configPath = null)
    {
        var applied = false;
        applied |= TryPromote(sqlitePath + ".restore", sqlitePath);
        if (!string.IsNullOrEmpty(configPath))
        {
            applied |= TryPromote(configPath + ".restore", configPath);
        }
        return applied;
    }

    private static bool TryPromote(string staged, string live)
    {
        if (!File.Exists(staged)) return false;
        if (File.Exists(live))
        {
            var rescue = live + ".pre-restore";
            if (File.Exists(rescue)) File.Delete(rescue);
            File.Move(live, rescue);
        }
        File.Move(staged, live);
        return true;
    }
}
