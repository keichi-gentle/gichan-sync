using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GichanDiary.Models;
using GichanDiary.Services;

namespace GichanDiary.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly SyncCoordinator? _syncCoordinator;
    private readonly IExcelService? _excelService;

    // ── File settings ───────────────────────────────────
    [ObservableProperty] private string _excelFilePath = "";
    [ObservableProperty] private bool _autoBackupEnabled = true;

    // ── Baby info ───────────────────────────────────────
    [ObservableProperty] private string _babyName = "";
    [ObservableProperty] private DateTime? _babyBirthDate;

    // ── Formula products ────────────────────────────────
    public ObservableCollection<string> FormulaProducts { get; } = new();
    [ObservableProperty] private string _newProductName = "";
    [ObservableProperty] private string _defaultFormulaProduct = "";

    // ── Feeding settings ────────────────────────────────
    [ObservableProperty] private int _fixedFeedingHours = 3;
    [ObservableProperty] private int _fixedFeedingMinutes;
    [ObservableProperty] private int _averageFeedingCount = 10;
    [ObservableProperty] private int _defaultBreastfeedAmount = 20;
    [ObservableProperty] private int _defaultFormulaAmount = 100;

    // ── Display settings ────────────────────────────────
    [ObservableProperty] private int _pageSize = 30;

    // ── Status message ──────────────────────────────────
    [ObservableProperty] private string _statusMessage = "";

    // ── Firebase sync ──────────────────────────────────
    [ObservableProperty] private bool _firebaseSyncEnabled;
    [ObservableProperty] private string _lastSyncTime = "-";
    [ObservableProperty] private string _syncStatusMessage = "";
    [ObservableProperty] private bool _isSyncing;
    [ObservableProperty] private bool _isUploading;
    [ObservableProperty] private bool _isDownloading;
    private CancellationTokenSource? _syncCts;

    public event Action? SettingsSaved;

    public SettingsViewModel(ISettingsService settingsService,
        SyncCoordinator? syncCoordinator = null,
        IExcelService? excelService = null)
    {
        _settingsService = settingsService;
        _syncCoordinator = syncCoordinator;
        _excelService = excelService;
        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        var s = _settingsService.Load();
        ExcelFilePath = s.ExcelFilePath ?? "";
        AutoBackupEnabled = s.AutoBackupEnabled;
        BabyName = s.BabyName ?? "";
        BabyBirthDate = s.BabyBirthDate;
        DefaultFormulaProduct = s.DefaultFormulaProduct;
        FixedFeedingHours = (int)s.FixedFeedingInterval.TotalHours;
        FixedFeedingMinutes = s.FixedFeedingInterval.Minutes;
        AverageFeedingCount = s.AverageFeedingCount;
        PageSize = s.PageSize;
        DefaultBreastfeedAmount = s.DefaultBreastfeedAmount;
        DefaultFormulaAmount = s.DefaultFormulaAmount;
        FirebaseSyncEnabled = s.FirebaseSyncEnabled;

        FormulaProducts.Clear();
        foreach (var p in s.FormulaProducts)
            FormulaProducts.Add(p);
    }

    [RelayCommand]
    private void Save()
    {
        var s = new AppSettings
        {
            ExcelFilePath = ExcelFilePath,
            AutoBackupEnabled = AutoBackupEnabled,
            BabyName = BabyName,
            BabyBirthDate = BabyBirthDate,
            FormulaProducts = FormulaProducts.ToList(),
            DefaultFormulaProduct = DefaultFormulaProduct,
            FixedFeedingInterval = new TimeSpan(FixedFeedingHours, FixedFeedingMinutes, 0),
            AverageFeedingCount = AverageFeedingCount,
            PageSize = PageSize,
            DefaultBreastfeedAmount = DefaultBreastfeedAmount,
            DefaultFormulaAmount = DefaultFormulaAmount,
            FirebaseSyncEnabled = FirebaseSyncEnabled,
        };
        _settingsService.Save(s);
        StatusMessage = "설정이 저장되었습니다.";
        Services.LogService.Event("설정 저장");
        SettingsSaved?.Invoke();
    }

    [RelayCommand]
    private void BrowseExcelPath()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Excel 파일 (*.xlsx)|*.xlsx|모든 파일 (*.*)|*.*",
            Title = "엑셀 파일 선택"
        };
        if (dlg.ShowDialog() == true)
        {
            ExcelFilePath = dlg.FileName;
        }
    }

    [RelayCommand]
    private void AddProduct()
    {
        if (!string.IsNullOrWhiteSpace(NewProductName) && !FormulaProducts.Contains(NewProductName.Trim()))
        {
            FormulaProducts.Add(NewProductName.Trim());
            NewProductName = "";
        }
    }

    [RelayCommand]
    private void RemoveProduct(string? product)
    {
        if (product != null && FormulaProducts.Contains(product))
        {
            FormulaProducts.Remove(product);
        }
    }

    // ── Firebase sync commands ─────────────────────────

    [RelayCommand]
    private async Task UploadToFirebase()
    {
        if (_syncCoordinator == null || _excelService == null)
        {
            SyncStatusMessage = "동기화 서비스를 사용할 수 없습니다.";
            return;
        }

        _syncCts = new CancellationTokenSource();
        IsSyncing = true;
        IsUploading = true;
        SyncStatusMessage = "Firebase 리셋 준비 중...";
        try
        {
            if (_syncCoordinator.CurrentMode != SyncMode.FirebaseSync)
                await _syncCoordinator.TryEnableFirebaseSync();

            var dataService = _syncCoordinator.GetDataService();
            var events = await _excelService.LoadEventsAsync(_settingsService.Load().ExcelFilePath!);

            if (dataService is FirebaseSyncDataService syncService)
            {
                var progress = new Progress<string>(msg =>
                    SyncStatusMessage = msg);
                await syncService.ResetFirebaseAsync(events, progress, _syncCts.Token);
            }

            LastSyncTime = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");
            SyncStatusMessage = $"업로드 완료 ({events.Count}건)";
            LogService.Event($"Firebase 리셋 업로드: {events.Count}건");
        }
        catch (OperationCanceledException)
        {
            SyncStatusMessage = "업로드 중단됨";
            LogService.System("Firebase upload cancelled by user");
        }
        catch (Exception ex)
        {
            SyncStatusMessage = $"업로드 실패: {ex.Message}";
            LogService.System($"Firebase upload failed: {ex.Message}");
        }
        finally
        {
            IsSyncing = false;
            IsUploading = false;
            _syncCts?.Dispose();
            _syncCts = null;
        }
    }

    [RelayCommand]
    private async Task DownloadFromFirebase()
    {
        if (_syncCoordinator == null || _excelService == null)
        {
            SyncStatusMessage = "동기화 서비스를 사용할 수 없습니다.";
            return;
        }

        _syncCts = new CancellationTokenSource();
        IsSyncing = true;
        IsDownloading = true;
        SyncStatusMessage = "Firebase에서 내려받는 중...";
        try
        {
            if (_syncCoordinator.CurrentMode != SyncMode.FirebaseSync)
                await _syncCoordinator.TryEnableFirebaseSync();

            _syncCts.Token.ThrowIfCancellationRequested();
            var dataService = _syncCoordinator.GetDataService();

            // 내려받기 전 기존 건수 저장 (비교용)
            var prevCount = (dataService is FirebaseSyncDataService prev) ? prev.CachedCount : 0;

            if (dataService is FirebaseSyncDataService syncService)
                syncService.InvalidateCache();
            var events = await dataService.LoadEventsAsync();

            _syncCts.Token.ThrowIfCancellationRequested();
            var excelPath = _settingsService.Load().ExcelFilePath!;
            await _excelService.ExportEventsAsync(excelPath, events);

            // 캐시 갱신 + UI 통지 (대시보드/전광판 즉시 반영)
            if (dataService is FirebaseSyncDataService svc)
                await svc.ForceReloadAndNotifyAsync();

            LastSyncTime = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");

            if (events.Count == prevCount && prevCount > 0)
            {
                SyncStatusMessage = $"이미 최신 상태입니다 ({events.Count}건)";
                // 토스트: 3초 후 메시지 자동 소멸
                _ = Task.Delay(3000).ContinueWith(_ =>
                    System.Windows.Application.Current?.Dispatcher.Invoke(() => {
                        if (SyncStatusMessage.StartsWith("이미 최신")) SyncStatusMessage = "";
                    }));
            }
            else
            {
                SyncStatusMessage = $"내려받기 완료 ({events.Count}건)";
            }
            LogService.Event($"Firebase 다운로드: {events.Count}건");
        }
        catch (OperationCanceledException)
        {
            SyncStatusMessage = "내려받기 중단됨";
            LogService.System("Firebase download cancelled by user");
        }
        catch (Exception ex)
        {
            SyncStatusMessage = $"내려받기 실패: {ex.Message}";
            LogService.System($"Firebase download failed: {ex.Message}");
        }
        finally
        {
            IsSyncing = false;
            IsDownloading = false;
            _syncCts?.Dispose();
            _syncCts = null;
        }
    }

    [RelayCommand]
    private void CancelSync()
    {
        _syncCts?.Cancel();
        SyncStatusMessage = "중단 요청...";
    }

    // ── Spinner commands ────────────────────────────────

    [RelayCommand] private void IncrementFixedHours() => FixedFeedingHours = Math.Min(12, FixedFeedingHours + 1);
    [RelayCommand] private void DecrementFixedHours() => FixedFeedingHours = Math.Max(0, FixedFeedingHours - 1);
    [RelayCommand] private void IncrementFixedMinutes() => FixedFeedingMinutes = Math.Min(59, FixedFeedingMinutes + 5);
    [RelayCommand] private void DecrementFixedMinutes() => FixedFeedingMinutes = Math.Max(0, FixedFeedingMinutes - 5);
    [RelayCommand] private void IncrementAvgCount() => AverageFeedingCount = Math.Min(50, AverageFeedingCount + 1);
    [RelayCommand] private void DecrementAvgCount() => AverageFeedingCount = Math.Max(1, AverageFeedingCount - 1);
    [RelayCommand] private void IncrementBreastfeed() => DefaultBreastfeedAmount = Math.Min(100, DefaultBreastfeedAmount + 5);
    [RelayCommand] private void DecrementBreastfeed() => DefaultBreastfeedAmount = Math.Max(0, DefaultBreastfeedAmount - 5);
    [RelayCommand] private void IncrementFormulaAmount() => DefaultFormulaAmount = Math.Min(200, DefaultFormulaAmount + 5);
    [RelayCommand] private void DecrementFormulaAmount() => DefaultFormulaAmount = Math.Max(10, DefaultFormulaAmount - 5);
    [RelayCommand] private void IncrementPageSize() => PageSize = Math.Min(1000, PageSize + 10);
    [RelayCommand] private void DecrementPageSize() => PageSize = Math.Max(10, PageSize - 10);
}
