using System.IO;
using System.Text.Json;
using GichanDiary.Models;
namespace GichanDiary.Services;

public class SettingsService : ISettingsService
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public SettingsService(string appDirectory)
    {
        _filePath = Path.Combine(appDirectory, "appsettings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_filePath)) return new AppSettings();
        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOpts) ?? new AppSettings();
        }
        catch
        {
            // JSON 파싱 실패 시 기본값 반환 (기존 파일은 다음 Save 시 덮어씀)
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOpts);
        File.WriteAllText(_filePath, json);
    }
}
