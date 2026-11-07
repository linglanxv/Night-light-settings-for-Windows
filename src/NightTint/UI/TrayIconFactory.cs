using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using NightTint.Core;

namespace NightTint.UI;

/// <summary>
/// 动态托盘图标：一个填满当前色温颜色的圆。
/// 6500K 是中性白，越低越偏琥珀，一眼就能看出当前状态。
/// </summary>
internal static class TrayIconFactory
{
    public static Icon Create(int kelvin)
    {
        const int size = 32;
        Color tint = ColorTemperature.ToColor(kelvin);
        bool neutral = kelvin >= ColorTemperature.NeutralKelvin;

        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            float cx = size / 2f;
            float cy = size / 2f;
            float radius = 10.5f;

            // 外圈：中性时用灰色描边，暖色时用更饱和的同色描边
            using (var ring = new Pen(neutral ? Color.FromArgb(210, 214, 220) : Darken(tint, 0.25f), 1.6f))
                g.DrawEllipse(ring, cx - radius - 1.2f, cy - radius - 1.2f, (radius + 1.2f) * 2, (radius + 1.2f) * 2);

            using (var brush = new SolidBrush(tint))
                g.FillEllipse(brush, cx - radius, cy - radius, radius * 2, radius * 2);

            // 暖色时叠一条小月牙，强化"夜间"语义
            if (!neutral)
            {
                using var moon = new SolidBrush(Color.FromArgb(120, 32, 28, 22));
                g.FillEllipse(moon, cx - radius * 0.2f, cy - radius * 0.95f, radius * 1.35f, radius * 1.6f);
            }
        }

        IntPtr handle = bitmap.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(handle);
            return (Icon)temp.Clone();
        }
        finally
        {
            Native.DestroyIcon(handle);
        }
    }

    private static Color Darken(Color color, float amount)
    {
        float k = 1f - amount;
        return Color.FromArgb(color.A, (int)(color.R * k), (int)(color.G * k), (int)(color.B * k));
    }
}
