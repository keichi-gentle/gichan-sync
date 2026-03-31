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

        if (mode == SyncMode.FirebaseSync && _authService.IsSignedIn)
        {
            _firestoreService.SetCredentials(_authService.UserId!, _authService.IdToken!);
            var syncService = new FirebaseSyncDataService(_excelService, _settingsService, _firestoreService);
            syncService.StartPolling();
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
            // Try to sign in with saved UID (test mode)
            var settings = _settingsService.Load();
            // For now, use hardcoded UID from migration
            await _authService.SignInWithSavedUidAsync("KrTxuQMTE9Ve2PXJcVTUJmhQntB3");
        }

        if (_authService.IsSignedIn)
        {
            CreateDataService(SyncMode.FirebaseSync);
            return true;
        }

        return false;
    }

    public void DisableFirebaseSync()
    {
        if (_dataService is FirebaseSyncDataService syncService)
            syncService.StopPolling();

        CreateDataService(SyncMode.ExcelOnly);
    }
}
