using System.Globalization;

namespace ZFood.Core;

/// <summary>
/// A tiny append-only diagnostics file recording app events (startup, shutdown,
/// settings load/save, corrupt-file recovery, log commits). Diagnostics must
/// never take the app down, so every write failure is swallowed.
/// </summary>
public sealed class DiagnosticsLog
{
    private readonly string? _path;
    private readonly object _gate = new();

    public DiagnosticsLog(string? path) => _path = path;

    /// <summary>A logger that goes nowhere (for tests).</summary>
    public static DiagnosticsLog Null { get; } = new(null);

    /// <summary>Appends one timestamped event line, ignoring all I/O failures.</summary>
    public void Write(string message)
    {
        if (_path is null)
            return;

        try
        {
            var line = DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture)
                       + " " + message + Environment.NewLine;
            lock (_gate)
            {
                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.AppendAllText(_path, line);
            }
        }
        catch
        {
            // Diagnostics are best-effort by design.
        }
    }
}
