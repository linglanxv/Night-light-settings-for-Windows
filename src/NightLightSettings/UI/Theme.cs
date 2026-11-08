using System.Drawing.Drawing2D;

namespace NightLightSettings.UI;

/// <summary>暗色主题配色与绘制小工具。</summary>
internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(27, 27, 29);
    public static readonly Color BackgroundElevated = Color.FromArgb(34, 34, 37);
    public static readonly Color Border = Color.FromArgb(52, 52, 58);
    public static readonly Color Text = Color.FromArgb(242, 242, 245);
    public static readonly Color TextDim = Color.FromArgb(142, 142, 150);
    public static readonly Color TextFaint = Color.FromArgb(96, 96, 104);
    public static readonly Color Track = Color.FromArgb(58, 58, 65);
    public static readonly Color TrackHover = Color.FromArgb(68, 68, 76);
    public static readonly Color Accent = Color.FromArgb(96, 168, 255);
    public static readonly Color AccentDark = Color.FromArgb(52, 122, 214);
    public static readonly Color Thumb = Color.FromArgb(250, 250, 252);

    public const string FontFamily = "Segoe UI";

    public static Font UiFont(float pixels, FontStyle style = FontStyle.Regular) =>
        new(FontFamily, pixels, style, GraphicsUnit.Pixel);

    public static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0.01f)
        {
            path.AddRectangle(rect);
            return path;
        }

        float d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static Color Mix(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return Color.FromArgb(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }

    /// <summary>给按钮 / 菜单做圆角悬浮背景。</summary>
    public static void FillRounded(Graphics g, RectangleF rect, float radius, Color color)
    {
        using var path = RoundedRect(rect, radius);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    public static void DrawRoundedBorder(Graphics g, RectangleF rect, float radius, Color color, float width = 1f)
    {
        using var path = RoundedRect(rect, radius);
        using var pen = new Pen(color, width);
        g.DrawPath(pen, path);
    }
}
