using ZFood.Core;

namespace ZFood.Tests;

public class DiagnosticsLogTests
{
    [Fact]
    public void Writes_timestamped_event_lines()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "diagnostics.log");
        var log = new DiagnosticsLog(path);

        log.Write("startup");
        log.Write("log commit: Portion 496 cal");

        var lines = File.ReadAllLines(path);
        Assert.Equal(2, lines.Length);
        Assert.EndsWith("startup", lines[0]);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}", lines[0]);
        Assert.Contains("log commit", lines[1]);
    }

    [Fact]
    public void Creates_the_directory_when_missing()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "nested", "diagnostics.log");
        new DiagnosticsLog(path).Write("startup");
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Write_failures_are_swallowed()
    {
        var log = new DiagnosticsLog("/proc/zfood-cannot-write-here/diagnostics.log");
        log.Write("startup"); // must not throw
    }

    [Fact]
    public void Null_logger_writes_nowhere_and_never_throws()
    {
        DiagnosticsLog.Null.Write("anything");
    }
}
