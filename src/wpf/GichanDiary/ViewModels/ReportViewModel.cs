using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GichanDiary.Models;
using GichanDiary.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace GichanDiary.ViewModels;

public partial class ReportViewModel : ObservableObject
{
    private readonly ICalculationService _calcService;
    private readonly ISettingsService _settingsService;
    private List<BabyEvent> _allEvents = new();

    // ── Period filter ──────────────────────────────────────
    [ObservableProperty] private string _selectedPeriod = "7일";
    public List<string> PeriodOptions { get; } = new() { "1일", "3일", "7일", "14일", "30일", "전체" };

    // ── Today summary ──────────────────────────────────────
    [ObservableProperty] private string _todayFeedCount = "0회";
    [ObservableProperty] private string _todayFeedTotal = "0ml";
    [ObservableProperty] private string _todayUrineCount = "0";
    [ObservableProperty] private string _todayStoolCount = "0";
    [ObservableProperty] private string _avgFeedInterval = "-";
    [ObservableProperty] private string _avgFeedAmount = "-";
    [ObservableProperty] private string _lastHeight = "-";
    [ObservableProperty] private string _lastWeight = "-";

    // ── Chart 1: 일별 수유량 추이 (Line) ───────────────────
    [ObservableProperty] private ISeries[] _dailyFeedSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _dailyFeedXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _dailyFeedYAxes = Array.Empty<Axis>();

    // ── Chart 2: 수유텀 분포 (Bar) ─────────────────────────
    [ObservableProperty] private ISeries[] _feedIntervalSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _feedIntervalXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _feedIntervalYAxes = Array.Empty<Axis>();

    // ── Chart 3: 일별 배변 횟수 (Stacked bar) ──────────────
    [ObservableProperty] private ISeries[] _dailyBowelSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _dailyBowelXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _dailyBowelYAxes = Array.Empty<Axis>();

    // ── Chart 4: 1회 수유량 변화 (Line) ────────────────────
    [ObservableProperty] private ISeries[] _feedAmountSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _feedAmountXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _feedAmountYAxes = Array.Empty<Axis>();

    // ── Chart 5: 카테고리별 이벤트 비율 (Pie) ──────────────
    [ObservableProperty] private ISeries[] _categoryPieSeries = Array.Empty<ISeries>();
    [ObservableProperty] private string _categoryPieTitle = "카테고리별 이벤트 비율";

    // ── Chart 6: 일별 카테고리별 이벤트 비율 (Stacked bar) ──
    [ObservableProperty] private ISeries[] _dailyCategorySeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _dailyCategoryXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _dailyCategoryYAxes = Array.Empty<Axis>();

    // ── Chart 7: 키 변화량 (Line) ────────────────────────
    [ObservableProperty] private ISeries[] _heightSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _heightXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _heightYAxes = Array.Empty<Axis>();

    // ── Chart 8: 몸무게 변화량 (Line) ─────────────────────
    [ObservableProperty] private ISeries[] _weightSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _weightXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _weightYAxes = Array.Empty<Axis>();

    // ── Shared chart colors (테마별 — 생성 시 결정) ─────────
    private static readonly SKTypeface KoreanTypeface = SKTypeface.FromFamilyName("Malgun Gothic");
    private static readonly SKColor FeedColor = SKColor.Parse("#1E8070");
    private static readonly SKColor BowelColor = SKColor.Parse("#B88020");
    private static readonly SKColor UrineColor = SKColor.Parse("#D4B840");
    private static readonly SKColor StoolColor = SKColor.Parse("#8B4513");

    // 축/범례 색상 — ViewModel 생성 시 테마에 따라 결정
    private readonly SKColor LabelColor;
    private readonly SKColor GridColor;

    // 툴팁: 고정색 (짙은 남색 배경 + 밝은 글자 → 양 테마 OK)
    public SolidColorPaint TooltipBgPaint { get; } = new(new SKColor(0x2C, 0x34, 0x48));
    public SolidColorPaint TooltipTextPaint { get; } = new(new SKColor(0xE8, 0xEC, 0xF0))
    {
        SKTypeface = SKTypeface.FromFamilyName("Malgun Gothic")
    };
    // 범례: 테마별
    public SolidColorPaint LegendTextPaint { get; }

