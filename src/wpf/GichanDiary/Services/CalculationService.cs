using GichanDiary.Models;
namespace GichanDiary.Services;

public class CalculationService : ICalculationService
{
    public int CalculateDayNumber(DateTime date, DateTime birthDate)
        => (date.Date - birthDate.Date).Days + 1;

    public TimeSpan? GetLastFeedingElapsed(List<BabyEvent> events)
    {
        var last = events.Where(e => e.IsFeeding && e.FullDateTime.HasValue)
            .OrderByDescending(e => e.FullDateTime).FirstOrDefault();
        return last?.FullDateTime.HasValue == true ? DateTime.Now - last.FullDateTime.Value : null;
    }

    public DateTime? GetNextExpectedFeed_Fixed(BabyEvent lastFeed, TimeSpan fixedInterval)
        => lastFeed.FullDateTime?.Add(fixedInterval);

    public DateTime? GetNextExpectedFeed_Average(List<BabyEvent> events, int recentCount)
    {
        var avg = GetAverageFeedingInterval(events, recentCount);
        var last = events.Where(e => e.IsFeeding && e.FullDateTime.HasValue)
            .OrderByDescending(e => e.FullDateTime).FirstOrDefault();
        if (avg == null || last?.FullDateTime == null) return null;
        return last.FullDateTime.Value.Add(avg.Value);
    }

    public double GetDailyFeedTotal(List<BabyEvent> events, DateTime date)
        => events.Where(e => e.IsFeeding && e.Date.Date == date.Date).Sum(e => e.TotalFeedAmount);

    public int GetDailyFeedCount(List<BabyEvent> events, DateTime date)
        => events.Count(e => e.IsFeeding && e.Date.Date == date.Date);

    public TimeSpan? GetAverageFeedingInterval(List<BabyEvent> events, int recentCount)
    {
        var feedings = events.Where(e => e.IsFeeding && e.FullDateTime.HasValue)
            .OrderBy(e => e.FullDateTime).ToList();
        if (feedings.Count < 2) return null;
        var recent = feedings.TakeLast(recentCount + 1).ToList();
        var intervals = new List<TimeSpan>();
        for (int i = 1; i < recent.Count; i++)
            intervals.Add(recent[i].FullDateTime!.Value - recent[i - 1].FullDateTime!.Value);
        if (intervals.Count == 0) return null;
        return TimeSpan.FromTicks((long)intervals.Average(t => t.Ticks));
    }

    public TimeSpan? GetLastUrineElapsed(List<BabyEvent> events)
    {
        var last = events.Where(e => e.Category == EventCategory.배변 && e.HasUrine == true && e.FullDateTime.HasValue)
            .OrderByDescending(e => e.FullDateTime).FirstOrDefault();
        return last?.FullDateTime.HasValue == true ? DateTime.Now - last.FullDateTime.Value : null;
    }

    public TimeSpan? GetLastStoolElapsed(List<BabyEvent> events)
    {
        var last = events.Where(e => e.Category == EventCategory.배변 && e.HasStool == true && e.FullDateTime.HasValue)
            .OrderByDescending(e => e.FullDateTime).FirstOrDefault();
        return last?.FullDateTime.HasValue == true ? DateTime.Now - last.FullDateTime.Value : null;
    }

    /// <summary>
    /// 수유 이벤트의 수유텀(FeedingInterval)을 일괄 계산.
    /// 시간순 정렬 후 각 수유 이벤트의 직전 수유와의 시간 간격을 설정.
    /// 최초 수유 이벤트는 수유텀 없음 (null).
    /// </summary>
    public void CalculateFeedingIntervals(List<BabyEvent> events)
    {
        var feedings = events
            .Where(e => e.IsFeeding && e.FullDateTime.HasValue)
            .OrderBy(e => e.FullDateTime)
            .ToList();

        for (int i = 0; i < feedings.Count; i++)
        {
            if (i == 0)
            {
                feedings[i].FeedingInterval = null;
            }
            else
            {
                feedings[i].FeedingInterval = feedings[i].FullDateTime!.Value - feedings[i - 1].FullDateTime!.Value;
            }
        }
    }

    public DailySummary GetDailySummary(List<BabyEvent> events, DateTime date)
    {
        var dayEvents = events.Where(e => e.Date.Date == date.Date).ToList();
        var feedings = dayEvents.Where(e => e.IsFeeding).ToList();
        var bowels = dayEvents.Where(e => e.Category == EventCategory.배변).ToList();
        return new DailySummary
        {
            Date = date.Date,
            TotalFeedAmount = feedings.Sum(e => e.TotalFeedAmount),
            FormulaCount = feedings.Count(e => e.FormulaAmount > 0),
            BreastfeedCount = feedings.Count(e => e.BreastfeedAmount > 0),
            UrineCount = bowels.Count(e => e.HasUrine == true),
            StoolCount = bowels.Count(e => e.HasStool == true),
            AvgFeedingInterval = GetAverageFeedingInterval(events, 10),
        };
    }
}
