using CommunityToolkit.Mvvm.ComponentModel;

namespace GichanDiary.Models;

public partial class BabyEvent : ObservableObject
{
    [ObservableProperty] private Guid _id = Guid.NewGuid();
    [ObservableProperty] private int _excelRowIndex;
    [ObservableProperty] private int? _dayNumber;
    [ObservableProperty] private DateTime _date;
    [ObservableProperty] private TimeSpan? _time;
    [ObservableProperty] private EventCategory _category;
    [ObservableProperty] private string _detail = string.Empty;
    [ObservableProperty] private string _amount = "-";
    [ObservableProperty] private string _note = string.Empty;
    [ObservableProperty] private TimeSpan? _feedingInterval;
    [ObservableProperty] private TimeSpan? _nextExpected;
    [ObservableProperty] private double? _dailyFeedTotal;
    // Extended fields (K~Q)
    [ObservableProperty] private string? _formulaProduct;
    [ObservableProperty] private int? _formulaAmount;
    [ObservableProperty] private int? _breastfeedAmount;  // 유축 모유 수유량(ml), 10~30, 5단위
    [ObservableProperty] private int? _feedingCount;
    [ObservableProperty] private bool? _hasUrine;
    [ObservableProperty] private bool? _hasStool;
    [ObservableProperty] private bool? _immediateNotice;

    public DateTime? FullDateTime => Time.HasValue ? Date.Add(Time.Value) : Date;
    public bool IsFeeding => Category == EventCategory.수유;
    public int TotalFeedAmount => (FormulaAmount ?? 0) + (BreastfeedAmount ?? 0);
}
