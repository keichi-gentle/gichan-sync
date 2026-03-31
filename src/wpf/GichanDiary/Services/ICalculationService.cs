using GichanDiary.Models;
namespace GichanDiary.Services;

public interface ICalculationService
{
    int CalculateDayNumber(DateTime date, DateTime birthDate);
    TimeSpan? GetLastFeedingElapsed(List<BabyEvent> events);
    DateTime? GetNextExpectedFeed_Fixed(BabyEvent lastFeed, TimeSpan fixedInterval);
    DateTime? GetNextExpectedFeed_Average(List<BabyEvent> events, int recentCount);
    double GetDailyFeedTotal(List<BabyEvent> events, DateTime date);
    int GetDailyFeedCount(List<BabyEvent> events, DateTime date);
    TimeSpan? GetAverageFeedingInterval(List<BabyEvent> events, int recentCount);
    TimeSpan? GetLastUrineElapsed(List<BabyEvent> events);
    TimeSpan? GetLastStoolElapsed(List<BabyEvent> events);
    DailySummary GetDailySummary(List<BabyEvent> events, DateTime date);
    void CalculateFeedingIntervals(List<BabyEvent> events);
}
