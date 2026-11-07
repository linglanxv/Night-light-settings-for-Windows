using System.Runtime.InteropServices;

namespace NightTint.Core;

/// <summary>
/// 显示器枚举 + gamma ramp 读写。
/// 每台显示器一个 GDI DC，句柄在进程内复用，避免频繁 CreateDC。
/// </summary>
internal sealed class GammaService : IDisposable
{
    private readonly object _gate = new();
    private readonly List<DisplayTarget> _targets = new();
    private bool _disposed;

    public IReadOnlyList<DisplayTarget> Targets
    {
        get { lock (_gate) return _targets.ToArray(); }
    }

    public void Refresh()
    {
        lock (_gate)
        {
            ReleaseDc();
            _targets.Clear();
            if (_disposed) return;

            var found = new List<DisplayTarget>();

            Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref Native.RECT rect, IntPtr data) =>
            {
                var info = new Native.MONITORINFOEX
                {
                    cbSize = Marshal.SizeOf<Native.MONITORINFOEX>(),
                    szDevice = string.Empty
                };
                if (!Native.GetMonitorInfo(hMon, ref info)) return true;

                string deviceName = info.szDevice ?? string.Empty;
                string friendly = Native.GetMonitorFriendlyName(deviceName);

                var target = new DisplayTarget
                {
                    DeviceName = deviceName,
                    Name = string.IsNullOrWhiteSpace(friendly) ? deviceName : friendly,
                    Bounds = rect.ToRectangle(),
                    IsPrimary = (info.dwFlags & Native.MONITORINFOF_PRIMARY) != 0
                };

                found.Add(target);
                return true;
            }, IntPtr.Zero);

            // 按物理位置排序：先上后下、先左后右
            found.Sort((a, b) =>
            {
                int byTop = a.Bounds.Top.CompareTo(b.Bounds.Top);
                return byTop != 0 ? byTop : a.Bounds.Left.CompareTo(b.Bounds.Left);
            });

            var counters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var target in found)
            {
                string baseKey = string.IsNullOrWhiteSpace(target.Name) ? target.DeviceName : target.Name.Trim();
                if (string.IsNullOrWhiteSpace(baseKey)) baseKey = "Display";
                counters.TryGetValue(baseKey, out int seen);
                counters[baseKey] = seen + 1;
                target.Key = seen == 0 ? baseKey : $"{baseKey}#{seen + 1}";

                target.Hdc = Native.CreateDC(null, target.DeviceName, null, IntPtr.Zero);
                target.Applicable = target.Hdc != IntPtr.Zero;
            }

            _targets.AddRange(found);
        }
    }

    /// <summary>把色温对应的 ramp 写到显示器。</summary>
    public bool Apply(DisplayTarget target, int kelvin)
    {
        lock (_gate)
        {
            if (target.Hdc == IntPtr.Zero) return false;
            var ramp = ColorTemperature.BuildRamp(kelvin);
            bool ok = Native.SetDeviceGammaRamp(target.Hdc, ramp);
            target.Applicable = ok;
            return ok;
        }
    }

    /// <summary>当前硬件上的 ramp 是否已经是我们想要的值（用于刷新后自动恢复）。</summary>
    public bool IsApplied(DisplayTarget target, int kelvin)
    {
        lock (_gate)
        {
            if (target.Hdc == IntPtr.Zero) return false;

            var desired = ColorTemperature.BuildRamp(kelvin);
            var current = new ushort[768];
            if (!Native.GetDeviceGammaRamp(target.Hdc, current)) return false;

            // 允许驱动量化误差
            for (int i = 0; i < 768; i++)
            {
                if (Math.Abs(current[i] - desired[i]) > 512) return false;
            }
            return true;
        }
    }

    /// <summary>读取当前第一个通道的中间值，主要用于诊断。</summary>
    public bool TryReadRamp(DisplayTarget target, out ushort[] ramp)
    {
        ramp = new ushort[768];
        lock (_gate)
        {
            if (target.Hdc == IntPtr.Zero) return false;
            return Native.GetDeviceGammaRamp(target.Hdc, ramp);
        }
    }

    public DisplayTarget? Find(string key)
    {
        lock (_gate)
            return _targets.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    private void ReleaseDc()
    {
        foreach (var target in _targets)
        {
            if (target.Hdc != IntPtr.Zero)
            {
                try { Native.DeleteDC(target.Hdc); } catch { }
                target.Hdc = IntPtr.Zero;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            ReleaseDc();
            _targets.Clear();
        }
    }
}
