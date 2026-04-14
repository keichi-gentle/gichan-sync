using GichanDiary.Models;

namespace GichanDiary.Services;

/// <summary>
/// Manages sync mode switching between ExcelOnly and FirebaseSync.
/// </summary>
public class SyncCoordinator
{
    private readonly IExcelService _excelService;
    private readonly ISettingsService _settingsService;
    private readonly FirestoreService _firestoreService;
    private readonly FirebaseAuthService _authService;

    private IDataService? _dataService;
    private SyncMode _mode = SyncMode.ExcelOnly;

    public SyncCoordinator(
        IExcelService excelService,
        ISettingsService settingsService,
        FirestoreService firestoreService,
        FirebaseAuthService authService)
    {
        _excelService = excelService;
        _settingsService = settingsService;
        _firestoreService = firestoreService;
        _authService = authService;
    }

    public SyncMode CurrentMode => _mode;

    public IDataService GetDataService()
    {
        if (_dataService != null) return _dataService;
        return CreateDataService(SyncMode.ExcelOnly);
    }

    public IDataService CreateDataService(SyncMode mode)
    {
        _mode = mode;

        if (mode == SyncMode.FirebaseSync)
        {
            var uid = _authService.UserId ?? "KrTxuQMTE9Ve2PXJcVTUJmhQntB3";
            _firestoreService.SetCredentials(uid, _authService.IdToken);
            var syncService = new FirebaseSyncDataService(_excelService, _settingsService, _firestoreService);
            _dataService = syncService;
        }
        else
        {
            _dataService = new ExcelOnlyDataService(_excelService, _settingsService);
        }

        return _dataService;
    }

    public async Task<bool> TryEnableFirebaseSync()
    {
        if (!_authService.IsSignedIn)
        {
            var settings = _settingsService.Load();
            await _authService.SignInWithSavedUidAsync("KrTxuQMTE9Ve2PXJcVTUJmhQntB3");
        }

        if (_authService.IsSignedIn)
        {
            CreateDataService(SyncMode.FirebaseSync);
            LogService.System($"Firebase 동기화: 활성화 (UID={_authService.UserId})");
            return true;
        }

        LogService.System("Firebase 동기화: 활성화 실패 — 인증 정보 없음");
        return false;
    }

    public void DisableFirebaseSync()
    {
        CreateDataService(SyncMode.ExcelOnly);
    }
}
