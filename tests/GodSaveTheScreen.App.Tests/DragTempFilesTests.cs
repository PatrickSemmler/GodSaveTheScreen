using System.IO;
using GodSaveTheScreen.App.Services;

namespace GodSaveTheScreen.App.Tests;

public sealed class DragTempFilesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gsts-drag-tests-" + Guid.NewGuid().ToString("N"));

    public DragTempFilesTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Create(string name, DateTime lastWrite)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, "x");
        File.SetLastWriteTime(path, lastWrite);
        return path;
    }

    [Fact]
    public void Cleanup_LoeschtNurAltePngDateien()
    {
        var now = new DateTime(2026, 10, 19, 12, 0, 0);
        var old = Create("alt.png", now.AddDays(-3));
        var fresh = Create("neu.png", now.AddHours(-1));
        var other = Create("alt.txt", now.AddDays(-3));

        var deleted = DragTempFiles.Cleanup(_dir, DragTempFiles.MaxAge, now);

        deleted.ShouldBe(1);
        File.Exists(old).ShouldBeFalse();
        File.Exists(fresh).ShouldBeTrue();
        File.Exists(other).ShouldBeTrue();
    }

    [Fact]
    public void Cleanup_OrdnerFehlt_LiefertNull()
    {
        DragTempFiles.Cleanup(Path.Combine(_dir, "gibt-es-nicht"), DragTempFiles.MaxAge, DateTime.Now).ShouldBe(0);
    }

    [Fact]
    public void Cleanup_DateiGesperrt_WirftNichtUndZaehltNicht()
    {
        var now = new DateTime(2026, 10, 19, 12, 0, 0);
        var path = Create("gesperrt.png", now.AddDays(-3));

        int deleted;
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            deleted = DragTempFiles.Cleanup(_dir, DragTempFiles.MaxAge, now);
        }

        deleted.ShouldBe(0);
        File.Exists(path).ShouldBeTrue();
    }
}
