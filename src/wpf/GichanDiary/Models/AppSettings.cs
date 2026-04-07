namespace GichanDiary.Models;

public class AppSettings
{
    public string? ExcelFilePath { get; set; }
    public bool AutoBackupEnabled { get; set; } = true;
    public string AutoBackupTime { get; set; } = "앱 시작 시";  // "앱 시작 시" or "매일 HH:mm"
    public string? AutoBackupFolder { get; set; }  // null이면 {DB경로}/Backup/
    public string AutoBackupFilePattern { get; set; } = "{name}_backup_{yyyyMMdd_HHmmss}";
    public string? BabyName { get; set; }
    public DateTime? BabyBirthDate { get; set; }
    public List<string> FormulaProducts { get; set; } = new() { "트루맘 클래식" };
    public string DefaultFormulaProduct { get; set; } = "트루맘 클래식";
    public TimeSpan FixedFeedingInterval { get; set; } = TimeSpan.FromHours(3);
    public int AverageFeedingCount { get; set; } = 10;
    public int PageSize { get; set; } = 30;
    public int DefaultBreastfeedAmount { get; set; } = 20;
    public int DefaultFormulaAmount { get; set; } = 100;
    public string Theme { get; set; } = "Light";
    public bool FirebaseSyncEnabled { get; set; } = false;
}
