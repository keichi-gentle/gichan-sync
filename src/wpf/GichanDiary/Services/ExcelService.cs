using System.IO;
using ClosedXML.Excel;
using GichanDiary.Helpers;
using GichanDiary.Models;

namespace GichanDiary.Services;

public class ExcelService : IExcelService
{
    private static readonly SemaphoreSlim _writeLock = new(1, 1);
    private string? _lastBackupDate;
    private ISettingsService? _settingsService;

    public ExcelService() { }

    public ExcelService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>자동 백업: 설정에 따른 폴더/파일명으로 하루 1회</summary>
    public async Task CreateAutoBackupIfNeededAsync(string filePath)
    {
        Models.AppSettings? settings = null;
        if (_settingsService != null)
        {
            settings = _settingsService.Load();
            if (!settings.AutoBackupEnabled) return;
        }

        var today = DateTime.Now.ToString("yyyyMMdd");
        if (_lastBackupDate == today) return;

        if (!File.Exists(filePath)) return;

        await Task.Run(() =>
        {
            var dir = Path.GetDirectoryName(filePath)!;
            var name = Path.GetFileNameWithoutExtension(filePath);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            // 백업 폴더: 설정값 우선, 없으면 {DB경로}/Backup/
            var backupDir = !string.IsNullOrEmpty(settings?.AutoBackupFolder)
                ? settings!.AutoBackupFolder
                : Path.Combine(dir, "Backup");
            if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);

            // 파일명: 설정 패턴 사용
            var pattern = settings?.AutoBackupFilePattern ?? "{name}_backup_{yyyyMMdd_HHmmss}";
            var fileName = pattern
                .Replace("{name}", name)
                .Replace("{yyyyMMdd_HHmmss}", timestamp)
                .Replace("{yyyyMMdd}", DateTime.Now.ToString("yyyyMMdd"));
            if (!fileName.EndsWith(".xlsx")) fileName += ".xlsx";

            var backupPath = Path.Combine(backupDir, fileName);
            File.Copy(filePath, backupPath);
            LogService.System($"자동 백업 생성: {backupPath}");
        });

