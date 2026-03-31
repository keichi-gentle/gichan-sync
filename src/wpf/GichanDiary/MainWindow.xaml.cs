using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using GichanDiary.ViewModels;

namespace GichanDiary;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly DispatcherTimer _toastTimer;
    private INotifyPropertyChanged? _currentChildVm;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Timer for auto-dismiss (2 seconds)
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            HideToast();
        };

        // Listen for CurrentView changes to track child VM StatusMessage
        viewModel.PropertyChanged += OnMainViewModelPropertyChanged;
    }

    private void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentView))
        {
            // Unsubscribe from previous child VM
            if (_currentChildVm != null)
            {
                _currentChildVm.PropertyChanged -= OnChildVmPropertyChanged;
                _currentChildVm = null;
            }

            // Subscribe to new child VM
            var mainVm = (MainViewModel)DataContext;
            if (mainVm.CurrentView is INotifyPropertyChanged childVm)
            {
                _currentChildVm = childVm;
                _currentChildVm.PropertyChanged += OnChildVmPropertyChanged;
            }
        }
        else if (e.PropertyName == "StatusMessage")
        {
            // MainViewModel 자체의 StatusMessage도 토스트 표시
            var mainVm = (MainViewModel)DataContext;
            if (!string.IsNullOrWhiteSpace(mainVm.StatusMessage))
                ShowToast(mainVm.StatusMessage);
        }
    }

    private void OnChildVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "StatusMessage") return;

        // Use reflection to get StatusMessage from the child VM
        var prop = sender?.GetType().GetProperty("StatusMessage");
        if (prop == null) return;

        var message = prop.GetValue(sender) as string;
        if (!string.IsNullOrWhiteSpace(message))
        {
            ShowToast(message);
        }
    }

    private void ShowToast(string message)
    {
        _toastTimer.Stop();

        ToastText.Text = message;
        ToastBorder.Visibility = Visibility.Visible;

        // Fade in
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        ToastBorder.BeginAnimation(OpacityProperty, fadeIn);

        // Start auto-dismiss timer
        _toastTimer.Start();
    }

    private void HideToast()
    {
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        fadeOut.Completed += (_, _) =>
        {
            ToastBorder.Visibility = Visibility.Collapsed;
        };
        ToastBorder.BeginAnimation(OpacityProperty, fadeOut);
    }

    // ── Title bar handlers ──────────────────────────────

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MaximizeClick(sender, e);
        }
        else
        {
            DragMove();
        }
    }

    private void MinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
