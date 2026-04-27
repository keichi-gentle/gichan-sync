using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GichanDiary.Services;

/// <summary>
/// Firebase Authentication via Email/Password REST API.
/// 흐름:
///   1) 첫 실행: signInWithPassword 호출 (사용자가 LoginDialog에서 입력)
///   2) idToken/refreshToken/email/localId 받아서 ProtectedData로 암호화 저장
///   3) 이후 실행: 저장된 refreshToken으로 토큰 갱신 (TryRestoreSessionAsync)
///   4) 만료 임박 시 자동 갱신
///
/// 보안 규칙은 request.auth.token.email로 역할 매핑하므로
/// idToken은 모든 Firestore REST 요청에 Bearer 헤더로 전달되어야 한다.
/// </summary>
public class FirebaseAuthService
{
    // Firebase Web API Key (firebaseConfig.apiKey와 동일)
    private const string API_KEY = "AIzaSyCy6kZ6NK-WltVMvdI5EGLUM7FndWMCjAA";

    private readonly string _tokenFilePath;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private string? _idToken;
    private string? _refreshToken;
    private string? _userId;
    private string? _email;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public string? UserId => _userId;
    public string? Email => _email;
    public string? IdToken => _idToken;

    /// <summary>
    /// 인증된 상태 = idToken이 존재하고 만료되지 않음.
    /// </summary>
    public bool IsSignedIn => !string.IsNullOrEmpty(_idToken) && DateTime.UtcNow < _tokenExpiry;

    public FirebaseAuthService(string appDir)
    {
        _tokenFilePath = Path.Combine(appDir, "firebase_token.dat");
    }

    /// <summary>
    /// 저장된 토큰 파일이 있으면 refresh를 시도하여 세션 복원.
    /// 실패 시 false → 호출부가 로그인 다이얼로그 표시해야 함.
    /// </summary>
    public async Task<bool> TryRestoreSessionAsync()
    {
        if (!TryLoadSavedToken()) return false;
        if (string.IsNullOrEmpty(_refreshToken)) return false;

        try
        {
            return await RefreshTokenAsync();
        }
        catch (Exception ex)
        {
            LogService.System($"세션 복원 실패: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 이메일/비밀번호로 로그인.
    /// Firebase Auth REST API: identitytoolkit.googleapis.com/v1/accounts:signInWithPassword
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> SignInWithPasswordAsync(string email, string password)
    {
        try
        {
            var body = JsonSerializer.Serialize(new
            {
                email,
                password,
                returnSecureToken = true
            });
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            var response = await _http.PostAsync(
                $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={API_KEY}",
                content);

            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = ParseErrorMessage(json);
                LogService.System($"로그인 실패: {errorMsg}");
                return (false, FriendlyErrorMessage(errorMsg));
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            _idToken = root.GetProperty("idToken").GetString();
            _refreshToken = root.GetProperty("refreshToken").GetString();
            _userId = root.GetProperty("localId").GetString();
            _email = root.GetProperty("email").GetString();
            _tokenExpiry = DateTime.UtcNow.AddSeconds(
                int.Parse(root.GetProperty("expiresIn").GetString() ?? "3600") - 60);

            SaveToken();
            LogService.Event($"Firebase 로그인 성공: {_email}");
            return (true, null);
        }
        catch (Exception ex)
        {
            LogService.System($"로그인 예외: {ex.Message}");
            return (false, "네트워크 오류 또는 서버 응답 실패");
        }
    }

    /// <summary>
    /// refreshToken으로 idToken 재발급.
    /// REST API: securetoken.googleapis.com/v1/token
    /// </summary>
    public async Task<bool> RefreshTokenAsync()
    {
        if (string.IsNullOrEmpty(_refreshToken)) return false;

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = _refreshToken!,
        });

        var response = await _http.PostAsync(
            $"https://securetoken.googleapis.com/v1/token?key={API_KEY}", content);

        if (!response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            LogService.System($"토큰 갱신 실패: {ParseErrorMessage(json)}");
            return false;
        }

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        _idToken = root.GetProperty("id_token").GetString();
        _refreshToken = root.GetProperty("refresh_token").GetString();
        _userId = root.GetProperty("user_id").GetString();
        _tokenExpiry = DateTime.UtcNow.AddSeconds(
            int.Parse(root.GetProperty("expires_in").GetString() ?? "3600") - 60);

        SaveToken();
        return true;
    }

    /// <summary>
    /// 만료 임박 시 자동 갱신. 호출부에서 모든 Firestore 호출 직전에 사용.
    /// </summary>
    public async Task<bool> EnsureValidTokenAsync()
    {
        if (string.IsNullOrEmpty(_idToken)) return false;
        if (DateTime.UtcNow < _tokenExpiry.AddMinutes(-5)) return true; // 5분 이상 남았으면 OK
        return await RefreshTokenAsync();
    }

    public void SignOut()
    {
        _idToken = null;
        _refreshToken = null;
        _userId = null;
        _email = null;
        _tokenExpiry = DateTime.MinValue;
        if (File.Exists(_tokenFilePath))
        {
            try { File.Delete(_tokenFilePath); } catch { }
        }
        LogService.Event("Firebase 로그아웃");
    }

    // ── 토큰 영속화 (Windows DPAPI로 암호화) ──

    private void SaveToken()
    {
        try
        {
            var data = JsonSerializer.Serialize(new
            {
                userId = _userId,
                email = _email,
                refreshToken = _refreshToken,
            });
            var encrypted = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(data), null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(_tokenFilePath, encrypted);
        }
        catch (Exception ex)
        {
            LogService.System($"토큰 저장 실패: {ex.Message}");
        }
    }

    private bool TryLoadSavedToken()
    {
        try
        {
            if (!File.Exists(_tokenFilePath)) return false;
            var encrypted = File.ReadAllBytes(_tokenFilePath);
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(decrypted));
            var root = doc.RootElement;
            _userId = root.GetProperty("userId").GetString();
            _email = root.GetProperty("email").GetString();
            _refreshToken = root.GetProperty("refreshToken").GetString();
            return !string.IsNullOrEmpty(_refreshToken);
        }
        catch
        {
            return false;
        }
    }

    // ── 에러 메시지 파싱/번역 ──

    private static string ParseErrorMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var err)
                && err.TryGetProperty("message", out var msg))
                return msg.GetString() ?? "UNKNOWN";
        }
        catch { }
        return "UNKNOWN";
    }

    private static string FriendlyErrorMessage(string code) => code switch
    {
        "EMAIL_NOT_FOUND"       => "등록되지 않은 이메일입니다.",
        "INVALID_PASSWORD"      => "비밀번호가 올바르지 않습니다.",
        "INVALID_LOGIN_CREDENTIALS" => "이메일 또는 비밀번호가 올바르지 않습니다.",
        "USER_DISABLED"         => "비활성화된 계정입니다.",
        "TOO_MANY_ATTEMPTS_TRY_LATER" => "로그인 시도 한도를 초과했습니다. 잠시 후 다시 시도하세요.",
        "MISSING_PASSWORD"      => "비밀번호를 입력해 주세요.",
        "INVALID_EMAIL"         => "올바른 이메일 형식이 아닙니다.",
        _                       => $"로그인 실패: {code}"
    };
}
