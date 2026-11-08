using System.Drawing.Drawing2D;
using NightLightSettings.Core;

namespace NightLightSettings.UI;

/// <summary>
/// 托盘弹出的夜间模式面板（Twinkle Tray 风格 flyout）：
/// 每台显示器一行 0~100 强度滑块，除此之外不放其它控件（设置项在独立设置面板里）。
/// </summary>
internal sealed class FlyoutForm : Form
{
    private const int BaseWidth = 350;
    private const int PadBase = 16;
    private const int HeaderBase = 44;
    private const int FooterBase = 30;
    private readonly List<TintRow> _rows = new();
    private readonly Font _titleFont;
    private readonly Font _hintFont;
    private readonly Font _glyphFont;
    private readonly System.Windows.Forms.Timer _fadeTimer;
    private readonly System.Windows.Forms.Timer _autoHideTimer;
    private int _fadeElapsed;
    private Rectangle _refreshHitRect = Rectangle.Empty;
    private Rectangle _settingsHitRect = Rectangle.Empty;
    private bool _hoverRefresh;
    private bool _hoverSettings;
    private bool _paused;

    public event Action<TintRow, int, bool>? ValueChanged;
    public event Action? RefreshRequested;
    public event Action? SettingsRequested;

    public FlyoutForm()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Theme.Background;
        Text = AppInfo.DisplayName;

        _titleFont = Theme.UiFont(15f, FontStyle.Bold);
        _hintFont = Theme.UiFont(11.5f);
        // Segoe MDL2 Assets：Windows 自带的图标字体（齿轮 / 刷新）
        _glyphFont = new Font("Segoe MDL2 Assets", 14f, FontStyle.Regular, GraphicsUnit.Pixel);

        _fadeTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _fadeTimer.Tick += (_, _) =>
        {
            _fadeElapsed += _fadeTimer.Interval;
            Opacity = Math.Min(1.0, _fadeElapsed / 130.0);
            if (Opacity >= 1.0)
            {
                _fadeTimer.Stop();
                Opacity = 1.0;
            }
        };

        // 非激活弹出（快捷键 / 滚轮）自动收起
        _autoHideTimer = new System.Windows.Forms.Timer { Interval = 2600 };
        _autoHideTimer.Tick += (_, _) =>
        {
            _autoHideTimer.Stop();
            Hide();
        };
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
            cp.ExStyle |= Native.WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    private float DpiScale => DeviceDpi / 96f;

    private int Px(float value) => (int)Math.Round(value * DpiScale);

    public IReadOnlyList<TintRow> Rows => _rows;

    /// <summary>按当前显示器列表重建 UI。</summary>
    public void Bind(IReadOnlyList<DisplayTarget> targets, int step)
    {
        SuspendLayout();

        foreach (var row in _rows)
        {
            row.ValueChanged -= OnRowValueChanged;
            Controls.Remove(row);
            row.Dispose();
        }
        _rows.Clear();

        foreach (var target in targets)
        {
            var row = new TintRow(target, step) { Paused = _paused };
            row.ValueChanged += OnRowValueChanged;
            Controls.Add(row);
            _rows.Add(row);
        }

        Relayout();
        ResumeLayout(true);
    }

