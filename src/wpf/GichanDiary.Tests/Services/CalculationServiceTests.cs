using GichanDiary.Models;
using GichanDiary.Services;
namespace GichanDiary.Tests.Services;

public class CalculationServiceTests
{
    private readonly CalculationService _svc = new();

    private static BabyEvent Feed(DateTime date, int h, int m, int amount) => new()
    {
        Date = date, Time = new TimeSpan(h, m, 0),
        Category = EventCategory.수유, FormulaAmount = amount
    };

    private static BabyEvent Bowel(DateTime date, int h, bool urine, bool stool) => new()
    {
        Date = date, Time = new TimeSpan(h, 0, 0),
        Category = EventCategory.배변, HasUrine = urine, HasStool = stool
    };

    [Fact]
    public void CalculateDayNumber_BirthDay_Returns1()
    {
        Assert.Equal(1, _svc.CalculateDayNumber(new DateTime(2026, 2, 27), new DateTime(2026, 2, 27)));
    }

    [Fact]
    public void CalculateDayNumber_OneWeekLater_Returns8()
    {
        Assert.Equal(8, _svc.CalculateDayNumber(new DateTime(2026, 3, 6), new DateTime(2026, 2, 27)));
    }

    [Fact]
    public void GetDailyFeedTotal_SumsCorrectDate()
    {
        var d = new DateTime(2026, 3, 24);
        var events = new List<BabyEvent>
        {
            Feed(d, 1, 0, 80), Feed(d, 4, 0, 90), Feed(d, 7, 0, 100),
            Feed(d.AddDays(-1), 22, 0, 50),
        };
        Assert.Equal(270, _svc.GetDailyFeedTotal(events, d));
    }

    [Fact]
    public void GetDailyFeedCount_CountsCorrectDate()
    {
        var d = new DateTime(2026, 3, 24);
        var events = new List<BabyEvent> { Feed(d, 1, 0, 80), Feed(d, 4, 0, 90), Feed(d, 7, 0, 100) };
        Assert.Equal(3, _svc.GetDailyFeedCount(events, d));
    }

    [Fact]
    public void GetAverageFeedingInterval_ReturnsAvg()
    {
        var d = new DateTime(2026, 3, 24);
        var events = new List<BabyEvent>
        {
            Feed(d, 1, 0, 80), Feed(d, 4, 0, 90), Feed(d, 7, 30, 100),
        };
        var avg = _svc.GetAverageFeedingInterval(events, 10);
        Assert.NotNull(avg);
        Assert.Equal(TimeSpan.FromMinutes(195), avg); // (3h + 3h30m) / 2 = 3h15m
    }

    [Fact]
    public void GetAverageFeedingInterval_SingleEvent_ReturnsNull()
    {
        var events = new List<BabyEvent> { Feed(new DateTime(2026, 3, 24), 1, 0, 80) };
        Assert.Null(_svc.GetAverageFeedingInterval(events, 10));
    }

    [Fact]
    public void GetNextExpectedFeed_Fixed_AddsInterval()
    {
        var last = Feed(new DateTime(2026, 3, 24), 17, 0, 80);
        Assert.Equal(new DateTime(2026, 3, 24, 20, 0, 0),
            _svc.GetNextExpectedFeed_Fixed(last, TimeSpan.FromHours(3)));
    }

    [Fact]
    public void GetDailySummary_AggregatesCorrectly()
    {
        var d = new DateTime(2026, 3, 24);
        var events = new List<BabyEvent>
        {
            Feed(d, 1, 0, 80), Feed(d, 4, 0, 90),
            Bowel(d, 2, true, false), Bowel(d, 5, true, true),
        };
        var s = _svc.GetDailySummary(events, d);
        Assert.Equal(170, s.TotalFeedAmount);
        Assert.Equal(2, s.UrineCount);
        Assert.Equal(1, s.StoolCount);
    }
}
