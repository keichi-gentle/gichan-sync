using GichanDiary.Models;

namespace GichanDiary.Helpers;

public static class ExcelColumnMapper
{
    private static readonly Dictionary<string, EventCategory> ExcelToEnum = new()
    {
        ["수유"] = EventCategory.수유,
        ["배변"] = EventCategory.배변,
        ["위생"] = EventCategory.위생관리,
        ["위생관리"] = EventCategory.위생관리,  // 하위 호환 (기존 파일)
        ["신체"] = EventCategory.신체측정,
        ["건강"] = EventCategory.건강관리,
        ["통증"] = EventCategory.건강관리,      // 하위 호환 (기존 파일)
        ["기타"] = EventCategory.기타,
    };

    // 신규 저장 시 사용하는 Excel 값
    private static readonly Dictionary<EventCategory, string> EnumToExcel = new()
    {
        [EventCategory.수유] = "수유",
        [EventCategory.배변] = "배변",
        [EventCategory.위생관리] = "위생",
        [EventCategory.신체측정] = "신체",
        [EventCategory.건강관리] = "건강",
        [EventCategory.기타] = "기타",
    };

    public static EventCategory ParseCategory(string excelValue)
        => ExcelToEnum.TryGetValue(excelValue.Trim(), out var cat) ? cat : EventCategory.기타;

    public static string ToCategoryString(EventCategory category)
        => EnumToExcel.TryGetValue(category, out var str) ? str : "기타";

    public static int? ParseAmount(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "-") return null;
        return int.TryParse(value, out var n) ? n : null;
    }

    public static string FormatAmount(int? value) => value.HasValue ? value.Value.ToString() : "-";
}
