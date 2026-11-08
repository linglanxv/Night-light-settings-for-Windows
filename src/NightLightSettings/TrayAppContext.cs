using System.Diagnostics;
using Microsoft.Win32;
using NightLightSettings.Core;
using NightLightSettings.Settings;
using NightLightSettings.UI;

namespace NightLightSettings;

/// <summary>
/// 托盘主控：图标、夜间模式面板、快捷键、滚轮、强度记忆、显示器热插拔 / 锁屏 / 唤醒后重新套用。
/// </summary>
internal sealed class TrayAppContext : ApplicationContext
{
    private readonly AppSettings _settings;
    private readonly GammaService _gamma = new();
    private readonly TintApplier _applier;
    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _menu;
    private readonly HotKeyWindow _hotKeys = new();
    private readonly TrayWheelHook _wheelHook = new();
    private readonly System.Windows.Forms.Timer _saveTimer;
    private readonly System.Windows.Forms.Timer _displayTimer;
    private readonly SynchronizationContext _ui;
    private readonly ToolStripMenuItem _enableItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly ToolStripMenuItem _wheelItem;
    private readonly ToolStripMenuItem _hotkeyItem;

    private FlyoutForm? _flyout;
    private SettingsForm? _settingsForm;
    private Icon? _trayIcon;
    private bool _suppressMenuEvents;
    private bool _applying;
    private bool _hotkeyWarned;
    private bool _disposed;

    public TrayAppContext(string? initialCommand = null)
    {
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _settings = AppSettings.Load();
        _gamma.Refresh();
        _applier = new TintApplier(_gamma);

        _saveTimer = new System.Windows.Forms.Timer { Interval = 600 };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            _settings.Save();
        };

        _displayTimer = new System.Windows.Forms.Timer { Interval = 900 };
        _displayTimer.Tick += (_, _) =>
        {
            _displayTimer.Stop();
            RefreshDisplays();
        };

        // 总开关：临时停用 ≠ 退出。关闭后屏幕回到中性白，滑块位置全部记住。
        _enableItem = new ToolStripMenuItem("启用夜间模式") { CheckOnClick = true };
        _enableItem.Click += (_, _) =>
        {
            if (_suppressMenuEvents) return;
            SetEnabled(_enableItem.Checked);
        };

        _startupItem = new ToolStripMenuItem("开机自动启动") { CheckOnClick = true };
        _startupItem.Click += (_, _) =>
        {
            if (_suppressMenuEvents) return;
            SetStartWithWindows(_startupItem.Checked);
        };

        _wheelItem = new ToolStripMenuItem("托盘滚轮调节") { CheckOnClick = true };
        _wheelItem.Click += (_, _) =>
        {
            if (_suppressMenuEvents) return;
            SetWheelEnabled(_wheelItem.Checked);
        };

        _hotkeyItem = new ToolStripMenuItem("全局快捷键 (Ctrl+Alt+Shift+↑/↓)") { CheckOnClick = true };
        _hotkeyItem.Click += (_, _) =>
        {
            if (_suppressMenuEvents) return;
            _settings.HotkeysEnabled = _hotkeyItem.Checked;
            RegisterHotKeys();
            ScheduleSave();
        };

        _menu = BuildMenu();
        _menu.Opening += (_, _) => SyncMenuState();

        _tray = new NotifyIcon
        {
            Visible = true,
            ContextMenuStrip = _menu,
            Text = AppInfo.DisplayName
        };
        _tray.MouseUp += OnTrayMouseUp;

        _hotKeys.HotKeyPressed += OnHotKey;
        RegisterHotKeys();

        _wheelHook.Enabled = _settings.WheelOnTray;
        _wheelHook.WheelTurned += OnWheelTurned;
        if (_settings.WheelOnTray) _wheelHook.Start();

        StartupRegistrar.Apply(_settings.StartWithWindows);

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;

        RestoreAll();
        ShowFirstRunTip();

        // 接收后续实例发来的命令（--show / --settings / --on / --off / --toggle）
        InstancePipe.StartServer(cmd => RunOnUi(() => ApplyCommand(cmd)));

