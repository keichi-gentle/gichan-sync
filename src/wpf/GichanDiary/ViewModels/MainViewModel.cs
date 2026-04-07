using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GichanDiary.Models;
using GichanDiary.Services;

namespace GichanDiary.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private static readonly string[] KoreanDayNames = { "일", "월", "화", "수", "목", "금", "토" };

    private readonly IDataService _dataService;
    private readonly IExcelService _excelService;
    private readonly ICalculationService _calcService;
    private readonly ISettingsService _settingsService;
    private readonly ITimerService _timerService;
    private readonly SyncCoordinator? _syncCoordinator;

    private AppSettings _settings = new();
    private List<BabyEvent> _events = new();

    // ── Scoreboard properties ────────────────────────────

    [ObservableProperty] private string _currentTime = "00 : 00 : 00";
    [ObservableProperty] private string _dateDisplay = "";
    [ObservableProperty] private string _dayDisplay = "";

    // Feed
    [ObservableProperty] private string _lastFeedInfo = "-";
    [ObservableProperty] private string _feedElapsed = "경과: -";
    [ObservableProperty] private string _nextFeedTime = "-";
    [ObservableProperty] private string _nextFeedRemain = "";
    [ObservableProperty] private double _feedProgressPercent;
    [ObservableProperty] private bool _isNextFeedUrgent;
    [ObservableProperty] private string _todayFeedSummary = "0ml / 0회";
    [ObservableProperty] private string _avgFeedInterval = "일일 평균 텀 -";
    [ObservableProperty] private string _h24AvgFeedInterval = "24H 평균 텀 -";

    // 예상 B/C (로직만, GUI 미정)
    private DateTime? _nextFeedExpectedB;
    private DateTime? _nextFeedExpectedC;
    public DateTime? NextFeedExpectedB => _nextFeedExpectedB;
    public DateTime? NextFeedExpectedC => _nextFeedExpectedC;

    // Bowel
    [ObservableProperty] private string _lastUrineTime = "-";
    [ObservableProperty] private string _urineElapsed = "경과: -";
    [ObservableProperty] private string _todayUrineCount = "0회";
    [ObservableProperty] private string _lastStoolTime = "-";
    [ObservableProperty] private string _stoolElapsed = "경과: -";
    [ObservableProperty] private string _todayStoolCount = "0회";

    // ── Sync status ────────────────────────────────────────
    [ObservableProperty] private bool _isSyncOnline;
    [ObservableProperty] private string _syncStatusText = "Off-Line";
    [ObservableProperty] private string _lastSyncDisplay = "";

    // ── Theme toggle ──────────────────────────────────────

    [ObservableProperty] private string _themeLabel = "낮";

    // ── Window title ────────────────────────────────────

    [ObservableProperty] private string _windowTitle = "아기 주요 이벤트 일지";
    [ObservableProperty] private string _statusMessage = "";

    // ── Tab navigation ───────────────────────────────────

    [ObservableProperty] private ObservableObject? _currentView;
    [ObservableProperty] private string _selectedTab = "현황";

    // ── Constructor ──────────────────────────────────────

    public MainViewModel(
        IDataService dataService,
        IExcelService excelService,
        ICalculationService calcService,
        ISettingsService settingsService,
        ITimerService timerService,
        SyncCoordinator? syncCoordinator = null)
    {
        _dataService = dataService;
        _excelService = excelService;
        _calcService = calcService;
        _settingsService = settingsService;
        _timerService = timerService;
        _syncCoordinator = syncCoordinator;

        _timerService.Tick += OnTimerTick;

        // Firebase 동기화 상태 구독
        if (_dataService is FirebaseSyncDataService syncService)
        {
            syncService.SyncTimeChanged += (dt) =>
            {
                IsSyncOnline = true;
                SyncStatusText = "On-Line";
                LastSyncDisplay = dt.ToString("HH:mm:ss");
            };
        }

        _dataService.EventsChanged += (events) =>
        {
            _events = events;
            _calcService.CalculateFeedingIntervals(_events);
            RefreshScoreboard();
        };
    }

    // ── Initialization (called after construction) ───────

    public async Task InitializeAsync()
    {
        _settings = _settingsService.Load();

        if (!string.IsNullOrEmpty(_settings.ExcelFilePath)
            && System.IO.File.Exists(_settings.ExcelFilePath))
        {
            try
            {
                _events = await _dataService.LoadEventsAsync();
                _calcService.CalculateFeedingIntervals(_events);
            }
            catch (System.IO.IOException)
            {
                System.Windows.MessageBox.Show(
                    $"데이터 파일이 다른 프로그램에서 열려 있습니다.\n파일을 닫은 후 프로그램을 재시작해 주세요.",
                    "파일 잠금 오류", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"데이터를 읽는 중 오류가 발생했습니다:\n{ex.Message}",
                    "데이터 로드 오류", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        // 테마 레이블 초기화 (현재 테마의 반대 = 전환 대상 표시)
        ThemeLabel = _settings.Theme == "Dark" ? "낮" : "밤";

        UpdateWindowTitle();
        RefreshScoreboard();
        _timerService.Start();

        // Start with Dashboard tab active
        ShowDashboard();
    }

    // ── Timer callback ───────────────────────────────────

    private void OnTimerTick()
    {
        var now = DateTime.Now;
        CurrentTime = now.ToString("HH : mm : ss");
        UpdateElapsedValues();

        // 동기화 온/오프라인 상태 갱신
        if (_dataService is FirebaseSyncDataService svc)
        {
            if (!svc.IsOnline && IsSyncOnline)
            {
                IsSyncOnline = false;
                SyncStatusText = "Off-Line";
            }
        }
    }

    // ── Window title ────────────────────────────────────

    private void UpdateWindowTitle()
    {
        var name = _settings.BabyName;
        WindowTitle = string.IsNullOrWhiteSpace(name)
            ? "주요 이벤트 일지"
            : $"주요 이벤트 일지 - {name}";
    }

    // ── Scoreboard refresh ───────────────────────────────

    public void RefreshScoreboard()
    {
        var now = DateTime.Now;
        CurrentTime = now.ToString("HH : mm : ss");

        // Date & day
        var dayName = KoreanDayNames[(int)now.DayOfWeek];
        DateDisplay = $"{now:yyyy/MM/dd}({dayName})";

        if (_settings.BabyBirthDate.HasValue)
        {
            var dayNum = _calcService.CalculateDayNumber(now, _settings.BabyBirthDate.Value);
            DayDisplay = $"{dayNum}일차";
        }
        else
        {
            DayDisplay = "";
        }

        // Feed info
        var lastFeed = _events
            .Where(e => e.IsFeeding && e.FullDateTime.HasValue)
            .OrderByDescending(e => e.FullDateTime)
            .FirstOrDefault();

        if (lastFeed?.FullDateTime != null)
        {
            var feedTime = lastFeed.FullDateTime.Value;
            // 전광판: 제품명 대신 간략 표시 (분유+모유, 분유, 모유)
            var feedType = (lastFeed.FormulaAmount > 0 && lastFeed.BreastfeedAmount > 0) ? "분유+모유"
                         : (lastFeed.FormulaAmount > 0) ? "분유"
                         : (lastFeed.BreastfeedAmount > 0) ? "모유"
                         : lastFeed.Detail;
            LastFeedInfo = $"{feedTime:HH:mm} {feedType} {lastFeed.Amount}";
            UpdateFeedElapsed(now, feedTime);

            // Next feed (fixed interval)
            var nextFeed = _calcService.GetNextExpectedFeed_Fixed(lastFeed, _settings.FixedFeedingInterval);
            if (nextFeed.HasValue)
            {
                NextFeedTime = nextFeed.Value.ToString("HH:mm");
                var remain = nextFeed.Value - now;
                if (remain.TotalSeconds > 0)
                {
                    NextFeedRemain = $"- {(int)remain.TotalHours}시간 {remain.Minutes}분 남음";
                    IsNextFeedUrgent = remain.TotalMinutes <= 30;
                }
                else
                {
                    var over = now - nextFeed.Value;
                    NextFeedRemain = $"시간 초과! {(int)over.TotalHours}시간{over.Minutes}분";
                    IsNextFeedUrgent = true;
                }

                // Progress
                var totalInterval = _settings.FixedFeedingInterval.TotalSeconds;
                var elapsed = (now - feedTime).TotalSeconds;
                FeedProgressPercent = Math.Min(100, Math.Max(0, elapsed / totalInterval * 100));
            }
            else
            {
                NextFeedTime = "-";
                NextFeedRemain = "";
                FeedProgressPercent = 0;
                IsNextFeedUrgent = false;
            }
        }
        else
        {
            LastFeedInfo = "-";
            FeedElapsed = "경과: -";
            NextFeedTime = "-";
            NextFeedRemain = "";
            FeedProgressPercent = 0;
            IsNextFeedUrgent = false;
        }

        // Today feed summary
        var todayTotal = _calcService.GetDailyFeedTotal(_events, now);
        var todayCount = _calcService.GetDailyFeedCount(_events, now);
        TodayFeedSummary = $"{todayTotal:0}ml / {todayCount}회";

        // 최근 수유 시각 (예상 B/C 계산용)
        var lastFeedDt = _events
            .Where(e => e.IsFeeding && e.FullDateTime.HasValue)
            .OrderByDescending(e => e.FullDateTime)
            .FirstOrDefault()?.FullDateTime;

        // 평균텀 B: 오늘 날짜 수유만 필터 → 간격 평균
        TimeSpan? avgTsB = null;
        var todayFeeds = _events
            .Where(e => e.IsFeeding && e.FullDateTime.HasValue && e.Date.Date == now.Date)
            .OrderBy(e => e.FullDateTime).ToList();
        if (todayFeeds.Count >= 2)
        {
            var intervals = new List<double>();
            for (int i = 1; i < todayFeeds.Count; i++)
                intervals.Add((todayFeeds[i].FullDateTime!.Value - todayFeeds[i - 1].FullDateTime!.Value).TotalMinutes);
            avgTsB = TimeSpan.FromMinutes(intervals.Average());
            AvgFeedInterval = $"일일 평균 텀 {(int)avgTsB.Value.TotalHours}:{avgTsB.Value.Minutes:D2}";
        }
        else
        {
            AvgFeedInterval = "일일 평균 텀 -";
        }

        // 예상 B: 평균텀 B 기준 다음 수유 예상 시간
        _nextFeedExpectedB = (avgTsB.HasValue && lastFeedDt.HasValue)
            ? lastFeedDt.Value.Add(avgTsB.Value) : null;

        // 평균텀 C: 최근 수유로부터 24시간 전까지의 수유 간격 평균
        TimeSpan? avgTsC = null;
        var allFeeds = _events
            .Where(e => e.IsFeeding && e.FullDateTime.HasValue)
            .OrderBy(e => e.FullDateTime).ToList();
        if (allFeeds.Count >= 2 && lastFeedDt.HasValue)
        {
            var cutoff = lastFeedDt.Value.AddHours(-24);
            var h24Feeds = allFeeds.Where(e => e.FullDateTime!.Value >= cutoff).ToList();
            if (h24Feeds.Count >= 2)
            {
                var intervals = new List<double>();
                for (int i = 1; i < h24Feeds.Count; i++)
                    intervals.Add((h24Feeds[i].FullDateTime!.Value - h24Feeds[i - 1].FullDateTime!.Value).TotalMinutes);
                avgTsC = TimeSpan.FromMinutes(intervals.Average());
                H24AvgFeedInterval = $"24H 평균 텀 {(int)avgTsC.Value.TotalHours}:{avgTsC.Value.Minutes:D2}";
            }
            else
            {
                H24AvgFeedInterval = "24H 평균 텀 -";
            }
        }
        else
        {
            H24AvgFeedInterval = "24H 평균 텀 -";
        }

        // 예상 C: 평균텀 C 기준 다음 수유 예상 시간
        _nextFeedExpectedC = (avgTsC.HasValue && lastFeedDt.HasValue)
            ? lastFeedDt.Value.Add(avgTsC.Value) : null;

        // Bowel
        UpdateBowelValues(now);
    }

    private void UpdateElapsedValues()
    {
        var now = DateTime.Now;

        // Feed elapsed
        var lastFeed = _events
            .Where(e => e.IsFeeding && e.FullDateTime.HasValue)
            .OrderByDescending(e => e.FullDateTime)
            .FirstOrDefault();

        if (lastFeed?.FullDateTime != null)
        {
            var feedTime = lastFeed.FullDateTime.Value;
            UpdateFeedElapsed(now, feedTime);

            // Progress
            var totalInterval = _settings.FixedFeedingInterval.TotalSeconds;
            var elapsed = (now - feedTime).TotalSeconds;
            FeedProgressPercent = Math.Min(100, Math.Max(0, elapsed / totalInterval * 100));

            // Next feed remain
            var nextFeed = _calcService.GetNextExpectedFeed_Fixed(lastFeed, _settings.FixedFeedingInterval);
            if (nextFeed.HasValue)
            {
                var remain = nextFeed.Value - now;
                if (remain.TotalSeconds > 0)
                {
                    NextFeedRemain = $"- {(int)remain.TotalHours}시간 {remain.Minutes}분 남음";
                    IsNextFeedUrgent = remain.TotalMinutes <= 30;
                }
                else
                {
                    var over = now - nextFeed.Value;
                    NextFeedRemain = $"시간 초과! {(int)over.TotalHours}시간{over.Minutes}분";
                    IsNextFeedUrgent = true;
                }
            }
        }

        // Bowel elapsed
        UpdateBowelValues(now);
    }

    private void UpdateFeedElapsed(DateTime now, DateTime feedTime)
    {
        var elapsed = now - feedTime;
        FeedElapsed = $"경과: {(int)elapsed.TotalHours}시간 {elapsed.Minutes}분";
    }

    private void UpdateBowelValues(DateTime now)
    {
        // Urine
        var urineElapsed = _calcService.GetLastUrineElapsed(_events);
        if (urineElapsed.HasValue)
        {
            var lastUrine = _events
                .Where(e => e.Category == EventCategory.배변 && e.HasUrine == true && e.FullDateTime.HasValue)
                .OrderByDescending(e => e.FullDateTime)
                .FirstOrDefault();
            LastUrineTime = lastUrine?.FullDateTime?.ToString("HH:mm") ?? "-";
            UrineElapsed = $"경과: {(int)urineElapsed.Value.TotalHours}시간 {urineElapsed.Value.Minutes}분";
        }
        else
        {
            LastUrineTime = "-";
            UrineElapsed = "경과: -";
        }

        // Stool
        var stoolElapsed = _calcService.GetLastStoolElapsed(_events);
        if (stoolElapsed.HasValue)
        {
            var lastStool = _events
                .Where(e => e.Category == EventCategory.배변 && e.HasStool == true && e.FullDateTime.HasValue)
                .OrderByDescending(e => e.FullDateTime)
                .FirstOrDefault();
            LastStoolTime = lastStool?.FullDateTime?.ToString("HH:mm") ?? "-";
            StoolElapsed = $"경과: {(int)stoolElapsed.Value.TotalHours}시간 {stoolElapsed.Value.Minutes}분";
        }
        else
        {
            LastStoolTime = "-";
            StoolElapsed = "경과: -";
        }

        // Today counts
        var summary = _calcService.GetDailySummary(_events, now);
        TodayUrineCount = $"{summary.UrineCount}회";
        TodayStoolCount = $"{summary.StoolCount}회";
    }

    // ── Theme toggle command ──────────────────────────────

    [RelayCommand]
    private void ToggleTheme()
    {
        var newTheme = _settings.Theme == "Dark" ? "Light" : "Dark";
        _settings.Theme = newTheme;
        _settingsService.Save(_settings);
        Services.ThemeManager.ApplyTheme(newTheme);
        ThemeLabel = newTheme == "Dark" ? "낮" : "밤";
        StatusMessage = newTheme == "Dark" ? "밤 모드로 전환됨" : "낮 모드로 전환됨";
        Services.LogService.Event($"테마 변경: {newTheme}");

        // 현재 탭 다시 로드하여 테마 즉시 반영
        RefreshCurrentTab();
    }

    private void RefreshCurrentTab()
    {
        switch (SelectedTab)
        {
            case "현황": ShowDashboard(); break;
            case "기록": ShowEntry(); break;
            case "조회": ShowList(); break;
            case "리포트": ShowReport(); break;
            case "엑셀": ShowExcel(); break;
            case "설정": ShowSettings(); break;
        }
    }

    // ── Tab commands ─────────────────────────────────────

    [RelayCommand]
    private void ShowDashboard()
    {
        SelectedTab = "현황";
        var vm = new DashboardViewModel(_calcService, _settingsService);
        vm.Refresh(_events);
        CurrentView = vm;
    }

    [RelayCommand]
    private void ShowEntry()
    {
        SelectedTab = "기록";
        var entryVm = new EventEntryViewModel(_dataService, _calcService, _settingsService);
        entryVm.EventSaved += OnEventSaved;
        CurrentView = entryVm;
    }

    [RelayCommand]
    private void ShowList()
    {
        SelectedTab = "조회";
        var vm = new EventListViewModel(_dataService, _calcService, _settingsService);
        vm.DataChanged += OnEventSaved;
        vm.EditRequested += EditEvent;
        vm.Refresh(_events);
        CurrentView = vm;
    }

    public void EditEvent(BabyEvent evt)
    {
        SelectedTab = "기록";
        var entryVm = new EventEntryViewModel(_dataService, _calcService, _settingsService);
        entryVm.LoadForEdit(evt);
        entryVm.EventSaved += async () =>
        {
            await ReloadEventsAsync();
            RefreshScoreboard();
            ShowList();
        };
        CurrentView = entryVm;
    }

    [RelayCommand]
    private void ShowReport()
    {
        SelectedTab = "리포트";
        var vm = new ReportViewModel(_calcService, _settingsService);
        vm.Refresh(_events);
        CurrentView = vm;
    }

    [RelayCommand]
    private void ShowExcel()
    {
        SelectedTab = "엑셀";
        var vm = new ImportExportViewModel(_excelService, _settingsService);
        vm.Refresh(_events);
        vm.DataChanged += OnEventSaved;
        CurrentView = vm;
    }

    [RelayCommand]
    private void ShowSettings()
    {
        SelectedTab = "설정";
        var vm = new SettingsViewModel(_settingsService, _syncCoordinator, _excelService);
        vm.SettingsSaved += async () =>
        {
            _settings = _settingsService.Load();
            UpdateWindowTitle();
            if (!string.IsNullOrEmpty(_settings.ExcelFilePath)
                && System.IO.File.Exists(_settings.ExcelFilePath))
            {
                try
                {
                    _events = await _dataService.LoadEventsAsync();
                    _calcService.CalculateFeedingIntervals(_events);
                }
                catch (Exception)
                {
                    // 설정 저장 후 새로고침 실패 시 기존 데이터 유지
                }
            }
            RefreshScoreboard();
        };
        CurrentView = vm;
    }

    private async Task ReloadEventsAsync()
    {
        if (!string.IsNullOrEmpty(_settings.ExcelFilePath)
            && System.IO.File.Exists(_settings.ExcelFilePath))
        {
            try
            {
                _events = await _dataService.LoadEventsAsync();
                _calcService.CalculateFeedingIntervals(_events);
            }
            catch (Exception)
            {
                // 새로고침 실패 시 기존 데이터 유지
            }
        }
    }

    private async void OnEventSaved()
    {
        await ReloadEventsAsync();
        RefreshScoreboard();

        // Refresh list view if it's currently active
        if (CurrentView is EventListViewModel listVm)
        {
            listVm.Refresh(_events);
        }
    }

    // ── Public accessors for other VMs ───────────────────

    public List<BabyEvent> Events => _events;
    public AppSettings Settings => _settings;
}
