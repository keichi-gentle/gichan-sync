using CommunityToolkit.Mvvm.ComponentModel;
using GichanDiary.Models;
using GichanDiary.Services;

namespace GichanDiary.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly ICalculationService _calcService;
    private readonly ISettingsService _settingsService;

    // ── Feed card ───────────────────────────────────────
    [ObservableProperty] private string _lastFeedTime = "-";
    [ObservableProperty] private string _lastFeedDetail = "-";
    [ObservableProperty] private string _feedElapsed = "-";
    [ObservableProperty] private string _nextFeed1 = "-";
    [ObservableProperty] private string _nextFeed1Label = "예상1(3시간)";
    [ObservableProperty] private string _nextFeed2 = "-";
    [ObservableProperty] private string _nextFeed2Label = "예상2(평균 10회)";
    [ObservableProperty] private string _todayFeedTotal = "0ml";
    [ObservableProperty] private string _todayFeedCount = "0회";
    [ObservableProperty] private string _avgFeedInterval = "-";
    [ObservableProperty] private string _feedMemo = "";

    // ── Bowel card ──────────────────────────────────────
    [ObservableProperty] private string _lastUrineTime = "-";
    [ObservableProperty] private string _urineElapsed = "-";
    [ObservableProperty] private int _todayUrineCount;
    [ObservableProperty] private string _lastStoolTime = "-";
    [ObservableProperty] private string _stoolElapsed = "-";
    [ObservableProperty] private int _todayStoolCount;

    // ── Hygiene card ────────────────────────────────────
    [ObservableProperty] private string _lastWash = "-";
    [ObservableProperty] private string _lastWashElapsed = "";
    [ObservableProperty] private string _lastBath = "-";
    [ObservableProperty] private string _lastBathElapsed = "";
    [ObservableProperty] private string _lastNails = "-";
    [ObservableProperty] private string _lastNailsElapsed = "";

    // ── Body card ───────────────────────────────────────
    // 날짜/값 분리: 날짜만 카테고리색, 값은 기본색
    [ObservableProperty] private string _lastHeightDate = "";
    [ObservableProperty] private string _lastHeightValue = "-";
    [ObservableProperty] private string _lastWeightDate = "";
    [ObservableProperty] private string _lastWeightValue = "-";
    [ObservableProperty] private string _lastHeadCircDate = "";
    [ObservableProperty] private string _lastHeadCircValue = "-";

    // ── Health card ─────────────────────────────────────
    [ObservableProperty] private string _lastHealthDate = "";
    [ObservableProperty] private string _lastHealthDetail = "-";

    // ── Etc card ────────────────────────────────────────
    [ObservableProperty] private string _lastEtcDate = "";
    [ObservableProperty] private string _lastEtcDetail = "-";

    public DashboardViewModel(ICalculationService calcService, ISettingsService settingsService)
    {
        _calcService = calcService;
        _settingsService = settingsService;
    }

    public void Refresh(List<BabyEvent> events)
    {
        var now = DateTime.Now;
        var settings = _settingsService.Load();

        // ── Feed ────────────────────────────────────────
        var lastFeed = events
            .Where(e => e.IsFeeding && e.FullDateTime.HasValue)
            .OrderByDescending(e => e.FullDateTime)
            .FirstOrDefault();

        if (lastFeed?.FullDateTime != null)
        {
            var ft = lastFeed.FullDateTime.Value;
            LastFeedTime = ft.ToString("HH:mm");
            LastFeedDetail = lastFeed.FeedingCount > 1
                ? $"{lastFeed.Detail} {lastFeed.Amount}, 분할 {lastFeed.FeedingCount}회"
                : $"{lastFeed.Detail} {lastFeed.Amount}";
            var elapsed = now - ft;
            FeedElapsed = $"{(int)elapsed.TotalHours}시간 {elapsed.Minutes}분 경과";
            FeedMemo = lastFeed.Note ?? "";

            // 예상1: 고정 수유텀 기준
            var fixedHours = (int)settings.FixedFeedingInterval.TotalHours;
            var fixedMins = settings.FixedFeedingInterval.Minutes;
            NextFeed1Label = fixedMins > 0 ? $"예상1({fixedHours}시간{fixedMins}분)" : $"예상1({fixedHours}시간)";
            var next1 = _calcService.GetNextExpectedFeed_Fixed(lastFeed, settings.FixedFeedingInterval);
            NextFeed1 = next1.HasValue ? next1.Value.ToString("HH:mm") : "-";

            // 예상2: 최근 N회 평균 기준
            NextFeed2Label = $"예상2(평균 {settings.AverageFeedingCount}회)";
            var next2 = _calcService.GetNextExpectedFeed_Average(events, settings.AverageFeedingCount);
            NextFeed2 = next2.HasValue ? next2.Value.ToString("HH:mm") : "-";
        }
        else
        {
            LastFeedTime = "-";
            LastFeedDetail = "-";
            FeedElapsed = "-";
            NextFeed1 = "-";
            NextFeed2 = "-";
            FeedMemo = "";
        }

        TodayFeedTotal = $"{_calcService.GetDailyFeedTotal(events, now):0}ml";
        TodayFeedCount = $"{_calcService.GetDailyFeedCount(events, now)}회";

        var avgInterval = _calcService.GetAverageFeedingInterval(events, settings.AverageFeedingCount);
        AvgFeedInterval = avgInterval.HasValue
            ? $"{(int)avgInterval.Value.TotalHours}:{avgInterval.Value.Minutes:D2}"
            : "-";

        // ── Bowel ───────────────────────────────────────
        var summary = _calcService.GetDailySummary(events, now);
        TodayUrineCount = summary.UrineCount;
        TodayStoolCount = summary.StoolCount;

        var urineElapsed = _calcService.GetLastUrineElapsed(events);
        if (urineElapsed.HasValue)
        {
            var lastUrine = events
                .Where(e => e.Category == EventCategory.배변 && e.HasUrine == true && e.FullDateTime.HasValue)
                .OrderByDescending(e => e.FullDateTime)
                .FirstOrDefault();
            LastUrineTime = lastUrine?.FullDateTime?.ToString("HH:mm") ?? "-";
            UrineElapsed = $"{(int)urineElapsed.Value.TotalHours}시간 {urineElapsed.Value.Minutes}분";
        }
        else
        {
            LastUrineTime = "-";
            UrineElapsed = "-";
        }

        var stoolElapsed = _calcService.GetLastStoolElapsed(events);
        if (stoolElapsed.HasValue)
        {
            var lastStool = events
                .Where(e => e.Category == EventCategory.배변 && e.HasStool == true && e.FullDateTime.HasValue)
                .OrderByDescending(e => e.FullDateTime)
                .FirstOrDefault();
            LastStoolTime = lastStool?.FullDateTime?.ToString("HH:mm") ?? "-";
            StoolElapsed = $"{(int)stoolElapsed.Value.TotalHours}시간 {stoolElapsed.Value.Minutes}분";
        }
        else
        {
            LastStoolTime = "-";
            StoolElapsed = "-";
        }

        // ── Hygiene ─────────────────────────────────────
        var hygieneEvents = events
            .Where(e => e.Category == EventCategory.위생관리 && e.FullDateTime.HasValue)
            .OrderByDescending(e => e.FullDateTime)
            .ToList();

        var washEvent = hygieneEvents
            .FirstOrDefault(e => e.Detail?.Contains("세안") == true || e.Detail?.Contains("샤워") == true);
        LastWash = washEvent?.FullDateTime?.ToString("MM/dd HH:mm") ?? "-";
        LastWashElapsed = FormatHygieneElapsed(washEvent?.FullDateTime, now);

        var bathEvent = hygieneEvents
            .FirstOrDefault(e => e.Detail?.Contains("목욕") == true || e.Detail?.Contains("샤워") == true);
        LastBath = bathEvent?.FullDateTime?.ToString("MM/dd HH:mm") ?? "-";
        LastBathElapsed = FormatHygieneElapsed(bathEvent?.FullDateTime, now);

        var nailsEvent = hygieneEvents
            .FirstOrDefault(e => e.Detail?.Contains("손발톱") == true);
        LastNails = nailsEvent?.FullDateTime?.ToString("MM/dd HH:mm") ?? "-";
        LastNailsElapsed = FormatHygieneElapsed(nailsEvent?.FullDateTime, now);

        // ── Body ────────────────────────────────────────
        var bodyEvents = events
            .Where(e => e.Category == EventCategory.신체측정 && e.FullDateTime.HasValue)
            .OrderByDescending(e => e.FullDateTime)
            .ToList();

        // 신체측정 데이터는 Detail 필드에 "키 60.5cm, 몸무게 3.2kg, 머리둘레 35cm" 형태로 저장됨
        AssignBody(bodyEvents.FirstOrDefault(e => e.Detail?.Contains("키") == true), @"키\s*([\d.]+cm)",
            d => LastHeightDate = d, v => LastHeightValue = v);
        AssignBody(bodyEvents.FirstOrDefault(e => e.Detail?.Contains("몸무게") == true), @"몸무게\s*([\d.]+kg)",
            d => LastWeightDate = d, v => LastWeightValue = v);
        AssignBody(bodyEvents.FirstOrDefault(e => e.Detail?.Contains("머리") == true), @"머리둘레\s*([\d.]+cm)",
            d => LastHeadCircDate = d, v => LastHeadCircValue = v);

        // ── Health ──────────────────────────────────────
        var lastHealth = events
            .Where(e => e.Category == EventCategory.건강관리 && e.FullDateTime.HasValue)
            .OrderByDescending(e => e.FullDateTime)
            .FirstOrDefault();
        if (lastHealth != null)
        {
            LastHealthDate = $"{lastHealth.FullDateTime:MM/dd HH:mm}";
            LastHealthDetail = $"{lastHealth.Detail} {lastHealth.Note}".Trim();
        }
        else { LastHealthDate = ""; LastHealthDetail = "-"; }

        // ── Etc ─────────────────────────────────────────
        var lastEtc = events
            .Where(e => e.Category == EventCategory.기타 && e.FullDateTime.HasValue)
            .OrderByDescending(e => e.FullDateTime)
            .FirstOrDefault();
        if (lastEtc != null)
        {
            LastEtcDate = $"{lastEtc.FullDateTime:MM/dd HH:mm}";
            LastEtcDetail = $"{lastEtc.Detail} {lastEtc.Note}".Trim();
        }
        else { LastEtcDate = ""; LastEtcDetail = "-"; }
    }

    /// <summary>
    /// 신체측정 값과 날짜를 분리해서 할당.
    /// </summary>
    private static void AssignBody(BabyEvent? evt, string pattern,
        Action<string> setDate, Action<string> setValue)
    {
        if (evt == null || string.IsNullOrEmpty(evt.Detail))
        {
            setDate(""); setValue("-"); return;
        }
        var match = System.Text.RegularExpressions.Regex.Match(evt.Detail, pattern);
        if (!match.Success) { setDate(""); setValue("-"); return; }
        setDate($"{evt.FullDateTime:MM/dd}");
        setValue(match.Groups[1].Value);
    }
    private static string FormatHygieneElapsed(DateTime? eventTime, DateTime now)
    {
        if (!eventTime.HasValue) return "";
        var elapsed = now - eventTime.Value;
        if (elapsed.TotalDays >= 1)
            return $"{(int)elapsed.TotalDays}일 {elapsed.Hours}시간 경과";
        return $"{(int)elapsed.TotalHours}시간 {elapsed.Minutes}분 경과";
    }

    /// <summary>
    /// 신체측정 Detail 문자열에서 정규식으로 수치를 추출하여 "MM/dd 값" 형식으로 반환.
    /// 예: "키 60.5cm, 몸무게 3.2kg" + 패턴 "키\s*([\d.]+cm)" → "04/09 60.5cm"
    /// </summary>
    private static string ExtractBodyValue(BabyEvent e, string pattern)
    {
        if (string.IsNullOrEmpty(e.Detail)) return "-";
        var match = System.Text.RegularExpressions.Regex.Match(e.Detail, pattern);
        if (!match.Success) return "-";
        return $"{e.FullDateTime:MM/dd} {match.Groups[1].Value}";
    }
}

// Extension to avoid temporary variables
internal static class NullableExtensions
{
    public static TResult? let<T, TResult>(this T obj, Func<T, TResult> func) where T : class where TResult : class
        => obj != null ? func(obj) : null;
}
