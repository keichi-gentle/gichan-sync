using GichanDiary.Models;

namespace GichanDiary.Services;

public interface IDataService
{
    Task<List<BabyEvent>> LoadEventsAsync();
    Task AddEventAsync(BabyEvent newEvent);
    Task UpdateEventAsync(BabyEvent updated);
    Task DeleteEventAsync(BabyEvent target);

    // Excel-specific operations (delegated to ExcelService)
    Task<List<BabyEvent>> ImportEventsAsync(string filePath, ImportMode mode);
    Task ExportEventsAsync(string targetPath, List<BabyEvent> events);
    Task<string> CreateBackupAsync(string? targetPath = null);

    // Real-time sync notification
    event Action<List<BabyEvent>>? EventsChanged;

    // Sync state
    SyncMode CurrentMode { get; }
    bool IsOnline { get; }
}

public enum SyncMode
{
    ExcelOnly,
    FirebaseSync
}
