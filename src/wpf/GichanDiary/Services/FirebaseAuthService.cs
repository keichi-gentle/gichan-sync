using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GichanDiary.Services;

/// <summary>
/// Firebase Authentication via Google OAuth desktop flow.
/// Opens browser → receives auth code on localhost → exchanges for Firebase tokens.
/// </summary>
public class FirebaseAuthService
{
    // Firebase Web API Key (from firebaseConfig)
    private const string API_KEY = "AIzaSyCy6kZ6NK-WltVMvdI5EGLUM7FndWMCjAA";
    // Google OAuth Client ID (from Firebase console → Authentication → Settings → Web SDK)
    // For desktop OAuth, we use the same web client ID
    private const string CLIENT_ID = "1051684985650-placeholder.apps.googleusercontent.com";

    private readonly string _tokenFilePath;
    private string? _idToken;
    private string? _refreshToken;
    private string? _userId;
    private string? _email;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public string? UserId => _userId;
    public string? Email => _email;
    public string? IdToken => _idToken;
    // Test mode: UID만으로 로그인 허용 (Firestore test mode rules)
    // Production: idToken 필수로 전환 필요
    public bool IsSignedIn => !string.IsNullOrEmpty(_userId);

    public FirebaseAuthService(string appDir)
    {
        _tokenFilePath = Path.Combine(appDir, "firebase_token.dat");
        TryLoadSavedToken();
    }

    /// <summary>
    /// Sign in using Firebase Auth REST API with email (simplified for single-user).
    /// Uses the anonymous sign-in or custom token approach.
    /// For now, use saved UID from migration — full OAuth to be added later.
    /// </summary>
    public async Task<bool> SignInWithSavedUidAsync(string uid)
    {
        _userId = uid;
        // In test mode, Firestore allows unauthenticated access
        // For production, implement full Google OAuth desktop flow
        _idToken = null; // Will work with test mode rules
        return true;
    }

    /// <summary>
    /// Sign in with Google OAuth via browser redirect.
    /// </summary>
    public async Task<bool> SignInWithGoogleAsync()
    {
        try
        {
            // Step 1: Start local HTTP listener for OAuth callback
            var redirectPort = 48234;
            var redirectUri = $"http://localhost:{redirectPort}/callback";
            var state = Guid.NewGuid().ToString("N");

            using var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{redirectPort}/");
            listener.Start();

            // Step 2: Open browser for Google OAuth
            var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth"
                + $"?client_id={CLIENT_ID}"
                + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
                + $"&response_type=code"
                + $"&scope=openid%20email%20profile"
                + $"&state={state}"
                + $"&access_type=offline";

            Process.Start(new ProcessStartInfo { FileName = authUrl, UseShellExecute = true });

            // Step 3: Wait for callback
            var context = await listener.GetContextAsync();
            var code = context.Request.QueryString["code"];
            var returnedState = context.Request.QueryString["state"];

            // Respond to browser
            var responseBytes = Encoding.UTF8.GetBytes("<html><body><h2>로그인 완료! 이 창을 닫아도 됩니다.</h2></body></html>");
            context.Response.ContentLength64 = responseBytes.Length;
            await context.Response.OutputStream.WriteAsync(responseBytes);
            context.Response.Close();
            listener.Stop();

            if (string.IsNullOrEmpty(code) || returnedState != state)
                return false;

            // Step 4: Exchange code for tokens via Firebase
            // This requires a server-side exchange (Google OAuth code → Firebase custom token)
            // For simplicity in single-user scenario, use Firebase Auth signInWithIdp
            // TODO: Implement full exchange

            return false; // Placeholder for full OAuth flow
        }
        catch (Exception ex)
        {
            LogService.System($"Google OAuth failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Refresh ID token using refresh token.
    /// </summary>
    public async Task<bool> RefreshTokenAsync()
    {
        if (string.IsNullOrEmpty(_refreshToken)) return false;

        using var http = new HttpClient();
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = _refreshToken,
        });

        var response = await http.PostAsync(
            $"https://securetoken.googleapis.com/v1/token?key={API_KEY}", content);

        if (!response.IsSuccessStatusCode) return false;

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        _idToken = doc.RootElement.GetProperty("id_token").GetString();
        _refreshToken = doc.RootElement.GetProperty("refresh_token").GetString();
        _tokenExpiry = DateTime.Now.AddSeconds(
            int.Parse(doc.RootElement.GetProperty("expires_in").GetString() ?? "3600"));

        SaveToken();
        return true;
    }

    public void SignOut()
    {
        _idToken = null;
        _refreshToken = null;
        _userId = null;
        _email = null;
        _tokenExpiry = DateTime.MinValue;
        if (File.Exists(_tokenFilePath))
            File.Delete(_tokenFilePath);
    }

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
        catch { /* ignore save errors */ }
    }

    private void TryLoadSavedToken()
    {
        try
        {
            if (!File.Exists(_tokenFilePath)) return;
            var encrypted = File.ReadAllBytes(_tokenFilePath);
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            var data = JsonSerializer.Deserialize<JsonElement>(Encoding.UTF8.GetString(decrypted));
            _userId = data.GetProperty("userId").GetString();
            _email = data.GetProperty("email").GetString();
            _refreshToken = data.GetProperty("refreshToken").GetString();
        }
        catch { /* ignore load errors */ }
    }
}