    public ReportViewModel(ICalculationService calcService, ISettingsService settingsService)
    {
        _calcService = calcService;
        _settingsService = settingsService;

        var isDark = _settingsService.Load().Theme == "Dark";
        if (isDark)
        {
            // Dark: 밝은 회색 축 라벨, 어두운 그리드
            LabelColor = new SKColor(0xA0, 0xB0, 0xC0);
            GridColor = new SKColor(0x3A, 0x42, 0x58);
        }
        else
        {
            // Light: 진한 글자, 밝은 그리드
            LabelColor = new SKColor(0x30, 0x38, 0x48);
            GridColor = new SKColor(0xA0, 0xA8, 0xB4);
        }
        LegendTextPaint = new SolidColorPaint(LabelColor) { SKTypeface = KoreanTypeface };
    }

    // ── Public entry point ─────────────────────────────────
    public void Refresh(List<BabyEvent> events)
    {
        _allEvents = events;
        RefreshCharts();
    }

    private Axis[] MakeYAxes(string? name = null) => new Axis[]
    {
        new Axis
        {
            LabelsPaint = new SolidColorPaint(LabelColor),
            SeparatorsPaint = new SolidColorPaint(GridColor),
            TextSize = 15,
            Name = name ?? "",
            NamePaint = name != null ? new SolidColorPaint(LabelColor) : null,
            NameTextSize = 15
        }
    };

    [RelayCommand]
    private void SetPeriod(string period) => SelectedPeriod = period;

    partial void OnSelectedPeriodChanged(string value) => RefreshCharts();

    // ── Core refresh ───────────────────────────────────────
    private void RefreshCharts()
    {
        var filtered = FilterByPeriod(_allEvents);
        CategoryPieTitle = $"카테고리별 이벤트 비율 ({SelectedPeriod})";
        UpdateTodaySummary(filtered);
        var dailyTotals = filtered
            .Where(e => e.IsFeeding)
            .GroupBy(e => e.Date.Date)
            .Select(g => (double)g.Sum(e => e.TotalFeedAmount))
            .ToList();
        AvgFeedAmount = dailyTotals.Count > 0
            ? $"{dailyTotals.Average():0}ml"
            : "-";
        BuildDailyFeedChart(filtered);
        BuildFeedIntervalChart(filtered);
        BuildDailyBowelChart(filtered);
        BuildFeedAmountChart(filtered);
        BuildCategoryPieChart(filtered);
        BuildDailyCategoryChart(filtered);
        BuildHeightChart(filtered);
        BuildWeightChart(filtered);
    }

    // ── Period filter ──────────────────────────────────────
    private List<BabyEvent> FilterByPeriod(List<BabyEvent> events)
    {
        if (SelectedPeriod == "전체") return events;

        int days = SelectedPeriod switch
        {
            "1일" => 1,
            "3일" => 3,
            "7일" => 7,
            "14일" => 14,
            "30일" => 30,
            _ => 7
        };

        var cutoff = DateTime.Today.AddDays(-(days - 1));
        return events.Where(e => e.Date >= cutoff).ToList();
    }

    // ── Today summary ──────────────────────────────────────
    private void UpdateTodaySummary(List<BabyEvent> filtered)
    {
        var now = DateTime.Now;
        var todayCount = _calcService.GetDailyFeedCount(_allEvents, now);
        var todayTotal = _calcService.GetDailyFeedTotal(_allEvents, now);
        var summary = _calcService.GetDailySummary(_allEvents, now);
        // 리포트: 기간 필터된 전체 데이터 기반 평균 (recentCount=0 → 전체)
        var avgInterval = _calcService.GetAverageFeedingInterval(filtered, 0);

        TodayFeedCount = $"{todayCount}회";
        TodayFeedTotal = $"{todayTotal:0}ml";
        TodayUrineCount = $"{summary.UrineCount}";
        TodayStoolCount = $"{summary.StoolCount}";
        AvgFeedInterval = avgInterval.HasValue
            ? $"{(int)avgInterval.Value.TotalHours} : {avgInterval.Value.Minutes:D2}"
            : "-";

        // 신체 최신 데이터
        var bodyEvents = _allEvents
            .Where(e => e.Category == EventCategory.신체측정 && e.FullDateTime.HasValue)
            .OrderByDescending(e => e.FullDateTime).ToList();
        var hEvt = bodyEvents.FirstOrDefault(e => e.Detail?.Contains("키") == true);
        var wEvt = bodyEvents.FirstOrDefault(e => e.Detail?.Contains("몸무게") == true);
        LastHeight = ExtractValue(hEvt, @"키\s*([\d.]+cm)");
        LastWeight = ExtractValue(wEvt, @"몸무게\s*([\d.]+kg)");
    }

