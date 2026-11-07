using System.Drawing.Drawing2D;
using NightTint.Core;

namespace NightTint.UI;

/// <summary>
/// 一行夜间模式强度控制：显示器名称 + 强度读数 + 中性→暖色渐变滑轨。
/// 0 = 关闭，100 = 最暖（与 Windows 夜间模式一致）。
/// </summary>
internal sealed class TintRow : Control
{
    private readonly DisplayTarget _target;
    private readonly Font _nameFont;
    private readonly Font _valueFont;
    private int _strength;
    private bool _dragging;
    private bool _hovering;
    private bool _paused;

    /// <summary>(row, strength, committed)。committed=true 表示本次拖动 / 滚轮结束。</summary>
    public event Action<TintRow, int, bool>? ValueChanged;

    public TintRow(DisplayTarget target, int step)
    {
        _target = target;
        Step = step;
        _strength = ColorTemperature.ClampStrength(target.Strength);

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);

        BackColor = Theme.Background;
        _nameFont = Theme.UiFont(13f);
        _valueFont = Theme.UiFont(12.5f);
        TabStop = true;
        Cursor = Cursors.Hand;
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    public string MonitorKey => _target.Key;

    public DisplayTarget Target => _target;

    public int Step { get; set; } = 5;

    /// <summary>夜间模式被临时停用：整行压暗显示，但拖动仍会重新启用。</summary>
    public bool Paused
    {
        get => _paused;
        set
        {
            if (_paused == value) return;
            _paused = value;
            Invalidate();
        }
    }

    public int Strength
    {
        get => _strength;
        set => SetStrength(value, commit: false);
    }

    private float DpiScale => DeviceDpi / 96f;

    private int Px(float value) => (int)Math.Round(value * DpiScale);

    private int TrackTop => Px(42);

    private int TrackHeight => Px(6);

    private int ThumbRadius => Px(8);

    public int PreferredHeight => Px(64);

    public void SetStrength(int strength, bool commit)
    {
        strength = ColorTemperature.ClampStrength(strength);
        if (strength == _strength) return;
        _strength = strength;
        _target.Strength = strength;
        Invalidate();
        ValueChanged?.Invoke(this, strength, commit);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Theme.Background);

        int padX = Px(2);
        bool on = _strength > 0;

