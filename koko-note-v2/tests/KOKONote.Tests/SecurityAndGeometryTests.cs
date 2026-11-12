using KOKONote.Core;

namespace KOKONote.Tests;

public class SecurityAndGeometryTests
{
    [WindowsFact]
    public void DpapiRoundTripAndTamperAndBinding()
    {
        var p = new DpapiProtector(); var id = Guid.NewGuid();
        var encrypted = p.Protect("日本語😀", id, "content");
        Assert.Equal("日本語😀", p.Unprotect(encrypted, id, "content"));
        Assert.Throws<StorageException>(() => p.Unprotect(encrypted, Guid.NewGuid(), "content"));
        Assert.Throws<StorageException>(() => p.Unprotect(encrypted, id, "title"));
        encrypted[encrypted.Length / 2] ^= 0x40;
        Assert.Throws<StorageException>(() => p.Unprotect(encrypted, id, "content"));
    }

    [WindowsFact]
    public async Task RealDpapiDatabaseRoundTrip()
    {
        using var db = new TestDatabase(new DpapiProtector()); var w = new Workspace(db.Store.Load());
        var n = w.Create(); w.Edit(n.Id, title: "タイトル😀", content: "DPAPI 日本語の本文");
        await new SaveCoordinator(w, db.Store).SaveAsync();
        Assert.Equal(w.Notes, db.Store.Load().Notes);
    }

    [Theory]
    [InlineData(50000, 50000, 800, 600)]
    [InlineData(-50000, -50000, 800, 600)]
    [InlineData(0, 0, int.MaxValue, int.MaxValue)]
    public void OffscreenAndOversizedWindowsRestoreInsideWorkArea(int x, int y, int width, int height)
    {
        var result = WindowGeometry.Restore(new(x, y, width, height), [new(0, 0, 1920, 1040)]);
        Assert.InRange(result.X, 0, 1920 - result.Width);
        Assert.InRange(result.Y, 0, 1040 - result.Height);
    }
    [Fact]
    public void NegativeMonitorCoordinatesArePreserved()
    {
        var result = WindowGeometry.Restore(new(-1200, 10, 800, 600), [new(0, 0, 1920, 1040), new(-1280, 0, 1280, 1000)]);
        Assert.Equal(-1200, result.X);
    }
}
