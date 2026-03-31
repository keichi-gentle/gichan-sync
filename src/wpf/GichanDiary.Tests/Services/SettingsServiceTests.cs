using GichanDiary.Models;
using GichanDiary.Services;
namespace GichanDiary.Tests.Services;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SettingsService _svc;

    public SettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _svc = new SettingsService(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Load_NoFile_ReturnsDefaults()
    {
        var s = _svc.Load();
        Assert.Null(s.ExcelFilePath);
        Assert.True(s.AutoBackupEnabled);
        Assert.Equal(20, s.DefaultBreastfeedAmount);
        Assert.Equal(30, s.PageSize);
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        var s = new AppSettings
        {
            ExcelFilePath = @"C:\test.xlsx",
            BabyName = "기찬",
            BabyBirthDate = new DateTime(2026, 2, 27),
            DefaultBreastfeedAmount = 15,
        };
        _svc.Save(s);
        var loaded = _svc.Load();
        Assert.Equal(@"C:\test.xlsx", loaded.ExcelFilePath);
        Assert.Equal("기찬", loaded.BabyName);
        Assert.Equal(new DateTime(2026, 2, 27), loaded.BabyBirthDate);
        Assert.Equal(15, loaded.DefaultBreastfeedAmount);
    }
}