        var nameRect = new Rectangle(padX, Px(2), Math.Max(10, Width - padX * 2 - Px(118)), Px(20));
        TextRenderer.DrawText(g, string.IsNullOrWhiteSpace(_target.Name) ? "显示器" : _target.Name,
            _nameFont, nameRect, _target.Applicable ? Theme.Text : Theme.TextFaint,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        string valueText = $"{_strength}% · {ColorTemperature.StrengthToKelvin(_strength)}K";
        var valueRect = new Rectangle(Width - padX - Px(116), Px(2), Px(116), Px(20));
        TextRenderer.DrawText(g, valueText, _valueFont, valueRect,
            on ? Theme.Text : Theme.TextDim,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        int trackX = padX;
        int trackWidth = Width - padX * 2;
        int trackY = TrackTop;
        int trackH = TrackHeight;
        float radius = trackH / 2f;
        var trackRect = new RectangleF(trackX, trackY, trackWidth, trackH);

        // 整条轨：中性白 -> 暖琥珀
        using (var path = Theme.RoundedRect(trackRect, radius))
        using (var gradient = new LinearGradientBrush(
                   new RectangleF(trackX, trackY, Math.Max(trackWidth, 2), trackH),
                   ColorTemperature.ToColor(ColorTemperature.MaxKelvin),
                   ColorTemperature.ToColor(ColorTemperature.MinKelvin),
                   LinearGradientMode.Horizontal))
        {
            g.FillPath(gradient, path);
        }

        int r = ThumbRadius;
        float usable = Math.Max(1f, trackWidth - r * 2f);
        float ratio = _strength / (float)ColorTemperature.MaxStrength;
        float thumbX = trackX + r + usable * Math.Clamp(ratio, 0f, 1f);
        float centerY = trackY + trackH / 2f;

        // 未选中的右侧压暗，突出当前强度
        using (var path = Theme.RoundedRect(trackRect, radius))
        {
            var state = g.Save();
            g.SetClip(path);
            using (var dim = new SolidBrush(Color.FromArgb(175, 16, 16, 18)))
                g.FillRectangle(dim, thumbX, trackY - 1, trackX + trackWidth - thumbX + 1, trackH + 2);
            g.Restore(state);
        }

        // 滑块
        float thumbR = r * (_dragging ? 1.12f : _hovering ? 1.05f : 1f);
        if (_dragging || _hovering)
        {
            using var glow = new SolidBrush(Color.FromArgb(_dragging ? 70 : 40, ColorTemperature.ToColor(_target.Kelvin)));
            g.FillEllipse(glow, thumbX - thumbR - Px(4), centerY - thumbR - Px(4),
                (thumbR + Px(4)) * 2, (thumbR + Px(4)) * 2);
        }

        using (var shadow = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
            g.FillEllipse(shadow, thumbX - thumbR, centerY - thumbR + Px(1), thumbR * 2, thumbR * 2);
        using (var thumbBrush = new SolidBrush(Theme.Thumb))
            g.FillEllipse(thumbBrush, thumbX - thumbR, centerY - thumbR, thumbR * 2, thumbR * 2);

        if (Focused)
        {
            using var focusPen = new Pen(Color.FromArgb(130, Theme.Accent), Math.Max(1f, Px(1)));
            g.DrawEllipse(focusPen, thumbX - thumbR - Px(3), centerY - thumbR - Px(3),
                (thumbR + Px(3)) * 2, (thumbR + Px(3)) * 2);
        }

        // 暂停态：整体压暗，一眼看出当前没有生效（数值仍然可见＝记住的位置还在）
        if (_paused)
        {
            using var veil = new SolidBrush(Color.FromArgb(165, Theme.Background));
            g.FillRectangle(veil, ClientRectangle);
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovering = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovering = false;
        Invalidate();
    }

    /// <summary>仅在支持 DDC-free gamma 且未被暂停时可交互。</summary>
    private bool Interactive => _target.Applicable && !_paused;

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!Interactive) return;

        Focus();
        if (e.Button == MouseButtons.Left)
        {
            _dragging = true;
            Capture = true;
            ApplyMouseX(e.X, commit: false);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging) ApplyMouseX(e.X, commit: false);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_dragging) return;
        _dragging = false;
        Capture = false;
        ApplyMouseX(e.X, commit: true);
    }

    protected override void OnDoubleClick(EventArgs e)
    {
        base.OnDoubleClick(e);
        if (!Interactive) return;
        // 双击关闭夜间模式
        SetStrength(0, commit: true);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (!Interactive) return;
        SetStrength(_strength + (e.Delta > 0 ? Step : -Step), commit: true);
    }

    protected override bool IsInputKey(Keys keyData) => true;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!Interactive) return;

        int? target = e.KeyCode switch
        {
            Keys.Left or Keys.Down => _strength - Step,
            Keys.Right or Keys.Up => _strength + Step,
            Keys.PageDown => _strength - 10,
            Keys.PageUp => _strength + 10,
            Keys.Home => 0,
            Keys.End => 100,
            _ => null
        };

        if (target is null) return;
        SetStrength(target.Value, commit: true);
        e.Handled = true;
    }

    private void ApplyMouseX(int x, bool commit)
    {
        int padX = Px(2);
        int trackWidth = Width - padX * 2;
        int r = ThumbRadius;
        float usable = Math.Max(1f, trackWidth - r * 2f);
        float ratio = Math.Clamp((x - padX - r) / usable, 0f, 1f);
        SetStrength((int)Math.Round(ratio * 100f), commit);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _nameFont.Dispose();
            _valueFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
