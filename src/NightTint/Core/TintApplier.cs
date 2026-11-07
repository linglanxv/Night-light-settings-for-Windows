namespace NightTint.Core;

/// <summary>
/// 色温写入线程 + 守护线程逻辑。
/// Windows 在切换分辨率、锁屏、休眠唤醒、驱动重置后会清掉 gamma ramp，
/// 所以这里每 2 秒校验一次，发现被重置就自动写回（这是「记住并保持」的关键）。
/// </summary>
internal sealed class TintApplier : IDisposable
{
    private readonly GammaService _gamma;
    private readonly object _gate = new();
    private readonly Dictionary<string, int> _desired = new(StringComparer.OrdinalIgnoreCase);
    private readonly AutoResetEvent _signal = new(false);
    private readonly Thread _thread;
    private volatile bool _running = true;

    public TintApplier(GammaService gamma)
    {
        _gamma = gamma;
        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = "NightTint.TintApplier",
            Priority = ThreadPriority.BelowNormal
        };
        _thread.Start();
    }

    /// <summary>设置某台显示器的目标色温并立即排队生效。</summary>
    public void Set(string monitorKey, int kelvin)
    {
        lock (_gate)
        {
            _desired[monitorKey] = Math.Clamp(kelvin, ColorTemperature.MinKelvin, ColorTemperature.MaxKelvin);
            _signal.Set();
        }
    }

    public void Forget(string monitorKey)
    {
        lock (_gate) _desired.Remove(monitorKey);
    }

    public int GetDesired(string monitorKey, int fallback)
    {
        lock (_gate) return _desired.TryGetValue(monitorKey, out int value) ? value : fallback;
    }

    private void Loop()
    {
        while (_running)
        {
            bool signaled = _signal.WaitOne(2000);
            if (!_running) return;

            if (signaled)
            {
                Thread.Sleep(16); // 合并连续拖动
                KeyValuePair<string, int>[] batch;
                lock (_gate) batch = _desired.ToArray();

                foreach (var pair in batch)
                {
                    if (!_running) return;
                    var target = _gamma.Find(pair.Key);
                    if (target is null) continue;
                    _gamma.Apply(target, pair.Value);
                }
            }

            Enforce();
        }
    }

    /// <summary>校验硬件上的实际 ramp，被改掉就重新写入。</summary>
    private void Enforce()
    {
        KeyValuePair<string, int>[] snapshot;
        lock (_gate) snapshot = _desired.ToArray();

        foreach (var pair in snapshot)
        {
            if (!_running) return;
            var target = _gamma.Find(pair.Key);
            if (target is null) continue;
            if (_gamma.IsApplied(target, pair.Value)) continue;
            _gamma.Apply(target, pair.Value);
        }
    }

    public void Dispose()
    {
        _running = false;
        _signal.Set();
        try { _thread.Join(800); } catch { }
        _signal.Dispose();
    }
}
