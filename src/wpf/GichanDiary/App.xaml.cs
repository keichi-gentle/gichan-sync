using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using GichanDiary.Services;
using GichanDiary.ViewModels;
using GichanDiary.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace GichanDiary;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        LogService.Initialize(appDir);
        LogService.System("프로그램 시작");

        // Global unhandled exception handlers
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            LogService.System($"비정상 종료 (UnhandledException): {ex?.Message}\n{ex?.StackTrace}");
        };
        DispatcherUnhandledException += (s, args) =>
        {
            LogService.System($"UI 스레드 예외: {args.Exception.Message}\n{args.Exception.StackTrace}");
            args.Handled = true;
            MessageBox.Show($"오류가 발생했습니다:\n{args.Exception.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        };
        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            LogService.System($"비동기 예외: {args.Exception?.Message}\n{args.Exception?.StackTrace}");
            args.SetObserved();
        };

        var services = new ServiceCollection();

        // Services
        services.AddSingleton<ISettingsService>(_ => new SettingsService(appDir));
        services.AddSingleton<IExcelService>(sp => new ExcelService(sp.GetRequiredService<ISettingsService>()));
        services.AddSingleton<ICalculationService, CalculationService>();
        services.AddSingleton<ITimerService, TimerService>();

        // Firebase services
        services.AddSingleton(_ => new FirebaseAuthService(appDir));
        services.AddSingleton<FirestoreService>();
        services.AddSingleton<SyncCoordinator>();
        services.AddSingleton<IDataService>(sp =>
        {
            var coordinator = sp.GetRequiredService<SyncCoordinator>();
            return coordinator.GetDataService();
        });

        // ViewModels
        services.AddTransient<MainViewModel>();

        // Windows
        services.AddSingleton<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        var excelService = _serviceProvider.GetRequiredService<IExcelService>();

        // 설정 로드 (실패 시 기본값)
        var settings = settingsService.Load();
        LogService.System($"설정 로드 완료: Theme={settings.Theme}, Excel={settings.ExcelFilePath}");

        // 테마를 먼저 적용 (FirstRunDialog/MainWindow 생성 전에 적용되어야 첫 렌더링이 올바른 테마로 됨)
        Services.ThemeManager.ApplyTheme(settings.Theme);

        // Excel 경로가 없거나 파일이 존재하지 않으면 → 첫 실행 다이얼로그
        var needFirstRun = string.IsNullOrEmpty(settings.ExcelFilePath)
                        || !File.Exists(settings.ExcelFilePath);

        if (needFirstRun)
        {
            var dialog = new FirstRunDialog(excelService);

            // 기존 설정이 있으면 다이얼로그에 미리 채우기
            if (!string.IsNullOrEmpty(settings.BabyName))
                dialog.TxtBabyName.Text = settings.BabyName;
            if (settings.BabyBirthDate.HasValue)
                dialog.DpBirthDate.SelectedDate = settings.BabyBirthDate;

            var result = dialog.ShowDialog();

            if (result != true || string.IsNullOrEmpty(dialog.SelectedFilePath))
            {
                Shutdown();
                return;
            }

            settings.ExcelFilePath = dialog.SelectedFilePath;
            if (!string.IsNullOrEmpty(dialog.BabyName))
                settings.BabyName = dialog.BabyName;
            if (dialog.BabyBirthDate.HasValue)
                settings.BabyBirthDate = dialog.BabyBirthDate;

            // 기존 Excel 파일 열기 시 분유 제품 자동 추가 (#2 피드백)
            if (File.Exists(settings.ExcelFilePath))
            {
                try
                {
                    var events = await excelService.LoadEventsAsync(settings.ExcelFilePath);
                    var existingProducts = new HashSet<string>(settings.FormulaProducts);
                    var newProducts = events
                        .Where(ev => ev.IsFeeding && !string.IsNullOrEmpty(ev.FormulaProduct))
                        .Select(ev => ev.FormulaProduct!)
                        .Distinct()
                        .Where(p => !existingProducts.Contains(p))
                        .ToList();

                    if (newProducts.Count > 0)
                        settings.FormulaProducts.AddRange(newProducts);

                    // 기본 분유 제품이 목록에 없으면 첫 번째로 설정
                    if (!settings.FormulaProducts.Contains(settings.DefaultFormulaProduct)
                        && settings.FormulaProducts.Count > 0)
                        settings.DefaultFormulaProduct = settings.FormulaProducts[0];
                }
                catch
                {
                    // 첫 실행 시 로드 실패해도 계속 진행
                }
            }

            settingsService.Save(settings);
        }

        // (테마는 이미 OnStartup 초기에 적용됨)

        // Excel 파일이 없으면 새로 생성
        if (!File.Exists(settings.ExcelFilePath))
        {
            try
            {
                var dir = Path.GetDirectoryName(settings.ExcelFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                await excelService.CreateNewFileAsync(settings.ExcelFilePath!);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Excel 파일 생성 실패:\n{ex.Message}\n\n프로그램을 종료합니다.",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }
        }

        // 자동 백업 (하루 1회) — 실패해도 앱 시작은 계속
        if (File.Exists(settings.ExcelFilePath))
        {
            try
            {
                await excelService.CreateAutoBackupIfNeededAsync(settings.ExcelFilePath);
                LogService.System("자동 백업 완료");
            }
            catch (Exception ex)
            {
                LogService.System($"자동 백업 실패: {ex.Message}");
                MessageBox.Show($"자동 백업 실패 (앱은 정상 시작됩니다):\n{ex.Message}",
                    "백업 경고", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Firebase 항상 활성화 (올리기/내려받기 수동 동기화용, 자동 폴링 없음)
        // 1) 저장된 토큰으로 세션 복원 시도 → 2) 실패 시 LoginDialog 띄움 → 3) 사용자 취소 시 ExcelOnly
        try
        {
            var coordinator = _serviceProvider.GetRequiredService<SyncCoordinator>();
            var authService = _serviceProvider.GetRequiredService<FirebaseAuthService>();
            var syncEnabled = await coordinator.TryEnableFirebaseSync();
            if (!syncEnabled)
            {
                // 저장된 세션 없음/만료 → 로그인 다이얼로그 표시
                var loginDialog = new LoginDialog(authService, defaultEmail: authService.Email);
                var loginResult = loginDialog.ShowDialog();
                if (loginResult == true && loginDialog.LoginSucceeded)
                {
                    coordinator.OnSignedIn();
                    syncEnabled = true;
                }
            }
            LogService.System($"Firebase 연결: {(syncEnabled ? "성공" : "취소/실패 (ExcelOnly로 계속)")}");
        }
        catch (Exception ex)
        {
            LogService.System($"Firebase 연결 실패 (ExcelOnly로 계속): {ex.Message}");
        }

        // Initialize and show
        try
        {
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            var viewModel = (MainViewModel)mainWindow.DataContext;
            await viewModel.InitializeAsync();

            mainWindow.Show();
            LogService.System("메인 윈도우 표시 완료");
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            MainWindow = mainWindow;
        }
        catch (Exception ex)
        {
            LogService.System($"프로그램 시작 실패: {ex.Message}");
            MessageBox.Show($"프로그램 시작 실패:\n{ex.Message}",
                "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        LogService.System("프로그램 정상 종료");
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
