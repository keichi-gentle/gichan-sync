using System.Windows;
using System.Windows.Input;
using GichanDiary.Services;

namespace GichanDiary.Views.Dialogs;

public partial class LoginDialog : Window
{
    private readonly FirebaseAuthService _authService;
    private bool _isAuthenticating;

    public bool LoginSucceeded { get; private set; }

    public LoginDialog(FirebaseAuthService authService, string? defaultEmail = null)
    {
        InitializeComponent();
        _authService = authService;

        if (!string.IsNullOrWhiteSpace(defaultEmail))
            TxtEmail.Text = defaultEmail;

        Loaded += (_, _) =>
        {
            if (string.IsNullOrEmpty(TxtEmail.Text)) TxtEmail.Focus();
            else TxtPassword.Focus();
        };
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        await TryLoginAsync();
    }

    private async void TxtPassword_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await TryLoginAsync();
        }
    }

    private async System.Threading.Tasks.Task TryLoginAsync()
    {
        if (_isAuthenticating) return;

        var email = TxtEmail.Text?.Trim() ?? "";
        var password = TxtPassword.Password ?? "";

        if (string.IsNullOrEmpty(email))
        {
            ShowError("이메일을 입력해 주세요.");
            TxtEmail.Focus();
            return;
        }
        if (string.IsNullOrEmpty(password))
        {
            ShowError("비밀번호를 입력해 주세요.");
            TxtPassword.Focus();
            return;
        }

        _isAuthenticating = true;
        BtnLogin.IsEnabled = false;
        TxtStatus.Foreground = System.Windows.Media.Brushes.LightSkyBlue;
        TxtStatus.Text = "로그인 중...";

        try
        {
            var (ok, errorMsg) = await _authService.SignInWithPasswordAsync(email, password);
            if (ok)
            {
                LoginSucceeded = true;
                DialogResult = true;
                Close();
            }
            else
            {
                ShowError(errorMsg ?? "로그인 실패");
                TxtPassword.Clear();
                TxtPassword.Focus();
            }
        }
        finally
        {
            _isAuthenticating = false;
            BtnLogin.IsEnabled = true;
        }
    }

    private void ShowError(string msg)
    {
        TxtStatus.Foreground = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0xE0, 0x70, 0x70));
        TxtStatus.Text = msg;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        LoginSucceeded = false;
        DialogResult = false;
        Close();
    }
}
