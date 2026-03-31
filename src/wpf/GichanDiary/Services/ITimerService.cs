namespace GichanDiary.Services;

public interface ITimerService
{
    event Action? Tick;
    void Start();
    void Stop();
}
