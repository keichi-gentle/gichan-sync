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
    private List<BabyEvent> _cachedEvents = new();
    private bool _isOnline;

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

    }

    public void InvalidateCache() => _cachedEvents = new();

    /// <summary>
    /// 캐시 비우고 Firestore에서 다시 로드 후 UI 통지.
    /// 내려받기 후 대시보드/전광판 즉시 반영용.
    /// </summary>
    public async Task ForceReloadAndNotifyAsync()
    {
        _cachedEvents = new();
        await LoadEventsAsync();
        NotifyUI();
    }

    private string ExcelPath => _settingsService.Load().ExcelFilePath;

    public async Task<List<BabyEvent>> LoadEventsAsync()
    {
        // 캐시에 데이터가 있으면 캐시 반환 (Import/CUD 후 즉시 반영)
        if (_cachedEvents.Count > 0)
            return _cachedEvents;

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
        _cachedEvents.Add(newEvent);
        _cachedEvents = _cachedEvents.OrderBy(e => e.FullDateTime).ToList();
        NotifyUI();
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
        var idx = _cachedEvents.FindIndex(e => e.Id == updated.Id);
        if (idx >= 0) _cachedEvents[idx] = updated;
        NotifyUI();
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
        _cachedEvents.RemoveAll(e => e.Id == target.Id);
        NotifyUI();
    }

    /// <summary>
    /// Firebase를 Excel 데이터로 리셋한다 (기존 Firebase 전체 삭제 → Excel 데이터 일괄 업로드).
    /// Excel에는 쓰지 않음 (이미 있는 데이터를 올리는 것이므로).
    /// </summary>
    public async Task ResetFirebaseAsync(List<BabyEvent> events, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (!_firestoreService.IsConfigured)
            throw new InvalidOperationException("Firebase가 구성되지 않았습니다.");

        // 1. 기존 Firebase 데이터 전체 삭제
        await _firestoreService.DeleteAllEventsAsync(progress, ct);

        // 2. Excel 데이터 일괄 업로드 (Excel 쓰기 없음, Firestore만)
        await _firestoreService.BulkAddEventsAsync(events, progress, ct);

        // 3. 캐시 갱신 + UI 통지 (1회)
        _cachedEvents = events.OrderBy(e => e.FullDateTime).ToList();
        _isOnline = true;
        UpdateSyncTime();
        NotifyUI();

        LogService.Event($"Firebase 리셋 완료: {events.Count}건 업로드");
    }

    public async Task<List<BabyEvent>> ImportEventsAsync(string filePath, ImportMode mode)
    {
        var events = await _excelService.ImportEventsAsync(filePath, mode);
        // Import된 Excel 데이터로 캐시 갱신 → UI에 즉시 반영
        // (Firebase에는 아직 안 올라감 — 별도 업로드 필요)
        _cachedEvents = events.OrderBy(e => e.FullDateTime).ToList();
        return events;
    }

    public Task ExportEventsAsync(string targetPath, List<BabyEvent> events)
        => _excelService.ExportEventsAsync(targetPath, events);

    public Task<string> CreateBackupAsync(string? targetPath = null)
        => _excelService.CreateBackupAsync(ExcelPath, targetPath);

    /// <summary>
    /// 경량 폴링: meta/lastUpdated 문서만 읽어서 변경 여부 확인 (1 read).
    /// 변경 감지 시에만 전체 이벤트를 가져온다.
    /// </summary>
    private void UpdateSyncTime()
    {
        LastSyncTime = DateTime.Now;
        Application.Current?.Dispatcher.Invoke(() => SyncTimeChanged?.Invoke(LastSyncTime.Value));
    }

    private void NotifyUI()
    {
        Application.Current?.Dispatcher.Invoke(() => EventsChanged?.Invoke(_cachedEvents));
    }
}
