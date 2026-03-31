using System.IO;
using System.Windows;
using System.Windows.Input;
using GichanDiary.Services;
using Microsoft.Win32;

namespace GichanDiary.Views.Dialogs;

public partial class FirstRunDialog : Window
{
    private readonly IExcelService _excelService;

    public string? SelectedFilePath { get; private set; }
    public string? BabyName { get; private set; }
    public DateTime? BabyBirthDate { get; private set; }

    public FirstRunDialog(IExcelService excelService)
    {
        _excelService = excelService;
        InitializeComponent();
    }

    private async void NewFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = "기찬이_이벤트일지.xlsx",
            Title = "새 Excel 파일 생성"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            await _excelService.CreateNewFileAsync(dlg.FileName);
            SelectedFilePath = dlg.FileName;
            TxtFilePath.Text = dlg.FileName;
            BtnStart.IsEnabled = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"파일 생성 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            Title = "기존 Excel 파일 열기"
        };
        if (dlg.ShowDialog() != true) return;

        SelectedFilePath = dlg.FileName;
        TxtFilePath.Text = dlg.FileName;
        BtnStart.IsEnabled = true;
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        BabyName = string.IsNullOrWhiteSpace(TxtBabyName.Text) ? null : TxtBabyName.Text.Trim();
        BabyBirthDate = DpBirthDate.SelectedDate;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, MouseButtonEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Cancel_ButtonClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }
}
