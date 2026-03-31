using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GichanDiary.Models;
using GichanDiary.Services;

namespace GichanDiary.ViewModels;

public partial class EventListViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private readonly ICalculationService _calcService;
    private readonly ISettingsService _settingsService;
    private readonly AppSettings _settings;

    private List<BabyEvent> _allEvents = new();
    private List<BabyEvent> _filteredAll = new();

    public event Action<BabyEvent>? EditRequested;
    public event Action? DataChanged;

    // ── Filter properties ───────────────────────────────
    [ObservableProperty] private DateTime _filterStartDate = DateTime.Today.AddDays(-7);
    [ObservableProperty] private DateTime _filterEndDate = DateTime.Today;
    [ObservableProperty] private bool _filterFeed = true;
    [ObservableProperty] private bool _filterBowel = true;
    [ObservableProperty] private bool _filterHygiene = true;
    [ObservableProperty] private bool _filterBody = true;
    [ObservableProperty] private bool _filterHealth = true;
    [ObservableProperty] private bool _filterEtc = true;
    [ObservableProperty] private string _searchKeyword = "";
    [ObservableProperty] private string _sortOrder = "desc";
    [ObservableProperty] private string _sortButtonText = "최신순 ▼";

    // ── Pagination ──────────────────────────────────────
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _pageSize = 30;

    // ── Display ─────────────────────────────────────────
    public ObservableCollection<BabyEvent> FilteredEvents { get; } = new();

    // ── Selected item ───────────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditEventCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteEventCommand))]
    private BabyEvent? _selectedEvent;

    // ── Instant filter on property change ────────────────
    partial void OnFilterStartDateChanged(DateTime value) => ApplyFilter();
    partial void OnFilterEndDateChanged(DateTime value) => ApplyFilter();
    partial void OnFilterFeedChanged(bool value) => ApplyFilter();
    partial void OnFilterBowelChanged(bool value) => ApplyFilter();
    partial void OnFilterHygieneChanged(bool value) => ApplyFilter();
    partial void OnFilterBodyChanged(bool value) => ApplyFilter();
    partial void OnFilterHealthChanged(bool value) => ApplyFilter();
    partial void OnFilterEtcChanged(bool value) => ApplyFilter();
    partial void OnSearchKeywordChanged(string value) => ApplyFilter();

    public EventListViewModel(IDataService dataService, ICalculationService calcService, ISettingsService settingsService)
    {
        _dataService = dataService;
        _calcService = calcService;
        _settingsService = settingsService;
        _settings = settingsService.Load();
        _pageSize = _settings.PageSize;
    }

    public void Refresh(List<BabyEvent> events)
    {
        _allEvents = events;

        // Fill missing DayNumbers from BirthDate
        var settings = _settingsService.Load();
        if (settings.BabyBirthDate.HasValue)
        {
            foreach (var evt in _allEvents)
            {
                if (evt.DayNumber == null || evt.DayNumber == 0)
                    evt.DayNumber = _calcService.CalculateDayNumber(evt.Date, settings.BabyBirthDate.Value);
            }
        }

        ApplyFilter();
    }

    [RelayCommand]
    private void ApplyFilter()
    {
        var categories = new List<EventCategory>();
        if (FilterFeed) categories.Add(EventCategory.수유);
        if (FilterBowel) categories.Add(EventCategory.배변);
        if (FilterHygiene) categories.Add(EventCategory.위생관리);
        if (FilterBody) categories.Add(EventCategory.신체측정);
        if (FilterHealth) categories.Add(EventCategory.건강관리);
        if (FilterEtc) categories.Add(EventCategory.기타);

        var filtered = _allEvents
            .Where(e => e.Date >= FilterStartDate.Date && e.Date <= FilterEndDate.Date)
            .Where(e => categories.Contains(e.Category));

        if (!string.IsNullOrWhiteSpace(SearchKeyword))
        {
            var kw = SearchKeyword.Trim();
            filtered = filtered.Where(e =>
                (e.Detail?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (e.Note?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (e.Amount?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true));
        }

        _filteredAll = SortOrder == "desc"
            ? filtered.OrderByDescending(e => e.FullDateTime).ToList()
            : filtered.OrderBy(e => e.FullDateTime).ToList();

        TotalCount = _filteredAll.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
        CurrentPage = Math.Min(CurrentPage, TotalPages);
        if (CurrentPage < 1) CurrentPage = 1;

        UpdatePage();
    }

    private void UpdatePage()
    {
        FilteredEvents.Clear();
        var pageItems = _filteredAll
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize);
        foreach (var item in pageItems)
            FilteredEvents.Add(item);
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            UpdatePage();
        }
    }

    [RelayCommand]
    private void PrevPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            UpdatePage();
        }
    }

    [RelayCommand]
    private void ToggleSort()
    {
        SortOrder = SortOrder == "desc" ? "asc" : "desc";
        SortButtonText = SortOrder == "desc" ? "최신순 ▼" : "오래된순 ▲";
        ApplyFilter();
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    private void EditEvent(BabyEvent? ev)
    {
        if (ev != null)
            EditRequested?.Invoke(ev);
    }

    private bool CanEditOrDelete(BabyEvent? ev) => ev != null;

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    private async Task DeleteEvent(BabyEvent? ev)
    {
        if (ev == null) return;

        var result = System.Windows.MessageBox.Show(
            $"{ev.FullDateTime:yyyy/MM/dd HH:mm} {ev.Detail} 기록을 삭제하시겠습니까?",
            "삭제 확인",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        if (!string.IsNullOrEmpty(_settings.ExcelFilePath))
        {
            try
            {
                await _dataService.DeleteEventAsync(ev);
                Services.LogService.Event($"기록 삭제: {ev.FullDateTime:yyyy/MM/dd HH:mm} {ev.Detail}");
                _allEvents.Remove(ev);
                ApplyFilter();
                DataChanged?.Invoke();
            }
            catch (System.IO.IOException)
            {
                System.Windows.MessageBox.Show(
                    "Excel 파일이 다른 프로그램에서 열려 있습니다.\n파일을 닫고 다시 시도하세요.",
                    "삭제 실패", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                Services.LogService.System($"기록 삭제 실패: {ex.Message}");
                System.Windows.MessageBox.Show(
                    $"삭제 실패: {ex.Message}",
                    "오류", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