        // 本次启动自带的命令也按同一套逻辑处理
        if (initialCommand is not null) ApplyCommand(initialCommand);
    }

    /// <summary>处理外部命令：面板、设置、启用 / 暂停。</summary>
    private void ApplyCommand(string command)
    {
        switch (command)
        {
            case "show":
                ShowPanel(takeFocus: true);
                break;
            case "settings":
                ShowSettings();
                break;
            case "on":
                SetEnabled(true);
                break;
            case "off":
                SetEnabled(false);
                break;
            case "toggle":
                SetEnabled(!_settings.Enabled);
                break;
        }
    }

    // =====================================================================
    // 菜单
    // =====================================================================

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip
        {
            Renderer = new DarkMenuRenderer(),
            BackColor = Theme.BackgroundElevated,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI", 9f)
        };

        var open = new ToolStripMenuItem("打开夜间模式面板") { Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        open.Click += (_, _) => ToggleFlyout();

        _enableItem.Font = new Font("Segoe UI", 9f, FontStyle.Bold);

        var presets = new ToolStripMenuItem("全部显示器强度");
        var turnOff = new ToolStripMenuItem("关闭夜间模式 (0%)");
        turnOff.Click += (_, _) => ApplyToAll(0);
        presets.DropDownItems.Add(turnOff);
        presets.DropDownItems.Add(new ToolStripSeparator());
        foreach (int strength in new[] { 25, 50, 75, 100 })
        {
            int value = strength;
            var item = new ToolStripMenuItem($"{value}% · {ColorTemperature.Describe(value)}（{ColorTemperature.StrengthToKelvin(value)}K）");
            item.Click += (_, _) => ApplyToAll(value);
            presets.DropDownItems.Add(item);
        }

        var settings = new ToolStripMenuItem("设置…");
        settings.Click += (_, _) => ShowSettings();

        var refresh = new ToolStripMenuItem("刷新显示器");
        refresh.Click += (_, _) => RefreshDisplays();

        var openConfig = new ToolStripMenuItem("打开配置文件位置");
        openConfig.Click += (_, _) => OpenConfigFolder();

        var exit = new ToolStripMenuItem($"退出 {AppInfo.DisplayName}");
        exit.Click += (_, _) => ExitThread();

        menu.Items.Add(open);
        menu.Items.Add(_enableItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(presets);
        menu.Items.Add(settings);
        menu.Items.Add(_startupItem);
        menu.Items.Add(_wheelItem);
        menu.Items.Add(_hotkeyItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(refresh);
        menu.Items.Add(openConfig);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exit);

        return menu;
    }

    private void SyncMenuState()
    {
        _suppressMenuEvents = true;
        try
        {
            _enableItem.Checked = _settings.Enabled;
            _startupItem.Checked = _settings.StartWithWindows;
            _wheelItem.Checked = _settings.WheelOnTray;
            _hotkeyItem.Checked = _settings.HotkeysEnabled;
        }
        finally
        {
            _suppressMenuEvents = false;
        }
    }

    /// <summary>
    /// 夜间模式总开关。关闭只是「临时停用」：屏幕立刻回到中性白，
    /// 记住的强度、开机自启、托盘图标全部保留，重新勾上即原样恢复。
    /// </summary>
    private void SetEnabled(bool enabled)
    {
        _settings.Enabled = enabled;
        _enableItem.Checked = enabled;
        _flyout?.SetPaused(!enabled);
        _settingsForm?.SetStates(enabled, _settings.StartWithWindows, _settings.WheelOnTray,
            _settings.HotkeysEnabled, _settings.RestoreOnStartup);

        ApplyCurrentState();
        UpdateTrayIcon();
        ScheduleSave();
    }

    private void SetStartWithWindows(bool enabled)
    {
        _settings.StartWithWindows = enabled;
        StartupRegistrar.Apply(enabled);
        _startupItem.Checked = enabled;
        _settingsForm?.SetStates(_settings.Enabled, _settings.StartWithWindows, _settings.WheelOnTray,
            _settings.HotkeysEnabled, _settings.RestoreOnStartup);
        ScheduleSave();
    }

    private void SetWheelEnabled(bool enabled)
    {
        _settings.WheelOnTray = enabled;
        _wheelHook.Enabled = enabled;
        _wheelItem.Checked = enabled;
        _settingsForm?.SetStates(_settings.Enabled, _settings.StartWithWindows, _settings.WheelOnTray,
            _settings.HotkeysEnabled, _settings.RestoreOnStartup);
        ScheduleSave();
    }

    // =====================================================================
    // 托盘 / 面板
    // =====================================================================

    private void OnTrayMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ToggleFlyout();
    }

    private void ToggleFlyout()
    {
        if (_flyout is { Visible: true })
        {
            _flyout.Hide();
            return;
        }
        ShowPanel(takeFocus: true);
    }

    private void ShowPanel(bool takeFocus)
    {
        EnsureFlyout();
        var targets = _gamma.Targets;
        var flyout = _flyout!;

        if (!flyout.MatchesTargets(targets))
            flyout.Bind(targets, _settings.Step);

        flyout.SyncValues();
        flyout.ShowNearTray(takeFocus);
    }

    private void EnsureFlyout()
    {
        if (_flyout is not null) return;

        _flyout = new FlyoutForm();
        _flyout.ValueChanged += OnRowValueChanged;
        _flyout.RefreshRequested += RefreshDisplays;
        _flyout.SettingsRequested += ShowSettings;
        _flyout.SetPaused(!_settings.Enabled);
    }

    private void ShowSettings()
    {
        if (_settingsForm is null)
        {
            _settingsForm = new SettingsForm();
            _settingsForm.EnabledToggled += SetEnabled;
            _settingsForm.StartupToggled += SetStartWithWindows;
            _settingsForm.WheelToggled += SetWheelEnabled;
            _settingsForm.HotkeysToggled += enabled =>
            {
                _settings.HotkeysEnabled = enabled;
                RegisterHotKeys();
                ScheduleSave();
            };
            _settingsForm.RestoreToggled += enabled =>
            {
                _settings.RestoreOnStartup = enabled;
                ScheduleSave();
                if (enabled) RestoreAll();
            };
        }

        _settingsForm.SetStates(_settings.Enabled, _settings.StartWithWindows, _settings.WheelOnTray,
            _settings.HotkeysEnabled, _settings.RestoreOnStartup);
        _settingsForm.ShowCentered();
    }

    private void OnRowValueChanged(TintRow row, int strength, bool committed)
    {
        // 暂停态滑块是只读的，任何意外的值变化都不应该偷偷把夜间模式打开
        if (_applying || !_settings.Enabled) return;
        ApplyStrength(row.Target, strength);
        if (committed) ScheduleSave();
    }

    /// <summary>统一入口：更新状态 + 排队写入 gamma + 记忆。</summary>
    private void ApplyStrength(DisplayTarget target, int strength)
    {
        strength = ColorTemperature.ClampStrength(strength);
        target.Strength = strength;
        _applier.Set(target.Key, target.Kelvin);
        _settings.RememberStrength(target.Key, strength);
        ScheduleSave();
        UpdateTrayIcon();

        if (_flyout is { Visible: true })
        {
            _applying = true;
            try { _flyout.SyncValues(); }
            finally { _applying = false; }
        }
    }

    private void ApplyToAll(int strength)
    {
        // 菜单里主动选强度＝明确要用，顺手解除暂停
        if (!_settings.Enabled) SetEnabled(true);

        foreach (var target in _gamma.Targets)
            ApplyStrength(target, strength);
    }

    private void NudgeAll(int deltaPercent, bool showPanel)
    {
        var targets = _gamma.Targets;
        if (targets.Count == 0) return;

        foreach (var target in targets)
            ApplyStrength(target, target.Strength + deltaPercent);

        if (showPanel)
        {
            EnsureFlyout();
            if (!_flyout!.MatchesTargets(_gamma.Targets))
                _flyout.Bind(_gamma.Targets, _settings.Step);
            _flyout.SyncValues();
            _flyout.ShowNearTray(takeFocus: false);
        }
    }

    // =====================================================================
    // 快捷键 / 滚轮
    // =====================================================================

    private void RegisterHotKeys()
    {
        _hotKeys.UnregisterAll();
        if (!_settings.HotkeysEnabled) return;

        // Ctrl+Alt+Shift 组合，避开 Intel 核显占用的 Ctrl+Alt+↑/↓
        int modifiers = Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_SHIFT | Native.MOD_NOREPEAT;
        bool up = _hotKeys.Register(HotKeyWindow.IdIncrease, modifiers, Native.VK_UP);
        bool down = _hotKeys.Register(HotKeyWindow.IdDecrease, modifiers, Native.VK_DOWN);
        _hotKeys.Register(HotKeyWindow.IdTogglePanel, Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_NOREPEAT, 0x42);

        if ((!up || !down) && !_hotkeyWarned)
        {
            _hotkeyWarned = true;
            try
            {
                _tray.ShowBalloonTip(4000, $"{AppInfo.DisplayName} 快捷键被占用",
                    "Ctrl+Alt+Shift+↑/↓ 注册失败，可能被其它软件占用；Ctrl+Alt+B 打开面板仍然可用。", ToolTipIcon.Warning);
            }
            catch { }
        }
    }

    private void OnHotKey(int id)
    {
        switch (id)
        {
            case HotKeyWindow.IdIncrease:
                NudgeAll(_settings.Step, showPanel: true);
                break;
            case HotKeyWindow.IdDecrease:
                NudgeAll(-_settings.Step, showPanel: true);
                break;
            case HotKeyWindow.IdTogglePanel:
                ToggleFlyout();
                break;
        }
    }

    private void OnWheelTurned(int direction) =>
        NudgeAll(direction * _settings.Step, showPanel: true);

    // =====================================================================
    // 显示器 / 会话生命周期
    // =====================================================================

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        RunOnUi(() =>
        {
            _displayTimer.Stop();
            _displayTimer.Start();
        });

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect
            or SessionSwitchReason.RemoteConnect or SessionSwitchReason.SessionLogon)
        {
            RunOnUi(RestoreAll);
        }
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) RunOnUi(RestoreAll);
    }

    private void RefreshDisplays()
    {
        Task.Run(() =>
        {
            try { _gamma.Refresh(); } catch { }

            RunOnUi(() =>
            {
                var targets = _gamma.Targets;
                if (_flyout is not null)
                {
                    if (!_flyout.MatchesTargets(targets))
                        _flyout.Bind(targets, _settings.Step);
                    else
                        _flyout.SyncValues();
                }

                RestoreAll();
                UpdateTrayIcon();
            });
        });
    }

    /// <summary>把记住的强度重新套到所有显示器（开机 / 解锁 / 插拔后调用）。</summary>
    private void RestoreAll()
    {
        foreach (var target in _gamma.Targets)
        {
            int saved = _settings.RestoreOnStartup ? _settings.GetSavedStrength(target.Key) : -1;
            target.Strength = saved >= 0 ? saved : 0;
        }

        ApplyCurrentState();
        UpdateTrayIcon();
    }

    /// <summary>
    /// 按显示器当前的 Strength 重新下发实际色温；暂停时一律下发中性白
    /// （不动 Strength 本身，所以重新启用后位置原样还原）。
    /// </summary>
    private void ApplyCurrentState()
    {
        int neutral = ColorTemperature.NeutralKelvin;
        foreach (var target in _gamma.Targets)
            _applier.Set(target.Key, _settings.Enabled ? target.Kelvin : neutral);

        if (_flyout is { Visible: true }) _flyout.SyncValues();
    }

    // =====================================================================
    // 图标 / 其它
    // =====================================================================

    private void UpdateTrayIcon()
    {
        var targets = _gamma.Targets;
        int strength = targets.Count > 0 ? (int)Math.Round(targets.Average(t => t.Strength)) : 0;
        int kelvin = ColorTemperature.StrengthToKelvin(strength);
        bool paused = !_settings.Enabled;

        var icon = TrayIconFactory.Create(kelvin, paused);
        var previous = _trayIcon;
        _trayIcon = icon;
        _tray.Icon = icon;
        _tray.Text = paused
            ? $"{AppInfo.DisplayName} · 已暂停"
            : strength <= 0
                ? $"{AppInfo.DisplayName} · 夜间模式关闭"
                : $"{AppInfo.DisplayName} · {strength}% ({kelvin}K)";
        previous?.Dispose();
    }

    private void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void RunOnUi(Action action)
    {
        _ui.Post(_ =>
        {
            try { action(); } catch { }
        }, null);
    }

    private void OpenConfigFolder()
    {
        try
        {
            Directory.CreateDirectory(AppSettings.SettingsDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{AppSettings.FilePath}\"",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void ShowFirstRunTip()
    {
        if (_settings.FirstRunTipShown) return;
        try
        {
            _tray.ShowBalloonTip(5000, $"{AppInfo.DisplayName} 已就绪",
                "左键点托盘图标打开面板；拖滑块调强度，0 = 关闭，100 = 最暖。右键菜单第一项可临时停用。", ToolTipIcon.None);
            _settings.FirstRunTipShown = true;
            ScheduleSave();
        }
        catch { }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;

            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;

            _saveTimer.Stop();
            _saveTimer.Dispose();
            _displayTimer.Stop();
            _displayTimer.Dispose();

            _hotKeys.Dispose();
            _wheelHook.Dispose();

            _settings.Save();

            // 退出前把屏幕恢复成中性白
            foreach (var target in _gamma.Targets)
                _gamma.Apply(target, ColorTemperature.NeutralKelvin);

            _applier.Dispose();

            _tray.Visible = false;
            _tray.Dispose();
            _trayIcon?.Dispose();

            _flyout?.Dispose();
            _settingsForm?.Dispose();
            _gamma.Dispose();
        }

        base.Dispose(disposing);
    }
}
