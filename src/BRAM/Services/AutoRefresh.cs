using System;
using System.Threading;
using System.Threading.Tasks;

namespace RobloxAccountManager.Services;

public sealed class PresenceRefresher
{
    private readonly Func<CancellationToken, Task> _sweep;
    private readonly Func<int> _intervalSeconds;
    private CancellationTokenSource? _cts;

    public PresenceRefresher(Func<CancellationToken, Task> sweep, Func<int> intervalSeconds)
    {
        _sweep = sweep;
        _intervalSeconds = intervalSeconds;
    }

    public void Start()
    {
        Stop();
        int seconds = _intervalSeconds();
        if (seconds <= 0) return;
        _cts = new CancellationTokenSource();
        _ = LoopAsync(seconds, _cts.Token);
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        _cts?.Dispose();
        _cts = null;
    }

    private async Task LoopAsync(int seconds, CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));
            while (await timer.WaitForNextTickAsync(ct))
            {
                try { await _sweep(ct); }
                catch (OperationCanceledException) { throw; }
                catch { }
            }
        }
        catch (OperationCanceledException) { }
    }
}
