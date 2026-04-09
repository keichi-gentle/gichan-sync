using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GichanDiary.Models;
using GichanDiary.Services;
using Microsoft.Win32;

namespace GichanDiary.ViewModels;

public partial class ImportExportViewModel : ObservableObject
{
    private readonly IExcelService _excelService;
    private readonly ISettingsService _settingsService;
    private readonly IDataService? _dataService;

    [ObservableProperty] private string _currentFilePath = "";
    [ObservableProperty] private int _totalRecords;
    [ObservableProperty] private string _lastSaved = "-";
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _importOverwrite = true;

    // 자동 백업 설정
    [ObservableProperty] private bool _autoBackupEnabled = true;
    [ObservableProperty] private string _autoBackupTime = "앱 시작 시";
    [ObservableProperty] private string _autoBackupFolder = "";
    [ObservableProperty] private string _autoBackupFilePattern = "{name}_backup_{yyyyMMdd_HHmmss}";

    public event Action? DataChanged;

    public ImportExportViewModel(IExcelService excelService, ISettingsService settingsService,
        IDataService? dataService = null)
    {
        _excelService = excelService;
        _settingsService = settingsService;
        _dataService = dataService;
        var settings = _settingsService.Load();
        CurrentFilePath = settings.ExcelFilePath ?? "";

        // 자동 백업 설정 로드
        AutoBackupEnabled = settings.AutoBackupEnabled;
        AutoBackupTime = settings.AutoBackupTime;
        AutoBackupFolder = settings.AutoBackupFolder
            ?? (string.IsNullOrEmpty(settings.ExcelFilePath) ? ""
                : System.IO.Path.Combine(System.IO.Path.GetDirectoryName(settings.ExcelFilePath)!, "Backup"));
        AutoBackupFilePattern = settings.AutoBackupFilePattern;
    }

    public void Refresh(List<BabyEvent> events)
    {
        TotalRecords = events.Count;
        LastSaved = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            Title = "가져올 Excel 파일 선택"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var mode = ImportOverwrite ? ImportMode.Overwrite : ImportMode.Merge;
            var events = _dataService != null
                ? await _dataService.ImportEventsAsync(dlg.FileName, mode)
                : await _excelService.ImportEventsAsync(dlg.FileName, mode);
            var newProducts = AutoAddNewProducts(events);

            StatusMessage = $"가져오기 완료: {events.Count}건";
            if (newProducts.Count > 0)
                StatusMessage += $"\n새 분유 제품 추가: {string.Join(", ", newProducts)}";

            TotalRecords = events.Count;
            Services.LogService.Event($"Import 완료: {events.Count}건 ({mode})");
            DataChanged?.Invoke();
        }
        catch (System.IO.IOException)
        {
            StatusMessage = "가져오기 실패: Excel 파일이 다른 프로그램에서 열려 있습니다.";
            Services.LogService.System("Import 실패: Excel 파일이 다른 프로그램에서 열려 있습니다.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"가져오기 실패: {ex.Message}";
            Services.LogService.System($"Import 실패: {ex.Message}");
        }
    }

    private List<string> AutoAddNewProducts(List<BabyEvent> events)
    {
        var settings = _settingsService.Load();
        var existingProducts = new HashSet<string>(settings.FormulaProducts);
        var newProducts = events
            .Where(e => e.IsFeeding && !string.IsNullOrEmpty(e.FormulaProduct))
            .Select(e => e.FormulaProduct!)
            .Distinct()
            .Where(p => !existingProducts.Contains(p))
            .ToList();

        if (newProducts.Count > 0)
        {
            settings.FormulaProducts.AddRange(newProducts);
            _settingsService.Save(settings);
        }

        return newProducts;
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = $"기찬이_이벤트일지_{DateTime.Now:yyyyMMdd}.xlsx",
            Title = "내보낼 위치 선택"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var settings = _settingsService.Load();
            var events = await _excelService.LoadEventsAsync(settings.ExcelFilePath!);
            await _excelService.ExportEventsAsync(dlg.FileName, events);
            StatusMessage = $"내보내기 완료: {dlg.FileName}";
            Services.LogService.Event($"Export 완료: {dlg.FileName}");
        }
        catch (System.IO.IOException)
        {
            StatusMessage = "내보내기 실패: Excel 파일이 다른 프로그램에서 열려 있습니다.";
            Services.LogService.System("Export 실패: Excel 파일이 다른 프로그램에서 열려 있습니다.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"내보내기 실패: {ex.Message}";
            Services.LogService.System($"Export 실패: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        try
        {
            var settings = _settingsService.Load();
            var srcPath = settings.ExcelFilePath!;
            var srcDir = System.IO.Path.GetDirectoryName(srcPath)!;
            var srcName = System.IO.Path.GetFileNameWithoutExtension(srcPath);
            var defaultDir = System.IO.Path.Combine(srcDir, "Backup");
            var defaultName = $"{srcName}_backup_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = defaultName,
                InitialDirectory = System.IO.Directory.Exists(defaultDir) ? defaultDir : srcDir,
                Title = "백업 파일 저장 위치 선택"
            };

            if (dlg.ShowDialog() != true) return;

            var savedPath = await _excelService.CreateBackupAsync(srcPath, dlg.FileName);
            StatusMessage = $"백업 완료: {savedPath}";
            Services.LogService.Event($"수동 백업 완료: {savedPath}");
        }
        catch (System.IO.IOException)
        {
            StatusMessage = "백업 실패: Excel 파일이 다른 프로그램에서 열려 있습니다.";
            Services.LogService.System("백업 실패: Excel 파일이 다른 프로그램에서 열려 있습니다.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"백업 실패: {ex.Message}";
            Services.LogService.System($"백업 실패: {ex.Message}");
        }
    }

    [RelayCommand]
    private void SaveAutoBackupSettings()
    {
        var settings = _settingsService.Load();
        settings.AutoBackupEnabled = AutoBackupEnabled;
        settings.AutoBackupTime = AutoBackupTime;
        settings.AutoBackupFolder = AutoBackupFolder;
        settings.AutoBackupFilePattern = AutoBackupFilePattern;
        _settingsService.Save(settings);
        StatusMessage = "자동 백업 설정 저장됨";
        Services.LogService.Event("자동 백업 설정 변경");
    }

    [RelayCommand]
    private void BrowseBackupFolder()
    {
        // WPF에서 폴더 선택: OpenFileDialog로 폴더를 선택하는 트릭 대신
        // SaveFileDialog로 파일명을 지정하면 해당 폴더를 추출
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "백업 폴더 선택 (아무 파일명으로 저장 클릭)",
            FileName = "folder_select",
            Filter = "Folder|*.folder",
            InitialDirectory = !string.IsNullOrEmpty(AutoBackupFolder) && System.IO.Directory.Exists(AutoBackupFolder)
                ? AutoBackupFolder : "",
        };
        if (dlg.ShowDialog() == true)
        {
            AutoBackupFolder = System.IO.Path.GetDirectoryName(dlg.FileName) ?? AutoBackupFolder;
        }
    }
}
