namespace NightLightSettings.Core;

/// <summary>
/// 色温计算。
/// 对外用 0~100 的「强度」（与 Windows 夜间模式一致：0 = 关闭，100 = 最暖），
/// 内部换算成开尔文再生成 gamma ramp（6500K 为屏幕原生白点，1900K 为极暖）。
/// </summary>
internal static class ColorTemperature
{
    public const int MinKelvin = 1900;
    public const int MaxKelvin = 6500;
    public const int NeutralKelvin = 6500;

    public const int MinStrength = 0;
    public const int MaxStrength = 100;

    /// <summary>强度 0~100 → 开尔文（0 = 6500K 中性，100 = 1900K 最暖）。</summary>
    public static int StrengthToKelvin(int strength)
    {
        strength = ClampStrength(strength);
        return MaxKelvin - (int)Math.Round(strength * (MaxKelvin - MinKelvin) / 100.0);
    }

    /// <summary>开尔文 → 强度 0~100。</summary>
    public static int KelvinToStrength(int kelvin) =>
        (int)Math.Round((MaxKelvin - Math.Clamp(kelvin, MinKelvin, MaxKelvin)) * 100.0 / (MaxKelvin - MinKelvin));

    public static int ClampStrength(int strength) => Math.Clamp(strength, MinStrength, MaxStrength);

    /// <summary>黑体辐射近似（Tanner Helland 算法），返回 0..1 的 RGB 系数。</summary>
    public static (double R, double G, double B) Factors(int kelvin)
    {
        if (kelvin >= NeutralKelvin) return (1.0, 1.0, 1.0);

        double t = Math.Clamp(kelvin, 1000, 40000) / 100.0;

        double r = t <= 66 ? 255.0 : 329.698727446 * Math.Pow(t - 60, -0.1332047592);
        double g = t <= 66
            ? 99.4708025861 * Math.Log(t) - 161.1195681661
            : 288.1221695283 * Math.Pow(t - 60, -0.0755148492);
        double b = t >= 66
            ? 255.0
            : t <= 19
                ? 0.0
                : 138.5177312231 * Math.Log(t - 10) - 305.0447927307;

        r = Math.Clamp(r / 255.0, 0.0, 1.0);
        g = Math.Clamp(g / 255.0, 0.0, 1.0);
        b = Math.Clamp(b / 255.0, 0.0, 1.0);

        // 归一化到最亮通道，避免整体明显变暗
        double max = Math.Max(r, Math.Max(g, b));
        if (max > 0.0001)
        {
            r /= max;
            g /= max;
            b /= max;
        }

        // 极暖时保留一点点蓝色，避免部分驱动拒绝纯琥珀色 ramp
        b = Math.Max(b, 0.02);
        return (r, g, b);
    }

    /// <summary>生成 gamma ramp（长度 768）。</summary>
    public static ushort[] BuildRamp(int kelvin)
    {
        var (r, g, b) = Factors(kelvin);
        var ramp = new ushort[768];

        for (int i = 0; i < 256; i++)
        {
            double v = i / 255.0;
            ramp[i] = ToWord(v * r);
            ramp[256 + i] = ToWord(v * g);
            ramp[512 + i] = ToWord(v * b);
        }

        return ramp;
    }

    private static ushort ToWord(double value) =>
        (ushort)Math.Clamp(Math.Round(value * 65535.0), 0, 65535);

    /// <summary>给 UI 用的显示色（0 强度接近白色，100 强度为琥珀色）。</summary>
    public static Color ToColor(int kelvin)
    {
        var (r, g, b) = Factors(kelvin);
        return Color.FromArgb(255, (int)(r * 255), (int)(g * 255), (int)(b * 255));
    }

    public static string Describe(int strength) => strength switch
    {
        <= 0 => "关闭",
        <= 20 => "轻暖",
        <= 40 => "微暖",
        <= 60 => "柔和",
        <= 80 => "暖",
        _ => "很暖"
    };
}
