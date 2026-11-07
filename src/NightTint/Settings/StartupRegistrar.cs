using Microsoft.Win32;

namespace NightTint.Settings;

/// <summary>
/// 开机自启（HKCU\...\Run，不需要管理员权限）。
/// </summary>
internal static class StartupRegistrar
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "NightTint";

    /// <summary>当前可执行文件的启动命令行（已加引号，路径含空格也安全）。</summary>
    public static string CommandLine
    {
        get
        {
            string exe = Environment.ProcessPath ?? Application.ExecutablePath;

            // 通过 dotnet NightTint.dll 启动时（开发调试），要带上 dll 参数
            if (exe.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            {
                string dll = Path.Combine(AppContext.BaseDirectory, "NightTint.dll");
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
