namespace NightLightSettings.Core;

/// <summary>软件名称统一出口，UI / 托盘 / 菜单 / 提示都用这里的文案。</summary>
internal static class AppInfo
{
    /// <summary>英文名（托盘、窗口标题、任务管理器里显示的名字）。</summary>
    public const string DisplayName = "Night light settings";

    /// <summary>完整项目名（仓库 / 发布页 / 关于信息用）。</summary>
    public const string ProjectName = "Night light settings for Windows";

    /// <summary>中文全名。</summary>
    public const string ChineseName = "Windows 夜间模式强度调节任务栏托盘";

    /// <summary>可执行文件名（不含扩展名），与 csproj 的 AssemblyName 一致。</summary>
    public const string ExeName = "NightLightSettings";

    /// <summary>完整的「英文名（中文名）」。</summary>
    public static string FullName => $"{DisplayName} · {ChineseName}";
}
