using GichanDiary.Models;

namespace GichanDiary.Tests.Models;

public class BabyEventTests
{
    [Fact]
    public void TotalFeedAmount_FormulaOnly_ReturnsFormulaAmount()
    {
        var e = new BabyEvent { FormulaAmount = 80 };
        Assert.Equal(80, e.TotalFeedAmount);
    }

    [Fact]
    public void TotalFeedAmount_FormulaAndBreastfeed_ReturnsSum()
    {
        var e = new BabyEvent { FormulaAmount = 70, BreastfeedAmount = 20 };
        Assert.Equal(90, e.TotalFeedAmount);
    }

    [Fact]
    public void TotalFeedAmount_BreastfeedOnly_ReturnsBreastfeedAmount()
    {
        var e = new BabyEvent { BreastfeedAmount = 25 };
        Assert.Equal(25, e.TotalFeedAmount);
    }

    [Fact]
    public void TotalFeedAmount_NoAmounts_ReturnsZero()
    {
        var e = new BabyEvent();
        Assert.Equal(0, e.TotalFeedAmount);
    }

    [Fact]
    public void IsFeeding_WhenCategory수유_ReturnsTrue()
    {
        var e = new BabyEvent { Category = EventCategory.수유 };
        Assert.True(e.IsFeeding);
    }

    [Fact]
    public void IsFeeding_WhenCategory배변_ReturnsFalse()
    {
        var e = new BabyEvent { Category = EventCategory.배변 };
        Assert.False(e.IsFeeding);
    }

    [Fact]
    public void FullDateTime_WithTime_CombinesDateAndTime()
    {
        var e = new BabyEvent { Date = new DateTime(2026, 3, 24), Time = new TimeSpan(17, 30, 0) };
        Assert.Equal(new DateTime(2026, 3, 24, 17, 30, 0), e.FullDateTime);
    }
}
