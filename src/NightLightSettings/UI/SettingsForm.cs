using System.Drawing.Drawing2D;
using NightLightSettings.Core;

namespace NightLightSettings.UI;

/// <summary>
/// 独立设置面板。托盘滑块面板只放滑块，开关类选项都在这里。
/// </summary>
internal sealed class SettingsForm : Form
{
    private readonly ToggleRow _enabled;
    private readonly ToggleRow _startup;
    private readonly ToggleRow _wheel;
    private readonly ToggleRow _hotkeys;
    private readonly ToggleRow _restore;
    private readonly Font _titleFont;
    private readonly Font _hintFont;
    private readonly Font _glyphFont;
    private Rectangle _closeHitRect = Rectangle.Empty;
    private bool _hoverClose;
    private bool _dragging;
    private Point _dragOffset;

    public event Action<bool>? EnabledToggled;
    public event Action<bool>? StartupToggled;
    public event Action<bool>? WheelToggled;
    public event Action<bool>? HotkeysToggled;
    public event Action<bool>? RestoreToggled;

    public SettingsForm()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Theme.Background;
        Text = $"{AppInfo.DisplayName} · 设置";

        _titleFont = Theme.UiFont(15f, FontStyle.Bold);
        _hintFont = Theme.UiFont(11f);
        _glyphFont = new Font("Segoe MDL2 Assets", 13f, FontStyle.Regular, GraphicsUnit.Pixel);

        _enabled = new ToggleRow("夜间模式已启用", true);
        _enabled.Toggled += (_, v) => EnabledToggled?.Invoke(v);

        _startup = new ToggleRow("开机自动启动", true);
        _startup.Toggled += (_, v) => StartupToggled?.Invoke(v);

        _wheel = new ToggleRow("托盘滚轮调节", true);
        _wheel.Toggled += (_, v) => WheelToggled?.Invoke(v);

        _hotkeys = new ToggleRow("全局快捷键 Ctrl+Alt+Shift+↑/↓", true);
        _hotkeys.Toggled += (_, v) => HotkeysToggled?.Invoke(v);

        _restore = new ToggleRow("启动时恢复上次强度", true);
        _restore.Toggled += (_, v) => RestoreToggled?.Invoke(v);

        Controls.AddRange(new Control[] { _enabled, _startup, _wheel, _hotkeys, _restore });
        Relayout();
    }

    private float DpiScale => DeviceDpi / 96f;

    private int Px(float value) => (int)Math.Round(value * DpiScale);

    public void SetStates(bool enabled, bool startup, bool wheel, bool hotkeys, bool restore)
    {
        _enabled.Checked = enabled;
        _startup.Checked = startup;
        _wheel.Checked = wheel;
        _hotkeys.Checked = hotkeys;
        _restore.Checked = restore;
    }

    private void Relayout()
    {
        int width = Px(372);
        int pad = Px(18);
        int header = Px(46);
        int footer = Px(34);
        int rowHeight = Px(38);

        var rows = new[] { _enabled, _startup, _wheel, _hotkeys, _restore };
        ClientSize = new Size(width, header + rows.Length * rowHeight + footer);

        int y = header;
        foreach (var row in rows)
        {
            row.Location = new Point(pad, y);
            row.Size = new Size(width - pad * 2, rowHeight);
            y += rowHeight;
        }

        UpdateWindowShape();
        Invalidate();
    }

    private void UpdateWindowShape()
    {
        if (Environment.OSVersion.Version.Build >= 22000)
        {
            int preference = Native.DWMWCP_ROUND;
            Native.DwmSetWindowAttribute(Handle, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
            int dark = 1;
            Native.DwmSetWindowAttribute(Handle, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
            return;
        }

        using var path = Theme.RoundedRect(new RectangleF(0, 0, Width, Height), Px(12));
        var old = Region;
        Region = new Region(path);
        old?.Dispose();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int pad = Px(18);
        using (var bg = new SolidBrush(Theme.Background))
            g.FillRectangle(bg, ClientRectangle);

        using (var borderPath = Theme.RoundedRect(new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f), Px(12)))
        using (var pen = new Pen(Theme.Border, 1f))
            g.DrawPath(pen, borderPath);

        TextRenderer.DrawText(g, "设置", _titleFont,
            new Rectangle(pad, Px(12), Px(200), Px(24)),
            Theme.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        TextRenderer.DrawText(g, AppInfo.ChineseName, _hintFont,
            new Rectangle(pad + Px(52), Px(15), Px(230), Px(20)),
            Theme.TextFaint, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

        int glyphSize = Px(26);
        _closeHitRect = new Rectangle(Width - pad - glyphSize, Px(11), glyphSize, glyphSize);
        TextRenderer.DrawText(g, "\uE711", _glyphFont, _closeHitRect,
            _hoverClose ? Theme.Text : Theme.TextDim,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        using (var pen = new Pen(Color.FromArgb(40, 40, 46), 1f))
            g.DrawLine(pen, pad, Px(44), Width - pad, Px(44));

        string footer = $"配置：{AppSettingsBridge.SettingsPath}";
        TextRenderer.DrawText(g, footer, _hintFont,
            new Rectangle(pad, Height - Px(30), Width - pad * 2, Px(22)),
            Theme.TextFaint, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.PathEllipsis | TextFormatFlags.NoPrefix);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (_closeHitRect.Contains(e.Location))
        {
            Hide();
            return;
        }

        if (e.Y < Px(44))
        {
            _dragging = true;
            _dragOffset = e.Location;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_dragging)
        {
            var screen = PointToScreen(e.Location);
            Location = new Point(screen.X - _dragOffset.X, screen.Y - _dragOffset.Y);
            return;
        }

        bool hover = _closeHitRect.Contains(e.Location);
        if (hover != _hoverClose)
        {
            _hoverClose = hover;
            Invalidate();
        }
        Cursor = hover ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape)
        {
            Hide();
            e.Handled = true;
        }
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        Relayout();
    }

    public void ShowCentered()
    {
        var work = Native.GetWorkAreaForPoint(Cursor.Position);
        Location = new Point(
            work.Left + (work.Width - Width) / 2,
            work.Top + (work.Height - Height) / 2);

        Show();
        BringToFront();
        Activate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleFont.Dispose();
            _hintFont.Dispose();
            _glyphFont.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>避免 UI 层直接依赖 Settings 命名空间里的路径字段。</summary>
internal static class AppSettingsBridge
{
    public static string SettingsPath => Settings.AppSettings.FilePath;
}
