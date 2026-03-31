using System.Text;
using System.Text.Json;
using ClosedXML.Excel;

// ── Configuration ──
const string PROJECT_ID = "gichan-diary";

if (args.Length < 2)
{
    Console.WriteLine("Usage: dotnet run -- <excel-path> <firebase-uid>");
    Console.WriteLine("  excel-path:   Path to the Excel (.xlsx) file");
    Console.WriteLine("  firebase-uid: Firebase Auth UID of the target user");
    Console.WriteLine();
    Console.WriteLine("To get your UID: Firebase Console > Authentication > Users > copy UID");
    return;
}

var excelPath = args[0];
var userId = args[1];

if (!File.Exists(excelPath))
{
    Console.WriteLine($"Error: File not found: {excelPath}");
    return;
}

Console.WriteLine($"Excel: {excelPath}");
Console.WriteLine($"User UID: {userId}");
Console.WriteLine($"Project: {PROJECT_ID}");
Console.WriteLine();

// ── Parse Excel ──
Console.Write("Reading Excel file... ");
var events = ParseExcel(excelPath);
Console.WriteLine($"{events.Count} events found.");

// ── Upload to Firestore ──
Console.WriteLine("Uploading to Firestore...");
var baseUrl = $"https://firestore.googleapis.com/v1/projects/{PROJECT_ID}/databases/(default)/documents";
using var http = new HttpClient();

int success = 0, errors = 0;
foreach (var chunk in events.Chunk(20))
{
    var tasks = chunk.Select(async evt =>
    {
        try
        {
            var docId = evt.Id;
            var url = $"{baseUrl}/users/{userId}/events/{docId}";
            var body = ToFirestoreDocument(evt);
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), url) { Content = content };
            var response = await http.SendAsync(request);

            if (response.IsSuccessStatusCode)
                Interlocked.Increment(ref success);
            else
            {
                var err = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"  Error [{response.StatusCode}]: {err[..Math.Min(200, err.Length)]}");
                Interlocked.Increment(ref errors);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Exception: {ex.Message}");
            Interlocked.Increment(ref errors);
        }
    });

    await Task.WhenAll(tasks);
    Console.Write($"\r  Progress: {success + errors}/{events.Count}");
}

Console.WriteLine();
Console.WriteLine($"Done! Success: {success}, Errors: {errors}");

// ── Excel Parser ──
static List<BabyEvent> ParseExcel(string path)
{
    using var wb = new XLWorkbook(path);
    var ws = wb.Worksheets.First();
    var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
    var list = new List<BabyEvent>();

    var catMap = new Dictionary<string, string>
    {
        ["수유"] = "수유", ["배변"] = "배변",
        ["위생관리"] = "위생관리", ["위생"] = "위생관리",
        ["신체측정"] = "신체측정", ["신체"] = "신체측정",
        ["건강관리"] = "건강관리", ["통증"] = "건강관리",
        ["기타"] = "기타",
    };

    for (int r = 2; r <= lastRow; r++)
    {
        var row = ws.Row(r);
        if (row.IsEmpty()) continue;

        var rawCat = GetStr(row.Cell(4)) ?? "기타";
        catMap.TryGetValue(rawCat.Trim(), out var category);
        category ??= "기타";

        var date = row.Cell(2).TryGetValue<DateTime>(out var dt) ? dt : (DateTime?)null;
        if (date == null) continue;

        list.Add(new BabyEvent
        {
            Id = Guid.NewGuid().ToString(),
            DayNumber = ParseDayNumber(GetStr(row.Cell(1))),
            Date = date.Value,
            Time = ParseTime(GetStr(row.Cell(3))),
            Category = category,
            Detail = GetStr(row.Cell(5)) ?? "",
            Amount = GetStr(row.Cell(6)) ?? "-",
            Note = GetStr(row.Cell(7)) ?? "",
            FeedingInterval = GetStr(row.Cell(8)),
            NextExpected = GetStr(row.Cell(9)),
            DailyFeedTotal = ParseDouble(GetStr(row.Cell(10))),
            FormulaProduct = GetStr(row.Cell(11)),
            FormulaAmount = ParseInt(GetStr(row.Cell(12))),
            BreastfeedAmount = ParseInt(GetStr(row.Cell(13))),
            FeedingCount = ParseInt(GetStr(row.Cell(14))),
            HasUrine = ParseBool(row.Cell(15)),
            HasStool = ParseBool(row.Cell(16)),
            ImmediateNotice = ParseBool(row.Cell(17)),
        });
    }
    return list;
}

