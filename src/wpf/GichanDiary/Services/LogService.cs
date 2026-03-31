using System.IO;

namespace GichanDiary.Services;

public static class LogService
{
    private static string _logDir = "";

    public static void Initialize(string appDir)
    {
        _logDir = Path.Combine(appDir, "log");
        if (!Directory.Exists(_logDir)) Directory.CreateDirectory(_logDir);
    }

    /// <summary>EventLog: 사용자 액션 (기록 추가/수정/삭제, 백업, Import/Export, 설정 변경)</summary>
    public static void Event(string message) { WriteLog("EventLog", message); }

    /// <summary>SystemLog: 앱 시작/종료, 예외, 실패</summary>
    public static void System(string message) { WriteLog("SystemLog", message); }

    private static void WriteLog(string prefix, string message)
    {
        if (string.IsNullOrEmpty(_logDir)) return;

        var now = DateTime.Now;
        var fileName = $"{prefix}_{now:yyyyMMdd}.log";
        var filePath = Path.Combine(_logDir, fileName);
        var line = $"{now:yyyy/MM/dd HH:mm:ss} {message}";
        // Thread-safe append
        try { File.AppendAllText(filePath, line + Environment.NewLine); } catch { }
    }
}
