using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GichanDiary.Models;
using GichanDiary.Services;

namespace GichanDiary.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;

    // ── File settings ───────────────────────────────────
    [ObservableProperty] private string _excelFilePath = "";
    [ObservableProperty] private bool _autoBackupEnabled = true;

    // ── Baby info ───────────────────────────────────────
    [ObservableProperty] private string _babyName = "";
    [ObservableProperty] private DateTime? _babyBirthDate;

    // ── Formula products ────────────────────────────────
    public ObservableCollection<string> FormulaProducts { get; } = new();
    [ObservableProperty] private string _newProductName = "";
    [ObservableProperty] private string _defaultFormulaProduct = "";

    // ── Feeding settings ────────────────────────────────
    [ObservableProperty] private int _fixedFeedingHours = 3;
    [ObservableProperty] private int _fixedFeedingMinutes;
    [ObservableProperty] private int _averageFeedingCount = 10;
    [ObservableProperty] private int _defaultBreastfeedAmount = 20;
    [ObservableProperty] private int _defaultFormulaAmount = 100;

    // ── Display settings ────────────────────────────────
    [ObservableProperty] private int _pageSize = 30;

    // ── Status message ──────────────────────────────────
    [ObservableProperty] private string _statusMessage = "";

    public event Action? SettingsSaved;

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        var s = _settingsService.Load();
        ExcelFilePath = s.ExcelFilePath ?? "";
        AutoBackupEnabled = s.AutoBackupEnabled;
        BabyName = s.BabyName ?? "";
        BabyBirthDate = s.BabyBirthDate;
        DefaultFormulaProduct = s.DefaultFormulaProduct;
        FixedFeedingHours = (int)s.FixedFeedingInterval.TotalHours;
        FixedFeedingMinutes = s.FixedFeedingInterval.Minutes;
        AverageFeedingCount = s.AverageFeedingCount;
        PageSize = s.PageSize;
        DefaultBreastfeedAmount = s.DefaultBreastfeedAmount;
        DefaultFormulaAmount = s.DefaultFormulaAmount;

        FormulaProducts.Clear();
        foreach (var p in s.FormulaProducts)
            FormulaProducts.Add(p);
    }

    [RelayCommand]
    private void Save()
    {
        var s = new AppSettings
        {
            ExcelFilePath = ExcelFilePath,
            AutoBackupEnabled = AutoBackupEnabled,
            BabyName = BabyName,
            BabyBirthDate = BabyBirthDate,
            FormulaProducts = FormulaProducts.ToList(),
            DefaultFormulaProduct = DefaultFormulaProduct,
            FixedFeedingInterval = new TimeSpan(FixedFeedingHours, FixedFeedingMinutes, 0),
            AverageFeedingCount = AverageFeedingCount,
            PageSize = PageSize,
            DefaultBreastfeedAmount = DefaultBreastfeedAmount,
            DefaultFormulaAmount = DefaultFormulaAmount,
        };
        _settingsService.Save(s);
        StatusMessage = "설정이 저장되었습니다.";
        Services.LogService.Event("설정 저장");
        SettingsSaved?.Invoke();
    }

    [RelayCommand]
    private void BrowseExcelPath()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Excel 파일 (*.xlsx)|*.xlsx|모든 파일 (*.*)|*.*",
            Title = "엑셀 파일 선택"
        };
        if (dlg.ShowDialog() == true)
        {
            ExcelFilePath = dlg.FileName;
        }
    }

    [RelayCommand]
    private void AddProduct()
    {
        if (!string.IsNullOrWhiteSpace(NewProductName) && !FormulaProducts.Contains(NewProductName.Trim()))
        {
            FormulaProducts.Add(NewProductName.Trim());
            NewProductName = "";
        }
    }

    [RelayCommand]
    private void RemoveProduct(string? product)
    {
        if (product != null && FormulaProducts.Contains(product))
        {
            FormulaProducts.Remove(product);
        }
    }

    // ── Spinner commands ────────────────────────────────

    [RelayCommand] private void IncrementFixedHours() => FixedFeedingHours = Math.Min(12, FixedFeedingHours + 1);
    [RelayCommand] private void DecrementFixedHours() => FixedFeedingHours = Math.Max(0, FixedFeedingHours - 1);
    [RelayCommand] private void IncrementFixedMinutes() => FixedFeedingMinutes = Math.Min(59, FixedFeedingMinutes + 5);
    [RelayCommand] private void DecrementFixedMinutes() => FixedFeedingMinutes = Math.Max(0, FixedFeedingMinutes - 5);
    [RelayCommand] private void IncrementAvgCount() => AverageFeedingCount = Math.Min(50, AverageFeedingCount + 1);
    [RelayCommand] private void DecrementAvgCount() => AverageFeedingCount = Math.Max(1, AverageFeedingCount - 1);
    [RelayCommand] private void IncrementBreastfeed() => DefaultBreastfeedAmount = Math.Min(100, DefaultBreastfeedAmount + 5);
    [RelayCommand] private void DecrementBreastfeed() => DefaultBreastfeedAmount = Math.Max(0, DefaultBreastfeedAmount - 5);
    [RelayCommand] private void IncrementFormulaAmount() => DefaultFormulaAmount = Math.Min(200, DefaultFormulaAmount + 5);
    [RelayCommand] private void DecrementFormulaAmount() => DefaultFormulaAmount = Math.Max(40, DefaultFormulaAmount - 5);
    [RelayCommand] private void IncrementPageSize() => PageSize = Math.Min(1000, PageSize + 10);
    [RelayCommand] private void DecrementPageSize() => PageSize = Math.Max(10, PageSize - 10);
}