// ── Firestore Document Builder ──
static string ToFirestoreDocument(BabyEvent evt)
{
    var fields = new Dictionary<string, object>
    {
        ["id"] = SVal(evt.Id),
        ["date"] = TsVal(evt.Date.Add(ParseTimeSpan(evt.Time))),
        ["category"] = SVal(evt.Category),
        ["detail"] = SVal(evt.Detail),
        ["amount"] = SVal(evt.Amount),
        ["note"] = SVal(evt.Note),
        ["source"] = SVal("migration"),
        ["createdAt"] = TsVal(DateTime.UtcNow),
        ["updatedAt"] = TsVal(DateTime.UtcNow),
    };

    if (evt.Time != null) fields["time"] = SVal(evt.Time);
    if (evt.DayNumber.HasValue) fields["dayNumber"] = IVal(evt.DayNumber.Value);
    if (evt.DailyFeedTotal.HasValue) fields["dailyFeedTotal"] = DVal(evt.DailyFeedTotal.Value);
    if (evt.FormulaProduct != null) fields["formulaProduct"] = SVal(evt.FormulaProduct);
    if (evt.FormulaAmount.HasValue) fields["formulaAmount"] = IVal(evt.FormulaAmount.Value);
    if (evt.BreastfeedAmount.HasValue) fields["breastfeedAmount"] = IVal(evt.BreastfeedAmount.Value);
    if (evt.FeedingCount.HasValue) fields["feedingCount"] = IVal(evt.FeedingCount.Value);
    if (evt.FeedingInterval != null) fields["feedingInterval"] = SVal(evt.FeedingInterval);
    if (evt.NextExpected != null) fields["nextExpected"] = SVal(evt.NextExpected);
    if (evt.HasUrine.HasValue) fields["hasUrine"] = BVal(evt.HasUrine.Value);
    if (evt.HasStool.HasValue) fields["hasStool"] = BVal(evt.HasStool.Value);
    if (evt.ImmediateNotice.HasValue) fields["immediateNotice"] = BVal(evt.ImmediateNotice.Value);

    return JsonSerializer.Serialize(new { fields });
}

static object SVal(string v) => new { stringValue = v };
static object IVal(int v) => new { integerValue = v.ToString() };
static object DVal(double v) => new { doubleValue = v };
static object BVal(bool v) => new { booleanValue = v };
static object TsVal(DateTime v) => new { timestampValue = v.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ") };

static string? GetStr(IXLCell cell)
{
    if (cell.IsEmpty()) return null;
    var v = cell.GetString()?.Trim();
    return string.IsNullOrEmpty(v) ? null : v;
}

static int? ParseDayNumber(string? v)
{
    if (v == null) return null;
    var num = new string(v.Where(char.IsDigit).ToArray());
    return int.TryParse(num, out var n) ? n : null;
}

static string? ParseTime(string? v)
{
    if (v == null) return null;
    if (TimeSpan.TryParse(v, out var ts))
        return $"{ts.Hours:D2}:{ts.Minutes:D2}";
    return null;
}

static TimeSpan ParseTimeSpan(string? v)
{
    if (v == null) return TimeSpan.Zero;
    var parts = v.Split(':');
    if (parts.Length >= 2 && int.TryParse(parts[0], out var h) && int.TryParse(parts[1], out var m))
        return new TimeSpan(h, m, 0);
    return TimeSpan.Zero;
}

static int? ParseInt(string? v)
{
    if (v == null) return null;
    return int.TryParse(v, out var n) && n != 0 ? n : null;
}

static double? ParseDouble(string? v)
{
    if (v == null) return null;
    return double.TryParse(v, out var n) && n != 0 ? n : null;
}

static bool? ParseBool(IXLCell cell)
{
    var v = GetStr(cell)?.ToUpper();
    if (v == "TRUE" || v == "1") return true;
    if (v == "FALSE" || v == "0") return false;
    return null;
}

class BabyEvent
{
    public string Id { get; set; } = "";
    public int? DayNumber { get; set; }
    public DateTime Date { get; set; }
    public string? Time { get; set; }
    public string Category { get; set; } = "기타";
    public string Detail { get; set; } = "";
    public string Amount { get; set; } = "-";
    public string Note { get; set; } = "";
    public string? FeedingInterval { get; set; }
    public string? NextExpected { get; set; }
    public double? DailyFeedTotal { get; set; }
    public string? FormulaProduct { get; set; }
    public int? FormulaAmount { get; set; }
    public int? BreastfeedAmount { get; set; }
    public int? FeedingCount { get; set; }
    public bool? HasUrine { get; set; }
    public bool? HasStool { get; set; }
    public bool? ImmediateNotice { get; set; }
}
