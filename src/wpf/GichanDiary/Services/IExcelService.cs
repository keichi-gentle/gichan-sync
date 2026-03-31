using GichanDiary.Models;

namespace GichanDiary.Services;

public interface IExcelService
{
    Task<List<BabyEvent>> LoadEventsAsync(string filePath);
    bool IsFileLocked(string filePath);
    Task AppendEventAsync(string filePath, BabyEvent newEvent);
    Task UpdateEventAsync(string filePath, BabyEvent updated);
    Task DeleteEventAsync(string filePath, BabyEvent target);
    Task<List<BabyEvent>> ImportEventsAsync(string filePath, ImportMode mode);
    Task ExportEventsAsync(string targetPath, List<BabyEvent> events);
    Task<string> CreateBackupAsync(string filePath, string? targetPath = null);
    Task CreateAutoBackupIfNeededAsync(string filePath);
    Task CreateNewFileAsync(string filePath);
}
