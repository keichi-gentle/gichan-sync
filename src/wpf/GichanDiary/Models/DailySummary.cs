namespace GichanDiary.Models;

public class DailySummary
{
    public DateTime Date { get; set; }
    public int DayNumber { get; set; }
    public int FormulaCount { get; set; }
    public int BreastfeedCount { get; set; }
    public double TotalFeedAmount { get; set; }
    public int UrineCount { get; set; }
    public int StoolCount { get; set; }
    public TimeSpan? AvgFeedingInterval { get; set; }
}
