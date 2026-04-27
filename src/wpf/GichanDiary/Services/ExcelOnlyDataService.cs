using GichanDiary.Models;

namespace GichanDiary.Services;

/// <summary>
/// IDataService implementation that wraps existing IExcelService.
/// Provides 100% backward compatibility — no Firebase dependency.
/// </summary>
public class ExcelOnlyDataService : IDataService
{
    private readonly IExcelService _excelService;
    private readonly ISettingsService _settingsService;

    public event Action<List<BabyEvent>>? EventsChanged;

    public SyncMode CurrentMode => SyncMode.ExcelOnly;
    public bool IsOnline => false;

    public ExcelOnlyDataService(IExcelService excelService, ISettingsService settingsService)
    {
        _excelService = excelService;
        _settingsService = settingsService;
    }

    private string ExcelPath => _settingsService.Load().ExcelFilePath!;

    public Task<List<BabyEvent>> LoadEventsAsync()
        => _excelService.LoadEventsAsync(ExcelPath);

    public async Task AddEventAsync(BabyEvent newEvent)
    {
        await _excelService.AppendEventAsync(ExcelPath, newEvent);
        await NotifyChanged();
    }

    public async Task UpdateEventAsync(BabyEvent updated)
    {
        await _excelService.UpdateEventAsync(ExcelPath, updated);
        await NotifyChanged();
    }

    public async Task DeleteEventAsync(BabyEvent target)
    {
        await _excelService.DeleteEventAsync(ExcelPath, target);
        await NotifyChanged();
    }

    public Task<List<BabyEvent>> ImportEventsAsync(string filePath, ImportMode mode)
        => _excelService.ImportEventsAsync(filePath, mode);

    public Task ExportEventsAsync(string targetPath, List<BabyEvent> events)
        => _excelService.ExportEventsAsync(targetPath, events);

    public Task<string> CreateBackupAsync(string? targetPath = null)
        => _excelService.CreateBackupAsync(ExcelPath, targetPath);

    private async Task NotifyChanged()
    {
        var events = await LoadEventsAsync();
        EventsChanged?.Invoke(events);
    }
}
