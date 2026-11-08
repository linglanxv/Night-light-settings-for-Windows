namespace NightLightSettings.UI;

/// <summary>面板底部的开关行（目前用于「开机自动启动」「托盘滚轮调节」）。</summary>
internal sealed class ToggleRow : Control
{
    private readonly Font _labelFont;
    private bool _checked;
    private bool _hovering;

    public event Action<ToggleRow, bool>? Toggled;

    public ToggleRow(string label, bool initial)
    {
        Label = label;
        _checked = initial;

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

        BackColor = Theme.Background;
        _labelFont = Theme.UiFont(12.5f);
        Cursor = Cursors.Hand;
    }

    public string Label { get; }

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            Invalidate();
        }
    }

    private float DpiScale => DeviceDpi / 96f;

    private int Px(float value) => (int)Math.Round(value * DpiScale);

    public int PreferredHeight => Px(36);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Theme.Background);

        TextRenderer.DrawText(g, Label, _labelFont,
            new Rectangle(Px(2), 0, Width - Px(60), Height),
            _hovering ? Theme.Text : Theme.TextDim,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        // 胶囊开关
        int switchW = Px(36);
        int switchH = Px(20);
        int x = Width - switchW - Px(2);
        int y = (Height - switchH) / 2;
        var track = new RectangleF(x, y, switchW, switchH);
        float radius = switchH / 2f;

        using (var path = Theme.RoundedRect(track, radius))
        using (var brush = new SolidBrush(_checked ? Theme.Accent : Color.FromArgb(70, 70, 78)))
            g.FillPath(brush, path);

        int knob = switchH - Px(6);
        float knobX = _checked ? x + switchW - knob - Px(3) : x + Px(3);
        float knobY = y + Px(3);

        if (_hovering)
        {
            using var glow = new SolidBrush(Color.FromArgb(40, 255, 255, 255));
            g.FillEllipse(glow, knobX - Px(2), knobY - Px(2), knob + Px(4), knob + Px(4));
        }

        using (var knobBrush = new SolidBrush(_checked ? Color.White : Color.FromArgb(190, 190, 198)))
            g.FillEllipse(knobBrush, knobX, knobY, knob, knob);
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

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        _checked = !_checked;
        Invalidate();
        Toggled?.Invoke(this, _checked);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _labelFont.Dispose();
        base.Dispose(disposing);
    }
}
