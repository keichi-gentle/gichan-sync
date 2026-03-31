using GichanDiary.Models;
using GichanDiary.Services;

namespace GichanDiary.Tests.Services;

public class ExcelServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ExcelService _svc = new();

    public ExcelServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task CreateNewFile_ThenLoad_ReturnsEmpty()
    {
        var path = Path.Combine(_tempDir, "new.xlsx");
        await _svc.CreateNewFileAsync(path);
        Assert.True(File.Exists(path));
        var events = await _svc.LoadEventsAsync(path);
        Assert.Empty(events);
    }

    [Fact]
    public async Task AppendAndLoad_RoundTrips()
    {
        var path = Path.Combine(_tempDir, "test.xlsx");
        await _svc.CreateNewFileAsync(path);
        var evt = new BabyEvent
        {
            DayNumber = 1, Date = new DateTime(2026, 3, 24),
            Time = new TimeSpan(17, 0, 0), Category = EventCategory.수유,
            Detail = "트루맘 클래식", Amount = "80",
            FormulaProduct = "트루맘 클래식", FormulaAmount = 80, FeedingCount = 1,
        };
        await _svc.AppendEventAsync(path, evt);
        var loaded = await _svc.LoadEventsAsync(path);
        Assert.Single(loaded);
        Assert.Equal(EventCategory.수유, loaded[0].Category);
        Assert.Equal(80, loaded[0].FormulaAmount);
    }

    [Fact]
    public async Task Append_건강관리_WritesAs통증InExcel()
    {
        var path = Path.Combine(_tempDir, "test.xlsx");
        await _svc.CreateNewFileAsync(path);
        await _svc.AppendEventAsync(path, new BabyEvent
        {
            Date = new DateTime(2026, 3, 24), Time = new TimeSpan(14, 0, 0),
            Category = EventCategory.건강관리, Detail = "배앓이",
        });
        // Reload and verify it round-trips through "통증" → 건강관리
        var loaded = await _svc.LoadEventsAsync(path);
        Assert.Equal(EventCategory.건강관리, loaded[0].Category);
    }

    [Fact]
    public void IsFileLocked_UnlockedFile_ReturnsFalse()
    {
        var path = Path.Combine(_tempDir, "unlocked.txt");
        File.WriteAllText(path, "test");
        Assert.False(_svc.IsFileLocked(path));
    }

    [Fact]
    public async Task CreateBackup_CreatesFileInBackupFolder()
    {
        var path = Path.Combine(_tempDir, "data.xlsx");
        await _svc.CreateNewFileAsync(path);
        var result = await _svc.CreateBackupAsync(path);
        Assert.True(File.Exists(result));
        var backupDir = Path.Combine(_tempDir, "Backup");
        var backups = Directory.GetFiles(backupDir, "data_backup_*.xlsx");
        Assert.Single(backups);
    }

    [Fact]
    public async Task DeleteEvent_RemovesRow()
    {
        var path = Path.Combine(_tempDir, "del.xlsx");
        await _svc.CreateNewFileAsync(path);
        var e1 = new BabyEvent { Date = new DateTime(2026, 3, 24), Time = new TimeSpan(10, 0, 0), Category = EventCategory.수유, FormulaAmount = 80, Amount = "80" };
        var e2 = new BabyEvent { Date = new DateTime(2026, 3, 24), Time = new TimeSpan(13, 0, 0), Category = EventCategory.수유, FormulaAmount = 90, Amount = "90" };
        await _svc.AppendEventAsync(path, e1);
        await _svc.AppendEventAsync(path, e2);
        var loaded = await _svc.LoadEventsAsync(path);
        Assert.Equal(2, loaded.Count);
        await _svc.DeleteEventAsync(path, loaded[0]);
        var after = await _svc.LoadEventsAsync(path);
        Assert.Single(after);
        Assert.Equal(90, after[0].FormulaAmount);
    }
}
