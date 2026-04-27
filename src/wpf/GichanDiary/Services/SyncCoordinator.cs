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
            // 데이터 경로는 항상 admin uid (모든 사용자가 admin 데이터 공유)
            // 인증 토큰은 현재 로그인된 사용자 본인의 것
            const string DataUid = "KrTxuQMTE9Ve2PXJcVTUJmhQntB3";
            _firestoreService.SetCredentials(DataUid, _authService.IdToken);
            var syncService = new FirebaseSyncDataService(_excelService, _settingsService, _firestoreService, _authService);
            _dataService = syncService;
        }
        else
        {
            _dataService = new ExcelOnlyDataService(_excelService, _settingsService);
        }

        return _dataService;
    }

    /// <summary>
    /// 저장된 토큰으로 세션 복원만 시도. 실패 시 false 반환 (호출부가 LoginDialog 띄울지 결정).
    /// </summary>
    public async Task<bool> TryEnableFirebaseSync()
    {
        if (!_authService.IsSignedIn)
        {
            var restored = await _authService.TryRestoreSessionAsync();
            if (!restored)
            {
                LogService.System("Firebase 동기화: 저장된 세션 없음/만료 — 로그인 필요");
                return false;
            }
        }

        if (_authService.IsSignedIn)
        {
            CreateDataService(SyncMode.FirebaseSync);
            LogService.System($"Firebase 동기화: 활성화 (UID={_authService.UserId}, Email={_authService.Email})");
            return true;
        }

        LogService.System("Firebase 동기화: 활성화 실패 — 인증 정보 없음");
        return false;
    }

    /// <summary>
    /// 새 로그인이 성공한 후 호출 — DataService를 FirebaseSync로 전환.
    /// </summary>
    public void OnSignedIn()
    {
        if (_authService.IsSignedIn)
        {
            CreateDataService(SyncMode.FirebaseSync);
            LogService.System($"Firebase 동기화: 신규 로그인 후 활성화 (Email={_authService.Email})");
        }
    }

    public void DisableFirebaseSync()
    {
        CreateDataService(SyncMode.ExcelOnly);
    }
}
