namespace NightLightSettings.Core;

/// <summary>一台显示器（一个 GDI 设备）及其当前夜间模式强度。</summary>
internal sealed class DisplayTarget
{
    /// <summary>持久化 Key（按显示器名称生成，重名追加序号）。</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>面板上显示的名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>GDI 设备名，例如 \\.\DISPLAY1，用于创建 DC 写 gamma ramp。</summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>桌面坐标。</summary>
    public Rectangle Bounds { get; set; }

    public bool IsPrimary { get; set; }

    /// <summary>缓存的设备 DC；IntPtr.Zero 表示不可用。</summary>
    public IntPtr Hdc { get; set; }

    /// <summary>夜间模式强度 0~100（0 = 关闭，100 = 最暖）。</summary>
    public int Strength { get; set; }

    /// <summary>由强度换算出的色温（开尔文）。</summary>
    public int Kelvin => ColorTemperature.StrengthToKelvin(Strength);

    /// <summary>是否成功应用过 gamma（不可用时 UI 显示提示）。</summary>
    public bool Applicable { get; set; } = true;

    public override string ToString() => $"{Name} ({DeviceName}) {Strength}% {Kelvin}K";
}
