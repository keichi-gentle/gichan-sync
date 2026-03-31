using System.Windows.Threading;

namespace GichanDiary.Services;

public class TimerService : ITimerService
{
    private readonly DispatcherTimer _timer;
    public event Action? Tick;

    public TimerService()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (s, e) => Tick?.Invoke();
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();
}