    private static string ExtractValue(BabyEvent? e, string pattern)
    {
        if (e == null || string.IsNullOrEmpty(e.Detail)) return "-";
        var m = System.Text.RegularExpressions.Regex.Match(e.Detail, pattern);
        return m.Success ? m.Groups[1].Value : "-";
    }

    // ── Chart 1: 일별 수유량 추이 ──────────────────────────
    private void BuildDailyFeedChart(List<BabyEvent> events)
    {
        var feedEvents = events.Where(e => e.IsFeeding).ToList();
        var grouped = feedEvents
            .GroupBy(e => e.Date.Date)
            .OrderBy(g => g.Key)
            .ToList();

        var dates = grouped.Select(g => g.Key).ToArray();
        var values = grouped.Select(g => (double)g.Sum(e => e.TotalFeedAmount)).ToArray();

        var dailyLabels = dates.Select(d => d.ToString("M/d")).ToArray();
        DailyFeedSeries = new ISeries[]
        {
            new LineSeries<double>
            {
                Values = values,
                Stroke = new SolidColorPaint(FeedColor, 3),
                Fill = new SolidColorPaint(FeedColor.WithAlpha(40)),
                GeometrySize = 8,
                GeometryStroke = new SolidColorPaint(FeedColor, 2),
                GeometryFill = new SolidColorPaint(FeedColor),
                Name = "일별 수유량",
                YToolTipLabelFormatter = point => $"{dailyLabels[(int)point.Index]}: {point.Coordinate.PrimaryValue:0}ml"
            }
        };

        DailyFeedXAxes = new Axis[]
        {
            new Axis
            {
                Labels = dates.Select(d => d.ToString("M/d")).ToArray(),
                LabelsPaint = new SolidColorPaint(LabelColor),
                SeparatorsPaint = new SolidColorPaint(GridColor),
                TextSize = 15,
                LabelsRotation = dates.Length > 14 ? 45 : 0
            }
        };

        DailyFeedYAxes = MakeYAxes("ml");
    }

