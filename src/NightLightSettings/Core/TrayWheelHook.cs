using System.Runtime.InteropServices;

namespace NightLightSettings.Core;

/// <summary>
/// 托盘图标滚轮支持。NotifyIcon 本身收不到滚轮事件，
/// 这里用低级鼠标钩子判断「光标是否停在通知区域」，是则拦截滚轮用于调亮度。
/// </summary>
internal sealed class TrayWheelHook : IDisposable
{
    private readonly Native.LowLevelMouseProc _callback;
    private IntPtr _hook = IntPtr.Zero;

    public bool Enabled { get; set; } = true;

    /// <summary>+1 = 向上滚（变亮），-1 = 向下滚（变暗）。</summary>
    public event Action<int>? WheelTurned;

    public TrayWheelHook()
    {
        _callback = HookCallback;
    }

    public bool IsRunning => _hook != IntPtr.Zero;

    public void Start()
    {
        if (_hook != IntPtr.Zero) return;
        _hook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _callback, IntPtr.Zero, 0);
    }

    public void Stop()
    {
        if (_hook == IntPtr.Zero) return;
        Native.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0 && Enabled && wParam == (IntPtr)Native.WM_MOUSEWHEEL)
            {
                var data = Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(lParam);
                if (IsOverNotificationArea(data.pt))
                {
                    int delta = (short)((data.mouseData >> 16) & 0xFFFF);
                    if (delta != 0)
                        WheelTurned?.Invoke(delta > 0 ? 1 : -1);
                }
            }
        }
        catch
        {
            // 钩子回调里任何异常都不能抛出去
        }

        return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    /// <summary>精确判断点是否落在任务栏的「通知区域」矩形内。</summary>
    private static bool IsOverNotificationArea(Native.POINT point)
    {
        IntPtr taskbar = Native.FindWindow("Shell_TrayWnd", null);
        if (taskbar != IntPtr.Zero)
        {
            IntPtr notify = Native.FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
            if (notify != IntPtr.Zero && Contains(notify, point)) return true;
        }

        // 隐藏图标的溢出面板
        foreach (string cls in new[] { "NotifyIconOverflowWindow", "TopLevelWindowForOverflowXamlIsland" })
        {
            IntPtr overflow = Native.FindWindow(cls, null);
            if (overflow != IntPtr.Zero && Native.IsWindowVisible(overflow) && Contains(overflow, point))
                return true;
        }

        return false;
    }

    private static bool Contains(IntPtr hwnd, Native.POINT point)
    {
        if (!Native.GetWindowRect(hwnd, out Native.RECT rect)) return false;
        return point.X >= rect.Left && point.X < rect.Right && point.Y >= rect.Top && point.Y < rect.Bottom;
    }

    public void Dispose() => Stop();
}