        _lastBackupDate = today;
    }

    private static readonly string[] Headers =
    {
        "일차", "날짜", "시간", "구분", "세부내용", "양(ml)", "비고",
        "수유텀", "다음예상", "일일수유량", "분유제품", "분유량(ml)",
        "모유수유량(ml)", "수유횟수", "소변여부", "대변여부", "직후인지"
    };

    // ── Load ──────────────────────────────────────────────

    public Task<List<BabyEvent>> LoadEventsAsync(string filePath)
    {
        return Task.Run(() =>
        {
            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheets.First();
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            var events = new List<BabyEvent>();

            for (int r = 2; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                // Skip completely empty rows
                if (row.IsEmpty()) continue;

                var evt = new BabyEvent
                {
                    ExcelRowIndex = r,
                    DayNumber = ParseDayNumber(GetStringOrNull(row.Cell(1))),
                    Date = row.Cell(2).TryGetValue<DateTime>(out var dt) ? dt : DateTime.MinValue,
                    Time = ParseTimeSpan(GetStringOrNull(row.Cell(3))),
                    Category = ExcelColumnMapper.ParseCategory(GetStringOrNull(row.Cell(4)) ?? "기타"),
                    Detail = GetStringOrNull(row.Cell(5)) ?? string.Empty,
                    Amount = GetStringOrNull(row.Cell(6)) ?? "-",
                    Note = GetStringOrNull(row.Cell(7)) ?? string.Empty,
                    FeedingInterval = ParseTimeSpan(GetStringOrNull(row.Cell(8))),
                    NextExpected = ParseTimeSpan(GetStringOrNull(row.Cell(9))),
                    DailyFeedTotal = ParseDoubleNullable(GetStringOrNull(row.Cell(10))),
                    // Extended K~Q
                    FormulaProduct = GetStringOrNull(row.Cell(11)),
                    FormulaAmount = ParseIntNullable(GetStringOrNull(row.Cell(12))),
                    BreastfeedAmount = ParseIntNullable(GetStringOrNull(row.Cell(13))),
                    FeedingCount = ParseIntNullable(GetStringOrNull(row.Cell(14))),
                    HasUrine = ParseBoolNullable(row.Cell(15)),
                    HasStool = ParseBoolNullable(row.Cell(16)),
                    ImmediateNotice = ParseBoolNullable(row.Cell(17)),
                };

                events.Add(evt);
            }

            return events;
        });
    }

    // ── Append ────────────────────────────────────────────

    public async Task AppendEventAsync(string filePath, BabyEvent newEvent)
    {
        if (IsFileLocked(filePath))
            throw new IOException($"Excel 파일이 다른 프로그램에서 열려 있습니다: {Path.GetFileName(filePath)}");

        await _writeLock.WaitAsync();
        try
        {
            await Task.Run(() =>
            {
                using var wb = new XLWorkbook(filePath);
                var ws = wb.Worksheets.First();
                var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

                // 새 이벤트의 날짜+시간
                var newDt = newEvent.Time.HasValue
                    ? newEvent.Date.Add(newEvent.Time.Value)
                    : newEvent.Date;

                // 삽입할 위치 찾기 (날짜+시간 오름차순 정렬 유지)
                int insertRow = lastRow + 1; // 기본: 맨 끝
                for (int r = 2; r <= lastRow; r++)
                {
                    var cellDate = ws.Cell(r, 2).GetValue<DateTime?>();
                    var cellTimeStr = ws.Cell(r, 3).GetString();
                    if (cellDate == null) continue;

                    var cellDt = cellDate.Value;
                    if (TimeSpan.TryParse(cellTimeStr, out var cellTime))
                        cellDt = cellDate.Value.Date.Add(cellTime);

                    if (cellDt > newDt)
                    {
                        insertRow = r;
                        break;
                    }
                }

                // 삽입 위치가 마지막이 아니면 행을 밀어서 공간 확보
                if (insertRow <= lastRow)
                {
                    ws.Row(insertRow).InsertRowsAbove(1);
                }

                newEvent.ExcelRowIndex = insertRow;
                WriteEventToRow(ws, insertRow, newEvent);
                if (newEvent.IsFeeding)
                    RecalcDailyFeedTotal(ws, newEvent.Date);
                wb.Save();
            });
        }
        finally
        {
            _writeLock.Release();
        }
    }

    // ── Update ────────────────────────────────────────────

    public async Task UpdateEventAsync(string filePath, BabyEvent updated)
    {
        if (IsFileLocked(filePath))
            throw new IOException($"Excel 파일이 다른 프로그램에서 열려 있습니다: {Path.GetFileName(filePath)}");

        await _writeLock.WaitAsync();
        try
        {
            await Task.Run(() =>
            {
                using var wb = new XLWorkbook(filePath);
                var ws = wb.Worksheets.First();
                var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

                var rowNumber = updated.ExcelRowIndex;
                // ExcelRowIndex 유효하지 않으면 Date+Time+Detail로 검색
                if (rowNumber < 2 || rowNumber > lastRow)
                    rowNumber = FindRowByEvent(ws, updated, lastRow);

                if (rowNumber >= 2)
                {
                    WriteEventToRow(ws, rowNumber, updated);
                    if (updated.IsFeeding)
                        RecalcDailyFeedTotal(ws, updated.Date);
                    wb.Save();
                }
            });
        }
        finally
        {
            _writeLock.Release();
        }
    }

    // ── Delete ────────────────────────────────────────────

    public async Task DeleteEventAsync(string filePath, BabyEvent target)
    {
        if (IsFileLocked(filePath))
            throw new IOException($"Excel 파일이 다른 프로그램에서 열려 있습니다: {Path.GetFileName(filePath)}");

        await _writeLock.WaitAsync();
        try
        {
            await Task.Run(() =>
            {
                using var wb = new XLWorkbook(filePath);
                var ws = wb.Worksheets.First();
                var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

                var rowNumber = target.ExcelRowIndex;

                // ExcelRowIndex가 유효하지 않으면 (Firestore에서 로드된 이벤트 등) Date+Time+Detail로 검색
                if (rowNumber < 2 || rowNumber > lastRow)
                {
                    rowNumber = FindRowByEvent(ws, target, lastRow);
                }

                if (rowNumber >= 2 && rowNumber <= lastRow)
                {
                    ws.Row(rowNumber).Delete();
                    wb.Save();
                }
                // 행을 찾지 못하면 조용히 넘김 (Excel에 없는 이벤트 삭제 요청)
            });
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// ExcelRowIndex 정보가 없을 때 Date+Time+Category+Detail로 Excel 행 검색.
    /// 찾지 못하면 0 반환.
    /// </summary>
    private static int FindRowByEvent(IXLWorksheet ws, BabyEvent target, int lastRow)
    {
        var targetDate = target.Date.Date;
        var targetTimeStr = target.Time?.ToString(@"hh\:mm") ?? "";
        var targetCat = target.Category.ToString();
        var targetDetail = target.Detail ?? "";

        for (int r = 2; r <= lastRow; r++)
        {
            var dateCell = ws.Cell(r, 2).GetDateTime();
            if (dateCell.Date != targetDate) continue;

            var timeCell = ws.Cell(r, 3).GetString()?.Trim() ?? "";
            if (timeCell != targetTimeStr) continue;

            var catCell = ws.Cell(r, 4).GetString()?.Trim() ?? "";
            if (catCell != targetCat) continue;

            var detailCell = ws.Cell(r, 5).GetString()?.Trim() ?? "";
            if (detailCell == targetDetail) return r;
        }
        return 0;
    }

    // ── Import / Export ───────────────────────────────────

    public async Task<List<BabyEvent>> ImportEventsAsync(string filePath, ImportMode mode)
    {
        var importedEvents = await LoadEventsAsync(filePath);

        if (mode == ImportMode.Merge && _settingsService != null)
        {
            var settings = _settingsService.Load();
            if (!string.IsNullOrEmpty(settings.ExcelFilePath) && File.Exists(settings.ExcelFilePath))
            {
                var existingEvents = await LoadEventsAsync(settings.ExcelFilePath);
                var merged = existingEvents.Concat(importedEvents)
                    .OrderBy(e => e.Date)
                    .ThenBy(e => e.Time)
                    .ToList();

                // Write merged data back to current file
                await ExportEventsAsync(settings.ExcelFilePath, merged);
                return merged;
            }
        }

        // Overwrite mode: write imported events to current file
        if (_settingsService != null)
        {
            var settings = _settingsService.Load();
            if (!string.IsNullOrEmpty(settings.ExcelFilePath))
            {
                await ExportEventsAsync(settings.ExcelFilePath, importedEvents);
            }
        }

        return importedEvents;
    }

    public async Task ExportEventsAsync(string targetPath, List<BabyEvent> events)
    {
        await _writeLock.WaitAsync();
        try
        {
            await Task.Run(() =>
            {
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Sheet1");
                WriteHeaderRow(ws);
                for (int i = 0; i < events.Count; i++)
                {
                    WriteEventToRow(ws, i + 2, events[i]);
                }
                wb.SaveAs(targetPath);
            });
        }
        finally
        {
            _writeLock.Release();
        }
    }

    // ── Backup ────────────────────────────────────────────

    /// <summary>수동 백업: 지정 경로로 백업 (targetPath가 null이면 Backup 폴더에 자동 생성)</summary>
    public Task<string> CreateBackupAsync(string filePath, string? targetPath = null)
    {
        return Task.Run(() =>
        {
            if (string.IsNullOrEmpty(targetPath))
            {
                var dir = Path.GetDirectoryName(filePath)!;
                var backupDir = Path.Combine(dir, "Backup");
                if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);
                var name = Path.GetFileNameWithoutExtension(filePath);
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                targetPath = Path.Combine(backupDir, $"{name}_backup_{timestamp}.xlsx");
            }
            File.Copy(filePath, targetPath);
            return targetPath;
        });
    }

    // ── Create New ────────────────────────────────────────

    public Task CreateNewFileAsync(string filePath)
    {
        return Task.Run(() =>
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Sheet1");
            WriteHeaderRow(ws);
            wb.SaveAs(filePath);
        });
    }

    // ── IsFileLocked ──────────────────────────────────────

    public bool IsFileLocked(string filePath)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }

    // ── Helpers ───────────────────────────────────────────

    private static void WriteHeaderRow(IXLWorksheet ws)
    {
        for (int i = 0; i < Headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = Headers[i];
        }
    }

    private static void WriteEventToRow(IXLWorksheet ws, int row, BabyEvent evt)
    {
        ws.Cell(row, 1).Value = evt.DayNumber ?? 0;
        ws.Cell(row, 2).Value = evt.Date;
        ws.Cell(row, 3).Value = evt.Time.HasValue ? evt.Time.Value.ToString(@"hh\:mm") : "";
        ws.Cell(row, 4).Value = ExcelColumnMapper.ToCategoryString(evt.Category);
        ws.Cell(row, 5).Value = evt.Detail;
        ws.Cell(row, 6).Value = evt.Amount;
        ws.Cell(row, 7).Value = evt.Note;
        ws.Cell(row, 8).Value = evt.FeedingInterval.HasValue ? evt.FeedingInterval.Value.ToString(@"hh\:mm") : "";
        ws.Cell(row, 9).Value = evt.NextExpected.HasValue ? evt.NextExpected.Value.ToString(@"hh\:mm") : "";
        ws.Cell(row, 10).Value = evt.DailyFeedTotal ?? 0;
        ws.Cell(row, 11).Value = evt.FormulaProduct ?? "";
        ws.Cell(row, 12).Value = evt.FormulaAmount ?? 0;
        ws.Cell(row, 13).Value = evt.BreastfeedAmount ?? 0;
        ws.Cell(row, 14).Value = evt.FeedingCount ?? 0;
        ws.Cell(row, 15).Value = evt.HasUrine == true ? "TRUE" : evt.HasUrine == false ? "FALSE" : "";
        ws.Cell(row, 16).Value = evt.HasStool == true ? "TRUE" : evt.HasStool == false ? "FALSE" : "";
        ws.Cell(row, 17).Value = evt.ImmediateNotice == true ? "TRUE" : evt.ImmediateNotice == false ? "FALSE" : "";
    }

    /// <summary>해당 날짜의 모든 수유 행의 일일수유량(Column J)을 재계산</summary>
    private static void RecalcDailyFeedTotal(IXLWorksheet ws, DateTime targetDate)
    {
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        var feedingRows = new List<int>();
        double dailyTotal = 0;

        for (int r = 2; r <= lastRow; r++)
        {
            if (!ws.Cell(r, 2).TryGetValue<DateTime>(out var rowDate)) continue;
            if (rowDate.Date != targetDate.Date) continue;

            var category = ws.Cell(r, 4).GetString();
            if (category != "수유") continue;

            feedingRows.Add(r);
            var formula = ParseIntNullable(GetStringOrNull(ws.Cell(r, 12))) ?? 0;
            var breast = ParseIntNullable(GetStringOrNull(ws.Cell(r, 13))) ?? 0;
            dailyTotal += formula + breast;
        }

        foreach (var r in feedingRows)
            ws.Cell(r, 10).Value = dailyTotal;
    }

    private static TimeSpan? ParseTimeSpan(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (TimeSpan.TryParseExact(value, @"hh\:mm\:ss", null, out var ts1)) return ts1;
        if (TimeSpan.TryParseExact(value, @"hh\:mm", null, out var ts2)) return ts2;
        if (TimeSpan.TryParse(value, out var ts3)) return ts3;
        return null;
    }

    private static bool? ParseBoolNullable(IXLCell cell)
    {
        var val = GetStringOrNull(cell);
        if (string.IsNullOrWhiteSpace(val)) return null;
        if (val.Equals("TRUE", StringComparison.OrdinalIgnoreCase)) return true;
        if (val.Equals("FALSE", StringComparison.OrdinalIgnoreCase)) return false;
        return null;
    }

    private static int? ParseDayNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        // Handle "22일차" format — extract digits only
        var numStr = new string(value.Where(char.IsDigit).ToArray());
        return int.TryParse(numStr, out var n) ? n : null;
    }

    private static int? ParseIntNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return int.TryParse(value, out var n) ? n : null;
    }

    private static double? ParseDoubleNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return double.TryParse(value, out var d) ? d : null;
    }

    private static string? GetStringOrNull(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;
        var str = cell.GetFormattedString();
        return string.IsNullOrWhiteSpace(str) ? null : str.Trim();
    }
}
