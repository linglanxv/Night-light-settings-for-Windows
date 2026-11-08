using Microsoft.Win32;

namespace NightLightSettings.Settings;

/// <summary>
/// 开机自启（HKCU\...\Run，不需要管理员权限）。
/// </summary>
internal static class StartupRegistrar
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "NightLightSettings";

    /// <summary>改名前的旧注册项，启动时顺手清掉，避免两个自启动项指向旧路径。</summary>
    private const string LegacyValueName = "NightTint";

    /// <summary>当前可执行文件的启动命令行（已加引号，路径含空格也安全）。</summary>
    public static string CommandLine
    {
        get
        {
            string exe = Environment.ProcessPath ?? Application.ExecutablePath;

            // 通过 dotnet NightLightSettings.dll 启动时（开发调试），要带上 dll 参数
            if (exe.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            {
                string dll = Path.Combine(AppContext.BaseDirectory, "NightLightSettings.dll");
                return File.Exists(dll) ? $"\"{exe}\" \"{dll}\"" : $"\"{exe}\"";
            }

            return $"\"{exe}\"";
        }
    }

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key is null) return;

            // 清理历史版本留下的旧值（改名前的 "NightTint"）
            if (key.GetValue(LegacyValueName) is not null)
                key.DeleteValue(LegacyValueName, throwOnMissingValue: false);

            if (enabled)
                key.SetValue(ValueName, CommandLine, RegistryValueKind.String);
            else if (key.GetValue(ValueName) is not null)
                key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch
        {
            // 组策略限制等情况下写入可能失败，忽略
        }
    }

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string value && value.Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
