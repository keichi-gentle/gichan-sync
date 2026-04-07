using System.Net.Http;
using System.Windows;
using GichanDiary.Models;

namespace GichanDiary.Services;

/// <summary>
/// IDataService that writes to both Firestore and Excel (dual-write).
/// Reads primarily from Firestore, falls back to Excel offline.
/// Polls meta/lastUpdated document every 60 seconds (1 read per poll).
/// Only fetches full events when change is detected.
/// </summary>
public class FirebaseSyncDataService : IDataService
{
    private readonly IExcelService _excelService;
    private readonly ISettingsService _settingsService;
    private readonly FirestoreService _firestoreService;
    private readonly System.Timers.Timer _pollTimer;
    private List<BabyEvent> _cachedEvents = new();
    private bool _isOnline;
    private string? _lastKnownTimestamp;
    private int _consecutiveFailures;

    public event Action<List<BabyEvent>>? EventsChanged;
    public event Action<DateTime>? SyncTimeChanged;
    public SyncMode CurrentMode => SyncMode.FirebaseSync;
    public bool IsOnline => _isOnline;
    public DateTime? LastSyncTime { get; private set; }

    public FirebaseSyncDataService(
        IExcelService excelService,
        ISettingsService settingsService,
        FirestoreService firestoreService)
    {
        _excelService = excelService;
        _settingsService = settingsService;
        _firestoreService = firestoreService;

        _pollTimer = new System.Timers.Timer(60_000); // 60 seconds
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
                UpdateSyncTime();
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
        try
        {
            if (_firestoreService.IsConfigured)
            {
                await _firestoreService.AddEventAsync(newEvent);
                await _firestoreService.TouchLastUpdatedAsync();
                _isOnline = true;
                UpdateSyncTime();
            }
        }
        catch (Exception ex)
        {
            LogService.System($"Firestore add failed: {ex.Message}");
            _isOnline = false;
        }

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
                await _firestoreService.TouchLastUpdatedAsync();
                _isOnline = true;
                UpdateSyncTime();
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
                await _firestoreService.TouchLastUpdatedAsync();
                _isOnline = true;
                UpdateSyncTime();
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

    /// <summary>
    /// 경량 폴링: meta/lastUpdated 문서만 읽어서 변경 여부 확인 (1 read).
    /// 변경 감지 시에만 전체 이벤트를 가져온다.
    /// </summary>
    private async Task PollForChanges()
    {
        try
        {
            if (!_firestoreService.IsConfigured) return;

            var remoteTimestamp = await _firestoreService.GetLastUpdatedTimestampAsync();
            _isOnline = true;
            _consecutiveFailures = 0;
            _pollTimer.Interval = 60_000; // 성공 시 60초로 복귀
            UpdateSyncTime();

            // 타임스탬프가 변경되었으면 전체 이벤트를 가져온다
            if (remoteTimestamp != null && remoteTimestamp != _lastKnownTimestamp)
            {
                _lastKnownTimestamp = remoteTimestamp;
                var latest = await _firestoreService.LoadEventsAsync();
                _cachedEvents = latest;
                Application.Current?.Dispatcher.Invoke(() => EventsChanged?.Invoke(_cachedEvents));
                LogService.System($"Firestore sync: change detected, {latest.Count} events loaded");
            }
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("429"))
        {
            _pollTimer.Interval = 300_000; // 429: 5분 대기
            _consecutiveFailures++;
            LogService.System($"Firestore rate limited (429), backing off 5min (retry {_consecutiveFailures})");
            _isOnline = false;
        }
        catch (Exception ex)
        {
            _consecutiveFailures++;
            var backoffMs = Math.Min(120_000, 60_000 * (int)Math.Pow(2, _consecutiveFailures - 1));
            _pollTimer.Interval = backoffMs;
            LogService.System($"Firestore poll failed (retry {_consecutiveFailures}, next {backoffMs / 1000}s): {ex.Message}");
            _isOnline = false;
        }
    }

    private void UpdateSyncTime()
    {
        LastSyncTime = DateTime.Now;
        Application.Current?.Dispatcher.Invoke(() => SyncTimeChanged?.Invoke(LastSyncTime.Value));
    }

    private async Task NotifyChanged()
    {
        _cachedEvents = await LoadEventsAsync();
        Application.Current?.Dispatcher.Invoke(() => EventsChanged?.Invoke(_cachedEvents));
    }
}
