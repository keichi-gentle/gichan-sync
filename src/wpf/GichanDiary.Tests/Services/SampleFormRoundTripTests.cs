using GichanDiary.Models;
using GichanDiary.Services;

namespace GichanDiary.Tests.Services;

public class SampleFormRoundTripTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ExcelService _svc = new();

    public SampleFormRoundTripTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private string GetSamplePath()
    {
        // Navigate from test output to docs/sample_form.xlsx
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        var root = Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "..", ".."));
        var sample = Path.Combine(root, "docs", "sample_form.xlsx");
        if (!File.Exists(sample))
            throw new FileNotFoundException($"sample_form.xlsx not found at {sample}");
        return sample;
    }

    [Fact]
    public async Task LoadSampleForm_AllRowsParsed()
    {
        var path = GetSamplePath();
        var events = await _svc.LoadEventsAsync(path);
        Assert.True(events.Count > 100, $"Expected >100 rows, got {events.Count}");
    }

    [Fact]
    public async Task LoadSampleForm_CategoriesParsedCorrectly()
    {
        var events = await _svc.LoadEventsAsync(GetSamplePath());
        var cats = events.GroupBy(e => e.Category).ToDictionary(g => g.Key, g => g.Count());

        Assert.True(cats.ContainsKey(EventCategory.수유), "수유 category missing");
        Assert.True(cats.ContainsKey(EventCategory.배변), "배변 category missing");
        Assert.True(cats.ContainsKey(EventCategory.위생관리), "위생관리 category missing");
        Assert.True(cats.ContainsKey(EventCategory.신체측정), "신체측정 category missing (from Excel '신체')");
        Assert.True(cats.ContainsKey(EventCategory.건강관리), "건강관리 category missing (from Excel '통증')");
        Assert.True(cats.ContainsKey(EventCategory.기타), "기타 category missing");
    }

    [Fact]
    public async Task LoadSampleForm_FeedingFieldsParsed()
    {
        var events = await _svc.LoadEventsAsync(GetSamplePath());
        var feedings = events.Where(e => e.IsFeeding).ToList();

        // Some should have formula
        Assert.True(feedings.Any(f => f.FormulaAmount > 0), "No formula amount found");
        // Some should have breastfeed
        Assert.True(feedings.Any(f => f.BreastfeedAmount > 0), "No breastfeed amount found");
        // Some should have both
        Assert.True(feedings.Any(f => f.FormulaAmount > 0 && f.BreastfeedAmount > 0), "No combined feed found");
        // FeedingCount should be parsed
        Assert.True(feedings.Any(f => f.FeedingCount > 1), "No split feeding found");
    }

    [Fact]
    public async Task LoadSampleForm_BowelFieldsParsed()
    {
        var events = await _svc.LoadEventsAsync(GetSamplePath());
        var bowels = events.Where(e => e.Category == EventCategory.배변).ToList();

        Assert.True(bowels.Any(b => b.HasUrine == true), "No urine found");
        Assert.True(bowels.Any(b => b.HasStool == true), "No stool found");
        Assert.True(bowels.Any(b => b.HasUrine == true && b.HasStool == true), "No combined bowel found");
        Assert.True(bowels.Any(b => b.ImmediateNotice == true), "No immediate notice found");
    }

    [Fact]
    public async Task RoundTrip_ExportThenImport_DataIdentical()
    {
        // 1. Load original
        var originalPath = GetSamplePath();
        var original = await _svc.LoadEventsAsync(originalPath);

        // 2. Export to new file
        var exportPath = Path.Combine(_tempDir, "exported.xlsx");
        await _svc.ExportEventsAsync(exportPath, original);

        // 3. Load exported file
        var reloaded = await _svc.LoadEventsAsync(exportPath);

        // 4. Compare: same count
        Assert.Equal(original.Count, reloaded.Count);

        // 5. Compare each row field by field
        for (int i = 0; i < original.Count; i++)
        {
            var o = original[i];
            var r = reloaded[i];

            Assert.Equal(o.Date.Date, r.Date.Date);
            Assert.Equal(o.Time, r.Time);
            Assert.Equal(o.Category, r.Category);
            Assert.Equal(o.Detail, r.Detail);
            Assert.Equal(o.Note, r.Note);
            Assert.Equal(o.FormulaProduct, r.FormulaProduct);
            Assert.Equal(o.FormulaAmount, r.FormulaAmount);
            Assert.Equal(o.BreastfeedAmount, r.BreastfeedAmount);
            Assert.Equal(o.FeedingCount, r.FeedingCount);
            Assert.Equal(o.HasUrine, r.HasUrine);
            Assert.Equal(o.HasStool, r.HasStool);
            Assert.Equal(o.ImmediateNotice, r.ImmediateNotice);
        }
    }
}
