using System.Windows;
using GichanDiary.Models;

namespace GichanDiary.Services;

/// <summary>
/// IDataService that writes to both Firestore and Excel (dual-write).
/// Reads primarily from Firestore, falls back to Excel offline.
/// Polls Firestore every 10 seconds for external changes.
/// </summary>
public class FirebaseSyncDataService : IDataService
{
    private readonly IExcelService _excelService;
    private readonly ISettingsService _settingsService;
    private readonly FirestoreService _firestoreService;
    private readonly System.Timers.Timer _pollTimer;
    private List<BabyEvent> _cachedEvents = new();
    private bool _isOnline;

    public event Action<List<BabyEvent>>? EventsChanged;
    public SyncMode CurrentMode => SyncMode.FirebaseSync;
    public bool IsOnline => _isOnline;

    public FirebaseSyncDataService(
        IExcelService excelService,
        ISettingsService settingsService,
        FirestoreService firestoreService)
    {
        _excelService = excelService;
        _settingsService = settingsService;
        _firestoreService = firestoreService;

        _pollTimer = new System.Timers.Timer(10_000); // 10 seconds
        _pollTimer.Elapsed += async (s, e) => await PollForChanges();
        _pollTimer.AutoReset = true;
    }

    public void StartPolling() => _pollTimer.Start();
    public void StopPolling() => _pollTimer.Stop();

    private string ExcelPath => _settingsService.Load().ExcelFilePath;

    public async Task<List<BabyEvent>> LoadEventsAsync()
    {
        try
        {
            if (_firestoreService.IsConfigured)
            {
                _cachedEvents = await _firestoreService.LoadEventsAsync();
                _isOnline = true;
                return _cachedEvents;
            }
        }
        catch (Exception ex)
        {
            LogService.System($"Firestore load failed, falling back to Excel: {ex.Message}");
            _isOnline = false;
        }

        // Fallback to Excel
        return await _excelService.LoadEventsAsync(ExcelPath);
    }

    public async Task AddEventAsync(BabyEvent newEvent)
    {
        // Firestore first
        try
        {
            if (_firestoreService.IsConfigured)
            {
                await _firestoreService.AddEventAsync(newEvent);
                _isOnline = true;
            }
        }
        catch (Exception ex)
        {
            LogService.System($"Firestore add failed: {ex.Message}");
            _isOnline = false;
        }

        // Excel backup always
        await _excelService.AppendEventAsync(ExcelPath, newEvent);
        await NotifyChanged();
    }

    public async Task UpdateEventAsync(BabyEvent updated)
    {
        try
        {
            if (_firestoreService.IsConfigured)
            {
                await _firestoreService.UpdateEventAsync(updated);
                _isOnline = true;
            }
        }
        catch (Exception ex)
        {
            LogService.System($"Firestore update failed: {ex.Message}");
            _isOnline = false;
        }

        await _excelService.UpdateEventAsync(ExcelPath, updated);
        await NotifyChanged();
    }

    public async Task DeleteEventAsync(BabyEvent target)
    {
        try
        {
            if (_firestoreService.IsConfigured)
            {
                await _firestoreService.DeleteEventAsync(target.Id.ToString());
                _isOnline = true;
            }
        }
        catch (Exception ex)
        {
            LogService.System($"Firestore delete failed: {ex.Message}");
            _isOnline = false;
        }

        await _excelService.DeleteEventAsync(ExcelPath, target);
        await NotifyChanged();
    }

    public Task<List<BabyEvent>> ImportEventsAsync(string filePath, ImportMode mode)
        => _excelService.ImportEventsAsync(filePath, mode);

    public Task ExportEventsAsync(string targetPath, List<BabyEvent> events)
        => _excelService.ExportEventsAsync(targetPath, events);

    public Task<string> CreateBackupAsync(string? targetPath = null)
        => _excelService.CreateBackupAsync(ExcelPath, targetPath);

    private async Task PollForChanges()
    {
        try
        {
            if (!_firestoreService.IsConfigured) return;

            var latest = await _firestoreService.LoadEventsAsync();
            if (latest.Count != _cachedEvents.Count)
            {
                _cachedEvents = latest;
                _isOnline = true;
                Application.Current?.Dispatcher.Invoke(() => EventsChanged?.Invoke(_cachedEvents));
            }
        }
        catch
        {
            _isOnline = false;
        }
    }

    private async Task NotifyChanged()
    {
        _cachedEvents = await LoadEventsAsync();
        EventsChanged?.Invoke(_cachedEvents);
    }
}
