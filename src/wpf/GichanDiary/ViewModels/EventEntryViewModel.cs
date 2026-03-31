using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GichanDiary.Models;
using GichanDiary.Services;

namespace GichanDiary.ViewModels;

public partial class EventEntryViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private readonly ICalculationService _calcService;
    private readonly ISettingsService _settingsService;
    private readonly AppSettings _settings;

    public event Action? EventSaved;

    // ── Date / Time ───────────────────────────────────────

    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
    [ObservableProperty] private int _selectedHour = DateTime.Now.Hour;
    [ObservableProperty] private int _selectedMinute = DateTime.Now.Minute;
    [ObservableProperty] private string _calculatedDayDisplay = "";

    // ── Category ──────────────────────────────────────────

    [ObservableProperty] private EventCategory _selectedCategory = EventCategory.수유;

    // ── Feeding ───────────────────────────────────────────

    [ObservableProperty] private bool _isFormulaEnabled = true;
    [ObservableProperty] private bool _isBreastfeedEnabled;
    [ObservableProperty] private string? _selectedFormulaProduct;
    [ObservableProperty] private int _formulaAmount;
    [ObservableProperty] private int _breastfeedAmount = 20;
    [ObservableProperty] private int _feedingCount = 1;

    public ObservableCollection<string> FormulaProducts { get; } = new();
    public List<string> FormulaProductOptions { get; } = new();

    // ComboBox option lists
    public List<string> HourOptions { get; } = Enumerable.Range(0, 24).Select(h => h.ToString("D2")).ToList();
    public List<string> MinuteOptions { get; } = Enumerable.Range(0, 60).Select(m => m.ToString("D2")).ToList();
    public List<int> FormulaAmountOptions { get; } = Enumerable.Range(8, 33).Select(i => i * 5).ToList(); // 40,45,50...200
    public List<int> BreastfeedAmountOptions { get; } = Enumerable.Range(2, 5).Select(i => i * 5).ToList(); // 10,15,20,25,30

    // ── Bowel ─────────────────────────────────────────────

    [ObservableProperty] private bool _hasUrine;
    [ObservableProperty] private bool _hasStool;
    [ObservableProperty] private bool _immediateNotice;

    // ── Hygiene ───────────────────────────────────────────

    [ObservableProperty] private bool _hygieneShower;       // 샤워
    [ObservableProperty] private bool _hygieneFaceWash;     // 세안
    [ObservableProperty] private bool _hygieneNails;        // 손발톱정리
    [ObservableProperty] private bool _hygieneNose;         // 코청소
    [ObservableProperty] private bool _hygieneEyeCrust;     // 눈꼽청소
    [ObservableProperty] private bool _hygieneMouth;        // 입안청소
    [ObservableProperty] private bool _hygieneNavel;        // 배꼽청소
    [ObservableProperty] private bool _hygieneOther;        // 기타

    // ── Body ──────────────────────────────────────────────

    [ObservableProperty] private string _heightCm = "";
    [ObservableProperty] private string _weightKg = "";
    [ObservableProperty] private string _headCircCm = "";

    // ── Health ────────────────────────────────────────────

    [ObservableProperty] private string _healthType = "";

    // ── Common ────────────────────────────────────────────

    [ObservableProperty] private string _memo = "";

    // ── Auto calc display ─────────────────────────────────

    [ObservableProperty] private string _feedingIntervalDisplay = "";
    [ObservableProperty] private string _dailyFeedTotalDisplay = "";

    // ── Direct edit mode ───────────────────────────────────

    [ObservableProperty] private bool _isFormulaAmountEditing;
    [ObservableProperty] private bool _isBreastfeedAmountEditing;
    // FeedingCount editing removed — only +/- buttons

    // ── Edit mode ──────────────────────────────────────────

    [ObservableProperty] private bool _isEditMode;
    [ObservableProperty] private BabyEvent? _editingEvent;

    // ── Status ────────────────────────────────────────────

    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isSaving;

    // ══ Constructor ═══════════════════════════════════════

    public EventEntryViewModel(
        IDataService dataService,
        ICalculationService calcService,
        ISettingsService settingsService)
    {
        _dataService = dataService;
        _calcService = calcService;
        _settingsService = settingsService;
        _settings = settingsService.Load();

        // Load formula products
        foreach (var product in _settings.FormulaProducts)
            FormulaProducts.Add(product);

        FormulaProductOptions = _settings.FormulaProducts.ToList();

        if (_settings.FormulaProducts.Count > 0)
            SelectedFormulaProduct = _settings.DefaultFormulaProduct ?? _settings.FormulaProducts[0];

        // Default amounts from settings
        _formulaAmount = _settings.DefaultFormulaAmount;
        _breastfeedAmount = _settings.DefaultBreastfeedAmount;
        _isFormulaEnabled = true;
        _isBreastfeedEnabled = false;

        // Calculate day display
        RecalculateDayDisplay();
    }

    // ══ Partial property change handlers ══════════════════

    partial void OnSelectedDateChanged(DateTime value) => RecalculateDayDisplay();

    // ══ Category command ══════════════════════════════════

    [RelayCommand]
    private void SelectCategory(EventCategory cat)
    {
        SelectedCategory = cat;
        ResetSubFields();
    }

    private void ResetSubFields()
    {
        // Feeding
        IsFormulaEnabled = true;
        IsBreastfeedEnabled = false;
        FormulaAmount = _settings.DefaultFormulaAmount;
        BreastfeedAmount = _settings.DefaultBreastfeedAmount;
        FeedingCount = 1;
        if (_settings.FormulaProducts.Count > 0)
            SelectedFormulaProduct = _settings.DefaultFormulaProduct ?? _settings.FormulaProducts[0];

        // Bowel
        HasUrine = false;
        HasStool = false;
        ImmediateNotice = false;

        // Hygiene
        HygieneShower = false;
        HygieneFaceWash = false;
        HygieneNails = false;
        HygieneNose = false;
        HygieneEyeCrust = false;
        HygieneMouth = false;
        HygieneNavel = false;
        HygieneOther = false;

        // Body
        HeightCm = "";
        WeightKg = "";
        HeadCircCm = "";

        // Health
        HealthType = "";

        // Common
        Memo = "";
    }

    // ══ Time spinner commands ══════════════════════════════

    [RelayCommand]
    private void IncrementHour() => SelectedHour = (SelectedHour + 1) % 24;
    [RelayCommand]
    private void DecrementHour() => SelectedHour = (SelectedHour + 23) % 24;
    [RelayCommand]
    private void IncrementMinute() => SelectedMinute = (SelectedMinute + 1) % 60;
    [RelayCommand]
    private void DecrementMinute() => SelectedMinute = (SelectedMinute + 59) % 60;

    // ══ Spinner commands ══════════════════════════════════

    [RelayCommand]
    private void IncrementFormulaAmount()
    {
        if (FormulaAmount < 200) FormulaAmount += 5;
    }

    [RelayCommand]
    private void DecrementFormulaAmount()
    {
        if (FormulaAmount > 40) FormulaAmount -= 5;
    }

    [RelayCommand]
    private void IncrementBreastfeedAmount()
    {
        if (BreastfeedAmount < 30) BreastfeedAmount += 5;
    }

    [RelayCommand]
    private void DecrementBreastfeedAmount()
    {
        if (BreastfeedAmount > 10) BreastfeedAmount -= 5;
    }

    [RelayCommand]
    private void IncrementFeedingCount()
    {
        if (FeedingCount < 5) FeedingCount++;
    }

    [RelayCommand]
    private void DecrementFeedingCount()
    {
        if (FeedingCount > 1) FeedingCount--;
    }

    [RelayCommand]
    private void SelectFormulaProduct(string product)
    {
        SelectedFormulaProduct = product;
    }

    // ══ Direct edit commands ═══════════════════════════════

    [RelayCommand]
    private void StartEditFormulaAmount() => IsFormulaAmountEditing = true;

    [RelayCommand]
    private void StartEditBreastfeedAmount() => IsBreastfeedAmountEditing = true;

    [RelayCommand]
    private void ConfirmEditFormulaAmount()
    {
        FormulaAmount = Math.Clamp(FormulaAmount, 40, 200);
        IsFormulaAmountEditing = false;
    }

    [RelayCommand]
    private void ConfirmEditBreastfeedAmount()
    {
        BreastfeedAmount = Math.Clamp(BreastfeedAmount, 10, 30);
        IsBreastfeedAmountEditing = false;
    }

    // ══ Load for edit ═══════════════════════════════════════

    public void LoadForEdit(BabyEvent evt)
    {
        IsEditMode = true;
        EditingEvent = evt;

        SelectedDate = evt.Date;
        if (evt.Time.HasValue)
        {
            SelectedHour = evt.Time.Value.Hours;
            SelectedMinute = evt.Time.Value.Minutes;
        }

        SelectedCategory = evt.Category;

        switch (evt.Category)
        {
            case EventCategory.수유:
                IsFormulaEnabled = evt.FormulaAmount.HasValue && evt.FormulaAmount.Value > 0;
                IsBreastfeedEnabled = evt.BreastfeedAmount.HasValue && evt.BreastfeedAmount.Value > 0;
                SelectedFormulaProduct = evt.FormulaProduct;
                FormulaAmount = evt.FormulaAmount ?? _settings.DefaultFormulaAmount;
                BreastfeedAmount = evt.BreastfeedAmount ?? _settings.DefaultBreastfeedAmount;
                FeedingCount = evt.FeedingCount ?? 1;
                break;

            case EventCategory.배변:
                HasUrine = evt.HasUrine == true;
                HasStool = evt.HasStool == true;
                ImmediateNotice = evt.ImmediateNotice == true;
                break;

            case EventCategory.위생관리:
                var detail = evt.Detail ?? "";
                HygieneShower = detail.Contains("샤워");
                HygieneFaceWash = detail.Contains("세안");
                HygieneNails = detail.Contains("손발톱정리");
                HygieneNose = detail.Contains("코청소");
                HygieneEyeCrust = detail.Contains("눈꼽청소");
                HygieneMouth = detail.Contains("입안청소");
                HygieneNavel = detail.Contains("배꼽청소");
                HygieneOther = detail.Contains("기타");
                break;

            case EventCategory.신체측정:
                var bodyDetail = evt.Detail ?? "";
                var heightMatch = System.Text.RegularExpressions.Regex.Match(bodyDetail, @"키\s*([\d.]+)cm");
                var weightMatch = System.Text.RegularExpressions.Regex.Match(bodyDetail, @"몸무게\s*([\d.]+)kg");
                var headMatch = System.Text.RegularExpressions.Regex.Match(bodyDetail, @"머리둘레\s*([\d.]+)cm");
                HeightCm = heightMatch.Success ? heightMatch.Groups[1].Value : "";
                WeightKg = weightMatch.Success ? weightMatch.Groups[1].Value : "";
                HeadCircCm = headMatch.Success ? headMatch.Groups[1].Value : "";
                break;

            case EventCategory.건강관리:
                HealthType = evt.Detail ?? "";
                break;
        }

        Memo = evt.Note ?? "";
    }

    // ══ Save / Reset ══════════════════════════════════════

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsSaving) return;

        try
        {
            IsSaving = true;
            StatusMessage = "";

            var evt = BuildBabyEvent();

            if (string.IsNullOrEmpty(_settings.ExcelFilePath))
            {
                StatusMessage = "엑셀 파일 경로가 설정되지 않았습니다.";
                return;
            }

            if (IsEditMode && EditingEvent != null)
            {
                evt.ExcelRowIndex = EditingEvent.ExcelRowIndex;
                evt.Id = EditingEvent.Id;
                await _dataService.UpdateEventAsync(evt);
                StatusMessage = "수정 완료!";
                Services.LogService.Event($"기록 수정: {evt.Category} {evt.Detail} {evt.Amount}");
            }
            else
            {
                await _dataService.AddEventAsync(evt);
                StatusMessage = "저장 완료!";
                Services.LogService.Event($"기록 추가: {evt.Category} {evt.Detail} {evt.Amount}");
            }

            EventSaved?.Invoke();

            // Reset form and edit mode after save
            IsEditMode = false;
            EditingEvent = null;
            ResetForm();
        }
        catch (System.IO.IOException)
        {
            StatusMessage = "저장 실패: Excel 파일이 다른 프로그램에서 열려 있습니다. 파일을 닫고 다시 시도하세요.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"저장 실패: {ex.Message}";
            Services.LogService.System($"기록 저장 실패: {ex.Message}");
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void Reset()
    {
        ResetForm();
        StatusMessage = "";
    }

    // ══ Helpers ═══════════════════════════════════════════

    private void RecalculateDayDisplay()
    {
        if (_settings.BabyBirthDate.HasValue)
        {
            var dayNum = _calcService.CalculateDayNumber(SelectedDate, _settings.BabyBirthDate.Value);
            CalculatedDayDisplay = $"{dayNum}일차";
        }
        else
        {
            CalculatedDayDisplay = "";
        }
    }

    private BabyEvent BuildBabyEvent()
    {
        var evt = new BabyEvent
        {
            Date = SelectedDate,
            Time = new TimeSpan(SelectedHour, SelectedMinute, 0),
            Category = SelectedCategory,
            Note = Memo
        };

        if (_settings.BabyBirthDate.HasValue)
            evt.DayNumber = _calcService.CalculateDayNumber(SelectedDate, _settings.BabyBirthDate.Value);

        switch (SelectedCategory)
        {
            case EventCategory.수유:
                var formulaAmt = IsFormulaEnabled ? FormulaAmount : 0;
                var breastAmt = IsBreastfeedEnabled ? BreastfeedAmount : 0;
                evt.FormulaProduct = IsFormulaEnabled ? SelectedFormulaProduct : null;
                evt.FormulaAmount = formulaAmt > 0 ? formulaAmt : null;
                evt.BreastfeedAmount = breastAmt > 0 ? breastAmt : null;
                evt.FeedingCount = FeedingCount;
                // 세부내용: 분유+모유 → "제품명+모유", 모유만 → "모유", 분유만 → 제품명
                if (formulaAmt > 0 && breastAmt > 0)
                    evt.Detail = $"{SelectedFormulaProduct}+모유";
                else if (breastAmt > 0)
                    evt.Detail = "모유";
                else
                    evt.Detail = SelectedFormulaProduct ?? "분유";
                evt.Amount = $"{formulaAmt + breastAmt}ml";
                break;

            case EventCategory.배변:
                evt.HasUrine = HasUrine;
                evt.HasStool = HasStool;
                evt.ImmediateNotice = ImmediateNotice;
                var parts = new List<string>();
                if (HasUrine) parts.Add("소변");
                if (HasStool) parts.Add("대변");
                evt.Detail = string.Join("+", parts);
                if (ImmediateNotice) evt.Detail += "(직후)";
                evt.Amount = "-";
                break;

            case EventCategory.위생관리:
                var hygieneItems = GetSelectedHygieneTypes();
                evt.Detail = string.Join(", ", hygieneItems.Select(h => h.ToString()));
                evt.Amount = "-";
                break;

            case EventCategory.신체측정:
                var bodyParts = new List<string>();
                if (decimal.TryParse(HeightCm, out var h)) bodyParts.Add($"키 {h}cm");
                if (decimal.TryParse(WeightKg, out var w)) bodyParts.Add($"몸무게 {w}kg");
                if (decimal.TryParse(HeadCircCm, out var hc)) bodyParts.Add($"머리둘레 {hc}cm");
                evt.Detail = string.Join(", ", bodyParts);
                evt.Amount = "-";
                break;

            case EventCategory.건강관리:
                evt.Detail = HealthType;
                evt.Amount = "-";
                break;

            case EventCategory.기타:
                evt.Detail = "기타";
                evt.Amount = "-";
                break;
        }

        return evt;
    }

    private List<HygieneType> GetSelectedHygieneTypes()
    {
        var list = new List<HygieneType>();
        if (HygieneShower) list.Add(HygieneType.샤워);
        if (HygieneFaceWash) list.Add(HygieneType.세안);
        if (HygieneNails) list.Add(HygieneType.손발톱정리);
        if (HygieneNose) list.Add(HygieneType.코청소);
        if (HygieneEyeCrust) list.Add(HygieneType.눈꼽청소);
        if (HygieneMouth) list.Add(HygieneType.입안청소);
        if (HygieneNavel) list.Add(HygieneType.배꼽청소);
        if (HygieneOther) list.Add(HygieneType.기타);
        return list;
    }

    private void ResetForm()
    {
        SelectedDate = DateTime.Today;
        SelectedHour = DateTime.Now.Hour;
        SelectedMinute = DateTime.Now.Minute;
        SelectedCategory = EventCategory.수유;
        ResetSubFields();
    }
}
