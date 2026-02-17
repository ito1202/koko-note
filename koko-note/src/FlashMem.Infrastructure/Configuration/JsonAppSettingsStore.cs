using System.Text.Json;
using FlashMem.Application.Configuration;

namespace FlashMem.Infrastructure.Configuration;

public sealed class JsonAppSettingsStore : IAppSettingsStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public JsonAppSettingsStore(string path)
    {
        _path = path;
    }

    public async Task<AppSettings> LoadAsync(bool isMacOs, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            return AppSettingsDefaults.Create(isMacOs);
        }

        var json = await File.ReadAllTextAsync(_path, cancellationToken);
        var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
            ?? AppSettingsDefaults.Create(isMacOs);
        return AppSettingsDefaults.Sanitize(settings, isMacOs);
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var tempPath = $"{_path}.tmp";
        await File.WriteAllTextAsync(tempPath, json, cancellationToken);
        File.Move(tempPath, _path, true);
    }
}
