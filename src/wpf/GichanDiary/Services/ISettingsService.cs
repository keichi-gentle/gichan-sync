using GichanDiary.Models;
namespace GichanDiary.Services;

public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}