    // ── Chart 2: 수유텀 분포 ───────────────────────────────
    private void BuildFeedIntervalChart(List<BabyEvent> events)
    {
        var feedEvents = events
            .Where(e => e.IsFeeding && e.FullDateTime.HasValue)
            .OrderBy(e => e.FullDateTime)
            .ToList();

        var intervals = new List<double>();
        for (int i = 1; i < feedEvents.Count; i++)
        {
            var diff = feedEvents[i].FullDateTime!.Value - feedEvents[i - 1].FullDateTime!.Value;
            if (diff.TotalHours > 0 && diff.TotalHours < 12)
                intervals.Add(diff.TotalMinutes);
        }

        // Group into 30-min buckets: ~1h, ~1.5h, ~2h, ~2.5h, ~3h, ~3.5h, ~4h, ~4.5h, 5h+
        var bucketLabels = new[] { "~1h", "~1.5h", "~2h", "~2.5h", "~3h", "~3.5h", "~4h", "~4.5h", "5h+" };
        var bucketCounts = new double[9];

        foreach (var mins in intervals)
        {
            int idx = mins switch
            {
                < 75 => 0,    // ~1h (0-75min)
                < 105 => 1,   // ~1.5h (75-105)
                < 135 => 2,   // ~2h (105-135)
                < 165 => 3,   // ~2.5h (135-165)
                < 195 => 4,   // ~3h (165-195)
                < 225 => 5,   // ~3.5h (195-225)
                < 255 => 6,   // ~4h (225-255)
                < 285 => 7,   // ~4.5h (255-285)
                _ => 8        // 5h+
            };
            bucketCounts[idx]++;
        }

        FeedIntervalSeries = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Values = bucketCounts,
                Fill = new SolidColorPaint(FeedColor),
                Stroke = null,
                Name = "수유텀 분포",
                MaxBarWidth = 30,
                YToolTipLabelFormatter = point => $"{bucketLabels[(int)point.Index]}: {point.Coordinate.PrimaryValue:0}건"
            }
        };

        FeedIntervalXAxes = new Axis[]
        {
            new Axis
            {
                Labels = bucketLabels,
                LabelsPaint = new SolidColorPaint(LabelColor),
                SeparatorsPaint = new SolidColorPaint(GridColor),
                TextSize = 12
            }
        };
        FeedIntervalYAxes = MakeYAxes();
    }

    // ── Chart 3: 일별 배변 횟수 ────────────────────────────
    private void BuildDailyBowelChart(List<BabyEvent> events)
    {
        var bowelEvents = events.Where(e => e.Category == EventCategory.배변).ToList();
        var grouped = bowelEvents
            .GroupBy(e => e.Date.Date)
            .OrderBy(g => g.Key)
            .ToList();

        var dates = grouped.Select(g => g.Key).ToArray();
        var urineValues = grouped.Select(g => (double)g.Count(e => e.HasUrine == true)).ToArray();
        var stoolValues = grouped.Select(g => (double)g.Count(e => e.HasStool == true)).ToArray();

        // Grouped bar (나란히) — 소변+대변 동시 기록 가능하므로 Stacked보다 정확
        DailyBowelSeries = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Values = urineValues,
                Fill = new SolidColorPaint(UrineColor),
                Stroke = null,
                Name = "소변",
                MaxBarWidth = 18,
                YToolTipLabelFormatter = point => $"소변: {point.Coordinate.PrimaryValue:0}회"
            },
            new ColumnSeries<double>
            {
                Values = stoolValues,
                Fill = new SolidColorPaint(StoolColor),
                Stroke = null,
                Name = "대변",
                MaxBarWidth = 18,
                YToolTipLabelFormatter = point => $"대변: {point.Coordinate.PrimaryValue:0}회"
            }
        };

        DailyBowelXAxes = new Axis[]
        {
            new Axis
            {
                Labels = dates.Select(d => d.ToString("M/d")).ToArray(),
                LabelsPaint = new SolidColorPaint(LabelColor),
                SeparatorsPaint = new SolidColorPaint(GridColor),
                TextSize = 12,
                LabelsRotation = dates.Length > 14 ? 45 : 0
            }
        };
        DailyBowelYAxes = MakeYAxes();
    }

    // ── Chart 4: 1회 수유량 변화 추이 ──────────────────────
    private void BuildFeedAmountChart(List<BabyEvent> events)
    {
        var feedEvents = events
            .Where(e => e.IsFeeding && e.TotalFeedAmount > 0 && e.FullDateTime.HasValue)
            .OrderBy(e => e.FullDateTime)
            .ToList();

        var values = feedEvents.Select(e => (double)e.TotalFeedAmount).ToArray();
        var fullLabels = feedEvents.Select(e => e.FullDateTime!.Value.ToString("M/d H:mm")).ToArray();

        FeedAmountSeries = new ISeries[]
        {
            new LineSeries<double>
            {
                Values = values,
                Stroke = new SolidColorPaint(FeedColor, 2),
                Fill = null,
                GeometrySize = 6,
                GeometryStroke = new SolidColorPaint(FeedColor, 2),
                GeometryFill = new SolidColorPaint(FeedColor),
                Name = "수유량",
                YToolTipLabelFormatter = point =>
                {
                    var idx = (int)point.Index;
                    var timeLabel = idx >= 0 && idx < fullLabels.Length ? fullLabels[idx] : "";
                    return $"{timeLabel}  {point.Coordinate.PrimaryValue:0}ml";
                }
            }
        };

        // X축: 데이터 많으면 적절 간격으로 라벨 표시 (최대 20개)
        var maxLabels = 20;
        var step = Math.Max(1, fullLabels.Length / maxLabels);
        var axisLabels = fullLabels.Select((l, i) => i % step == 0 ? l : "").ToArray();

        FeedAmountXAxes = new Axis[]
        {
            new Axis
            {
                Labels = axisLabels.Length > 0 ? axisLabels : new[] { "" },
                LabelsPaint = new SolidColorPaint(LabelColor),
                SeparatorsPaint = new SolidColorPaint(GridColor),
                TextSize = 11,
                LabelsRotation = 45
            }
        };
        FeedAmountYAxes = MakeYAxes("ml");
    }

    // ── Chart 5: 카테고리별 이벤트 비율 ────────────────────
    private void BuildCategoryPieChart(List<BabyEvent> events)
    {
        var categoryColors = new Dictionary<EventCategory, SKColor>
        {
            { EventCategory.수유, SKColor.Parse("#1E8070") },
            { EventCategory.배변, SKColor.Parse("#B88020") },
            { EventCategory.위생관리, SKColor.Parse("#305898") },
            { EventCategory.신체측정, SKColor.Parse("#704890") },
            { EventCategory.건강관리, SKColor.Parse("#A04040") },
            { EventCategory.기타, SKColor.Parse("#506068") }
        };

        var categoryNames = new Dictionary<EventCategory, string>
        {
            { EventCategory.수유, "수유" },
            { EventCategory.배변, "배변" },
            { EventCategory.위생관리, "위생" },
            { EventCategory.신체측정, "신체" },
            { EventCategory.건강관리, "건강" },
            { EventCategory.기타, "기타" }
        };

        var grouped = events
            .GroupBy(e => e.Category)
            .Where(g => g.Any())
            .OrderByDescending(g => g.Count())
            .ToList();

        var series = new List<ISeries>();
        foreach (var g in grouped)
        {
            var color = categoryColors.GetValueOrDefault(g.Key, SKColor.Parse("#506068"));
            var name = categoryNames.GetValueOrDefault(g.Key, g.Key.ToString());
            var count = g.Count();
            series.Add(new PieSeries<double>
            {
                Values = new[] { (double)count },
                Fill = new SolidColorPaint(color),
                Name = name,
                DataLabelsPaint = new SolidColorPaint(LabelColor) { SKTypeface = KoreanTypeface },
                DataLabelsSize = 13,
                InnerRadius = 50,
                ToolTipLabelFormatter = point => $"{name}: {point.Coordinate.PrimaryValue:0}건 ({point.StackedValue?.Share ?? 0:P0})"
            });
        }

        CategoryPieSeries = series.ToArray();
    }

    // ── Chart 6: 일별 카테고리별 이벤트 비율 ─────────────────
    private void BuildDailyCategoryChart(List<BabyEvent> events)
    {
        var categoryColors = new Dictionary<EventCategory, SKColor>
        {
            { EventCategory.수유, SKColor.Parse("#1E8070") },
            { EventCategory.배변, SKColor.Parse("#B88020") },
            { EventCategory.위생관리, SKColor.Parse("#305898") },
            { EventCategory.신체측정, SKColor.Parse("#704890") },
            { EventCategory.건강관리, SKColor.Parse("#A04040") },
            { EventCategory.기타, SKColor.Parse("#506068") }
        };
        var categoryNames = new Dictionary<EventCategory, string>
        {
            { EventCategory.수유, "수유" },
            { EventCategory.배변, "배변" },
            { EventCategory.위생관리, "위생" },
            { EventCategory.신체측정, "신체" },
            { EventCategory.건강관리, "건강" },
            { EventCategory.기타, "기타" }
        };

        var grouped = events
            .GroupBy(e => e.Date.Date)
            .OrderBy(g => g.Key)
            .ToList();

        if (grouped.Count == 0)
        {
            DailyCategorySeries = Array.Empty<ISeries>();
            DailyCategoryXAxes = Array.Empty<Axis>();
            DailyCategoryYAxes = Array.Empty<Axis>();
            return;
        }

        var dates = grouped.Select(g => g.Key).ToArray();
        var seriesList = new List<ISeries>();

        foreach (var cat in categoryColors.Keys)
        {
            var values = grouped.Select(g => (double)g.Count(e => e.Category == cat)).ToArray();
            if (values.All(v => v == 0)) continue;

            var color = categoryColors[cat];
            seriesList.Add(new ColumnSeries<double>
            {
                Values = values,
                Fill = new SolidColorPaint(color),
                Stroke = null,
                Name = categoryNames[cat],
                MaxBarWidth = 16,
                YToolTipLabelFormatter = point => $"{categoryNames[cat]}: {point.Coordinate.PrimaryValue:0}건"
            });
        }

        DailyCategorySeries = seriesList.ToArray();
        DailyCategoryXAxes = new Axis[]
        {
            new Axis
            {
                Labels = dates.Select(d => d.ToString("M/d")).ToArray(),
                LabelsPaint = new SolidColorPaint(LabelColor) { SKTypeface = KoreanTypeface },
                SeparatorsPaint = new SolidColorPaint(GridColor),
                TextSize = 15,
                LabelsRotation = dates.Length > 14 ? 45 : 0
            }
        };
        DailyCategoryYAxes = MakeYAxes();
    }

    // ── Chart 7: 키 변화량 ─────────────────────────────────
    private static readonly SKColor BodyColor = SKColor.Parse("#704890");

    private void BuildHeightChart(List<BabyEvent> filtered)
    {
        var data = filtered
            .Where(e => e.Category == EventCategory.신체측정 && e.Detail != null && e.Detail.Contains("키") && e.FullDateTime.HasValue)
            .OrderBy(e => e.FullDateTime)
            .Select(e => {
                var m = System.Text.RegularExpressions.Regex.Match(e.Detail!, @"키\s*([\d.]+)");
                return m.Success ? (Date: e.FullDateTime!.Value, Val: double.Parse(m.Groups[1].Value)) : (Date: e.FullDateTime!.Value, Val: 0.0);
            })
            .Where(x => x.Val > 0)
            .ToList();

        // 항상 Series 생성 (빈 values여도) — LiveCharts2가 이전 렌더링을 갱신하도록
        HeightSeries = new ISeries[]
        {
            new LineSeries<double>
            {
                Values = data.Select(x => x.Val).ToArray(),
                Stroke = new SolidColorPaint(BodyColor, 2),
                Fill = null,
                GeometrySize = 8,
                GeometryStroke = new SolidColorPaint(BodyColor, 2),
                GeometryFill = new SolidColorPaint(BodyColor),
                Name = "키",
                YToolTipLabelFormatter = p =>
                {
                    var idx = (int)p.Index;
                    var label = idx >= 0 && idx < data.Count ? data[idx].Date.ToString("M/d") : "";
                    return $"{label}  {p.Coordinate.PrimaryValue:0.0}cm";
                }
            }
        };
        HeightXAxes = new Axis[]
        {
            new Axis
            {
                Labels = data.Count > 0 ? data.Select(x => x.Date.ToString("M/d")).ToArray() : new[] { "" },
                LabelsPaint = new SolidColorPaint(LabelColor) { SKTypeface = KoreanTypeface },
                SeparatorsPaint = new SolidColorPaint(GridColor),
                TextSize = 13
            }
        };
        HeightYAxes = MakeYAxes("cm");
    }

    // ── Chart 8: 몸무게 변화량 ────────────────────────────
    private void BuildWeightChart(List<BabyEvent> filtered)
    {
        var data = filtered
            .Where(e => e.Category == EventCategory.신체측정 && e.Detail != null && e.Detail.Contains("몸무게") && e.FullDateTime.HasValue)
            .OrderBy(e => e.FullDateTime)
            .Select(e => {
                var m = System.Text.RegularExpressions.Regex.Match(e.Detail!, @"몸무게\s*([\d.]+)");
                return m.Success ? (Date: e.FullDateTime!.Value, Val: double.Parse(m.Groups[1].Value)) : (Date: e.FullDateTime!.Value, Val: 0.0);
            })
            .Where(x => x.Val > 0)
            .ToList();

        // 항상 Series 생성 (빈 values여도) — LiveCharts2가 이전 렌더링을 갱신하도록
        WeightSeries = new ISeries[]
        {
            new LineSeries<double>
            {
                Values = data.Select(x => x.Val).ToArray(),
                Stroke = new SolidColorPaint(BodyColor, 2),
                Fill = null,
                GeometrySize = 8,
                GeometryStroke = new SolidColorPaint(BodyColor, 2),
                GeometryFill = new SolidColorPaint(BodyColor),
                Name = "몸무게",
                YToolTipLabelFormatter = p =>
                {
                    var idx = (int)p.Index;
                    var label = idx >= 0 && idx < data.Count ? data[idx].Date.ToString("M/d") : "";
                    return $"{label}  {p.Coordinate.PrimaryValue:0.00}kg";
                }
            }
        };
        WeightXAxes = new Axis[]
        {
            new Axis
            {
                Labels = data.Count > 0 ? data.Select(x => x.Date.ToString("M/d")).ToArray() : new[] { "" },
                LabelsPaint = new SolidColorPaint(LabelColor) { SKTypeface = KoreanTypeface },
                SeparatorsPaint = new SolidColorPaint(GridColor),
                TextSize = 13
            }
        };
        WeightYAxes = MakeYAxes("kg");
    }
}
