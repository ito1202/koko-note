using FlashMem.Application.Configuration;
using FlashMem.Infrastructure.Configuration;

namespace FlashMem.Infrastructure.Tests;

public sealed class JsonAppSettingsStoreTests
{
    [Fact]
    public async Task LoadAsync_Returns_Default_When_File_Does_Not_Exist()
    {
        var tempDir = Directory.CreateTempSubdirectory("flashmem-settings-tests-");
        try
        {
            var path = Path.Combine(tempDir.FullName, "settings.json");
            var store = new JsonAppSettingsStore(path);

            var settings = await store.LoadAsync(isMacOs: true);

            Assert.Equal(HotkeyTapKey.Shift, settings.HotkeyTapKey);
        }
        finally
        {
            tempDir.Delete(true);
        }
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrips_Settings()
    {
        var tempDir = Directory.CreateTempSubdirectory("flashmem-settings-tests-");
        try
        {
            var path = Path.Combine(tempDir.FullName, "settings.json");
            var store = new JsonAppSettingsStore(path);
            var original = new AppSettings(HotkeyTapKey.Command, 420, false);

            await store.SaveAsync(original);
            var loaded = await store.LoadAsync(isMacOs: true);

            Assert.Equal(original, loaded);
        }
        finally
        {
            tempDir.Delete(true);
        }
    }
}
