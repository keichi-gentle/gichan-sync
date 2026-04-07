using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GichanDiary.Models;

namespace GichanDiary.Services;

/// <summary>
/// Firestore REST API client for CRUD operations.
/// Uses Firebase Auth ID token for authentication.
/// </summary>
public class FirestoreService
{
    private const string PROJECT_ID = "gichan-diary";
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(100) };
    private string? _userId;
    private string? _idToken;

    private string BaseUrl => $"https://firestore.googleapis.com/v1/projects/{PROJECT_ID}/databases/(default)/documents";
    private string EventsUrl => $"{BaseUrl}/users/{_userId}/events";

    public void SetCredentials(string userId, string? idToken = null)
    {
        _userId = userId;
        _idToken = idToken;
        if (!string.IsNullOrEmpty(idToken))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", idToken);
        else
            _http.DefaultRequestHeaders.Authorization = null;
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_userId);

    // ── CRUD ──

    public async Task<List<BabyEvent>> LoadEventsAsync()
    {
        var events = new List<BabyEvent>();
        var url = $"{EventsUrl}?pageSize=1000&orderBy=date";
        string? nextPageToken = null;

        do
        {
            var reqUrl = nextPageToken != null ? $"{url}&pageToken={nextPageToken}" : url;
            var response = await _http.GetAsync(reqUrl);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("documents", out var docs))
            {
                foreach (var d in docs.EnumerateArray())
                {
                    var evt = MapFromFirestore(d);
                    if (evt != null) events.Add(evt);
                }
            }

            nextPageToken = doc.RootElement.TryGetProperty("nextPageToken", out var token)
                ? token.GetString() : null;
        } while (nextPageToken != null);

        return events.OrderBy(e => e.FullDateTime).ToList();
    }

    public async Task AddEventAsync(BabyEvent evt)
    {
        var docId = evt.Id.ToString();
        var url = $"{EventsUrl}/{docId}";
        var body = MapToFirestore(evt, "wpf", isNew: true);
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), url) { Content = content };
        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateEventAsync(BabyEvent evt)
    {
        var docId = evt.Id.ToString();
        var url = $"{EventsUrl}/{docId}";
        var body = MapToFirestore(evt, "wpf", isNew: false);
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), url) { Content = content };
        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteEventAsync(string eventId)
    {
        var url = $"{EventsUrl}/{eventId}";
        var response = await _http.DeleteAsync(url);
        response.EnsureSuccessStatusCode();
    }

    // ── Mapping: BabyEvent → Firestore JSON ──

    private static string MapToFirestore(BabyEvent evt, string source, bool isNew)
    {
        var fields = new Dictionary<string, object>
        {
            ["id"] = SVal(evt.Id.ToString()),
            ["date"] = TsVal(evt.FullDateTime ?? evt.Date),
            ["category"] = SVal(evt.Category.ToString()),
            ["detail"] = SVal(evt.Detail),
            ["amount"] = SVal(evt.Amount),
            ["note"] = SVal(evt.Note),
            ["source"] = SVal(source),
            ["updatedAt"] = TsVal(DateTime.UtcNow),
        };

        if (isNew) fields["createdAt"] = TsVal(DateTime.UtcNow);
        if (evt.Time.HasValue) fields["time"] = SVal(evt.Time.Value.ToString(@"hh\:mm"));
        if (evt.DayNumber.HasValue) fields["dayNumber"] = IVal(evt.DayNumber.Value);
        if (evt.DailyFeedTotal.HasValue) fields["dailyFeedTotal"] = DVal(evt.DailyFeedTotal.Value);
        if (evt.FormulaProduct != null) fields["formulaProduct"] = SVal(evt.FormulaProduct);
        if (evt.FormulaAmount.HasValue) fields["formulaAmount"] = IVal(evt.FormulaAmount.Value);
        if (evt.BreastfeedAmount.HasValue) fields["breastfeedAmount"] = IVal(evt.BreastfeedAmount.Value);
        if (evt.FeedingCount.HasValue) fields["feedingCount"] = IVal(evt.FeedingCount.Value);
        if (evt.FeedingInterval.HasValue) fields["feedingInterval"] = SVal(evt.FeedingInterval.Value.ToString(@"hh\:mm"));
        if (evt.NextExpected.HasValue) fields["nextExpected"] = SVal(evt.NextExpected.Value.ToString(@"hh\:mm"));
        if (evt.HasUrine.HasValue) fields["hasUrine"] = BVal(evt.HasUrine.Value);
        if (evt.HasStool.HasValue) fields["hasStool"] = BVal(evt.HasStool.Value);
        if (evt.ImmediateNotice.HasValue) fields["immediateNotice"] = BVal(evt.ImmediateNotice.Value);

        return JsonSerializer.Serialize(new { fields });
    }

    // ── Mapping: Firestore JSON → BabyEvent ──

    private static BabyEvent? MapFromFirestore(JsonElement doc)
    {
        if (!doc.TryGetProperty("fields", out var f)) return null;

        var evt = new BabyEvent
        {
            Detail = GetStr(f, "detail") ?? "",
            Amount = GetStr(f, "amount") ?? "-",
            Note = GetStr(f, "note") ?? "",
        };

        var idStr = GetStr(f, "id");
        if (Guid.TryParse(idStr, out var guid)) evt.Id = guid;

        var catStr = GetStr(f, "category") ?? "기타";
        if (Enum.TryParse<EventCategory>(catStr, out var cat)) evt.Category = cat;
        else evt.Category = EventCategory.기타;

        if (GetTimestamp(f, "date") is DateTime dt)
        {
            evt.Date = dt.Date;
            if (dt.TimeOfDay.TotalSeconds > 0)
                evt.Time = dt.TimeOfDay;
        }

        var timeStr = GetStr(f, "time");
        if (timeStr != null && TimeSpan.TryParse(timeStr, out var ts))
            evt.Time = ts;

        evt.DayNumber = GetInt(f, "dayNumber");
        evt.DailyFeedTotal = GetDouble(f, "dailyFeedTotal");
        evt.FormulaProduct = GetStr(f, "formulaProduct");
        evt.FormulaAmount = GetInt(f, "formulaAmount");
        evt.BreastfeedAmount = GetInt(f, "breastfeedAmount");
        evt.FeedingCount = GetInt(f, "feedingCount");
        evt.HasUrine = GetBool(f, "hasUrine");
        evt.HasStool = GetBool(f, "hasStool");
        evt.ImmediateNotice = GetBool(f, "immediateNotice");

        var intervalStr = GetStr(f, "feedingInterval");
        if (intervalStr != null && TimeSpan.TryParse(intervalStr, out var fi))
            evt.FeedingInterval = fi;

        return evt;
    }

    // ── Firestore value helpers ──
    private static object SVal(string v) => new { stringValue = v };
    private static object IVal(int v) => new { integerValue = v.ToString() };
    private static object DVal(double v) => new { doubleValue = v };
    private static object BVal(bool v) => new { booleanValue = v };
    private static object TsVal(DateTime v) => new { timestampValue = v.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ") };

    private static string? GetStr(JsonElement f, string key)
    {
        if (f.TryGetProperty(key, out var prop) && prop.TryGetProperty("stringValue", out var sv))
            return sv.GetString();
        return null;
    }

    private static int? GetInt(JsonElement f, string key)
    {
        if (f.TryGetProperty(key, out var prop) && prop.TryGetProperty("integerValue", out var iv))
            return int.TryParse(iv.GetString(), out var n) ? n : null;
        return null;
    }

    private static double? GetDouble(JsonElement f, string key)
    {
        if (f.TryGetProperty(key, out var prop) && prop.TryGetProperty("doubleValue", out var dv))
            return dv.GetDouble();
        return null;
    }

    private static bool? GetBool(JsonElement f, string key)
    {
        if (f.TryGetProperty(key, out var prop) && prop.TryGetProperty("booleanValue", out var bv))
            return bv.GetBoolean();
        return null;
    }

    private static DateTime? GetTimestamp(JsonElement f, string key)
    {
        if (f.TryGetProperty(key, out var prop) && prop.TryGetProperty("timestampValue", out var tv))
            return DateTime.TryParse(tv.GetString(), out var dt) ? dt.ToLocalTime() : null;
        return null;
    }
}
