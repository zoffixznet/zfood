namespace ZFood.Core;

/// <summary>Crash-safe file helpers: atomic writes and move-aside recovery.</summary>
public static class AtomicFile
{
    /// <summary>
    /// Writes contents to a temp file in the same directory, then renames it
    /// over the target, so the target is never observed half-written.
    /// </summary>
    public static void Write(string path, string contents)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var temp = path + ".tmp";
        File.WriteAllText(temp, contents);
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>
    /// Moves a corrupt file aside to "path.bak" (replacing any previous .bak)
    /// so a fresh default can take its place. Returns the .bak path, or null
    /// when the file did not exist or could not be moved.
    /// </summary>
    public static string? MoveAside(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;
            var bak = path + ".bak";
            File.Move(path, bak, overwrite: true);
            return bak;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
