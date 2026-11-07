using System.Text.Json;
using System.Text.Json.Serialization;

namespace NightTint.Settings;

/// <summary>
/// 配置与「强度记忆」。存放在 %LOCALAPPDATA%\NightTint\settings.json。
/// 每台显示器按稳定 Key 记住最后一次的强度（0~100），开机 / 重新接上显示器时自动恢复。
/// </summary>
internal sealed class AppSettings
{
    public const int CurrentVersion = 3;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>
    /// 夜间模式总开关。关闭时屏幕立刻恢复中性白，但**不清除**记住的强度，
    /// 重新开启后原样恢复——这就是「临时关闭而不退出软件」。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>开机自动启动（面板和右键菜单里都有开关）。</summary>
    public bool StartWithWindows { get; set; } = true;

    /// <summary>鼠标停在托盘图标上滚动滚轮调节强度。</summary>
    public bool WheelOnTray { get; set; } = true;

    /// <summary>全局快捷键 Ctrl+Alt+Shift+↑ / ↓。</summary>
    public bool HotkeysEnabled { get; set; } = true;

    /// <summary>启动时恢复上次记录的强度。</summary>
    public bool RestoreOnStartup { get; set; } = true;

    /// <summary>滚轮 / 快捷键单次调整步长（百分点）。</summary>
    public int Step { get; set; } = 5;

    /// <summary>显示器 Key -> 夜间模式强度 0~100。</summary>
    public Dictionary<string, int> Strength { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>是否已经展示过首次运行提示。</summary>
    public bool FirstRunTipShown { get; set; }

    [JsonIgnore]
    public static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NightTint");

    [JsonIgnore]
    public static string FilePath => Path.Combine(SettingsDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new AppSettings();

            var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions);
            if (loaded is null) return new AppSettings();

            loaded.Strength ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string key in loaded.Strength.Keys.ToList())
                loaded.Strength[key] = Core.ColorTemperature.ClampStrength(loaded.Strength[key]);

            loaded.Step = Math.Clamp(loaded.Step, 1, 25);
            return loaded;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            string json = JsonSerializer.Serialize(this, JsonOptions);
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, FilePath, overwrite: true);
        }
        catch
        {
            // 配置保存失败不影响使用
        }
    }

    public int GetSavedStrength(string monitorKey) =>
        Strength.TryGetValue(monitorKey, out int value) ? value : -1;

    public void RememberStrength(string monitorKey, int strength) =>
        Strength[monitorKey] = Core.ColorTemperature.ClampStrength(strength);
}