    public bool MatchesTargets(IReadOnlyList<DisplayTarget> targets)
    {
        if (_rows.Count != targets.Count) return false;
        for (int i = 0; i < targets.Count; i++)
        {
            if (!string.Equals(_rows[i].MonitorKey, targets[i].Key, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    public void SyncValues()
    {
        foreach (var row in _rows)
        {
            if (row.Strength != row.Target.Strength)
            {
                row.Strength = row.Target.Strength;
                row.Invalidate();
            }
        }
    }

    public void UpdateStep(int step)
    {
        foreach (var row in _rows) row.Step = step;
    }

    /// <summary>暂停态：滑块整体压暗并提示，提示用户右键菜单可恢复。</summary>
    public void SetPaused(bool paused)
    {
        if (_paused == paused) return;
        _paused = paused;
        foreach (var row in _rows) row.Paused = paused;
        Invalidate();
    }

    private void OnRowValueChanged(TintRow row, int strength, bool committed)
    {
        ValueChanged?.Invoke(row, strength, committed);
    }

    private void Relayout()
    {
        int width = Px(BaseWidth);
        int pad = Px(PadBase);
        int header = Px(HeaderBase);
        int footer = Px(FooterBase);
        int contentHeight = _rows.Count == 0 ? Px(52) : _rows.Sum(r => r.PreferredHeight);
        int height = header + contentHeight + footer + Px(6);

        ClientSize = new Size(width, height);

        int y = header;
        foreach (var row in _rows)
        {
            row.Width = width - pad * 2;
            row.Height = row.PreferredHeight;
            row.Location = new Point(pad, y);
            y += row.PreferredHeight;
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

        int pad = Px(PadBase);
        var bounds = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);

        using (var bg = new SolidBrush(Theme.Background))
            g.FillRectangle(bg, ClientRectangle);

        using (var borderPath = Theme.RoundedRect(bounds, Px(12)))
        using (var borderPen = new Pen(Theme.Border, 1f))
            g.DrawPath(borderPen, borderPath);

        TextRenderer.DrawText(g, AppInfo.DisplayName, _titleFont,
            new Rectangle(pad, Px(12), Px(230), Px(24)),
            _paused ? Theme.TextDim : Theme.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

        int glyphSize = Px(26);
        _refreshHitRect = new Rectangle(Width - pad - glyphSize, Px(9), glyphSize, glyphSize);
        _settingsHitRect = new Rectangle(_refreshHitRect.Left - glyphSize - Px(2), Px(9), glyphSize, glyphSize);

        TextRenderer.DrawText(g, "\uE72C", _glyphFont, _refreshHitRect,
            _hoverRefresh ? Theme.Text : Theme.TextDim,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g, "\uE713", _glyphFont, _settingsHitRect,
            _hoverSettings ? Theme.Text : Theme.TextDim,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        if (_rows.Count == 0)
        {
            TextRenderer.DrawText(g, "未检测到显示器", _hintFont,
                new Rectangle(pad, Px(HeaderBase), Width - pad * 2, Px(36)),
                Theme.TextFaint, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        string hint = _paused
            ? "已暂停 · 右键托盘菜单可重新启用"
            : "滚轮微调 · 双击关闭";
        TextRenderer.DrawText(g, hint, _hintFont,
            new Rectangle(pad, Height - Px(FooterBase), Width - pad * 2, Px(24)),
            _paused ? Theme.Accent : Theme.TextFaint,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _hoverRefresh = _refreshHitRect.Contains(e.Location);
        _hoverSettings = _settingsHitRect.Contains(e.Location);
        Cursor = _hoverRefresh || _hoverSettings ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        if (_refreshHitRect.Contains(e.Location)) RefreshRequested?.Invoke();
        else if (_settingsHitRect.Contains(e.Location)) SettingsRequested?.Invoke();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _autoHideTimer.Stop();
        base.OnMouseDown(e);
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

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        Hide();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible)
        {
            _fadeTimer.Stop();
            _autoHideTimer.Stop();
        }
    }

    /// <summary>吸附在鼠标所在屏幕右下角（任务栏上方）弹出。</summary>
    public void ShowNearTray(bool takeFocus)
    {
        var work = Native.GetWorkAreaForPoint(Cursor.Position);
        int margin = Px(12);

        int x = Math.Max(work.Left + margin, work.Right - Width - margin);
        int y = Math.Max(work.Top + margin, work.Bottom - Height - margin);
        Location = new Point(x, y);

        if (!Visible)
        {
            _fadeElapsed = 0;
            Opacity = 0.0;
            Show();
            _fadeTimer.Start();
        }

        BringToFront();
        _autoHideTimer.Stop();

        if (takeFocus) Activate();
        else _autoHideTimer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fadeTimer.Dispose();
            _autoHideTimer.Dispose();
            _titleFont.Dispose();
            _hintFont.Dispose();
            _glyphFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
