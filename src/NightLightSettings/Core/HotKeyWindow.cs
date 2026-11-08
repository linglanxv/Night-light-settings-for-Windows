namespace NightLightSettings.Core;

/// <summary>
/// 全局快捷键宿主（消息窗口）。Ctrl+Alt+Shift+↑ / ↓ 调节所有显示器色温。
/// </summary>
internal sealed class HotKeyWindow : NativeWindow, IDisposable
{
    public const int IdIncrease = 0x4C01;
    public const int IdDecrease = 0x4C02;
    public const int IdTogglePanel = 0x4C03;

    private readonly List<int> _registered = new();

    public event Action<int>? HotKeyPressed;

    public HotKeyWindow()
    {
        var cp = new CreateParams
        {
            Caption = "NightLightSettingsHotKeyWindow",
            Parent = (IntPtr)(-3) // HWND_MESSAGE：消息窗口，不显示也不进任务栏
        };
        CreateHandle(cp);
    }

    public bool Register(int id, int modifiers, int virtualKey)
    {
        if (Handle == IntPtr.Zero) return false;
        if (!Native.RegisterHotKey(Handle, id, modifiers, virtualKey)) return false;
        _registered.Add(id);
        return true;
    }

    public void UnregisterAll()
    {
        if (Handle == IntPtr.Zero) return;
        foreach (int id in _registered)
        {
            try { Native.UnregisterHotKey(Handle, id); } catch { }
        }
        _registered.Clear();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY)
        {
            HotKeyPressed?.Invoke(m.WParam.ToInt32());
            return;
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        UnregisterAll();
        if (Handle != IntPtr.Zero) DestroyHandle();
    }
}
