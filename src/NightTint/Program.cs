using NightTint.Core;

namespace NightTint;

internal static class Program
{
    private const string MutexName = @"Global\NightTint.SingleInstance";

    [STAThread]
    private static void Main()
    {
        string? command = ParseCommand(Environment.GetCommandLineArgs());

        // 单实例：已有实例在跑就把命令转给它（--off / --show 对正在运行的托盘同样有效）
        using var mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            if (command is not null) InstancePipe.TrySend(command);
            return;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Application.ThreadException += (_, e) => ShowError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) ShowError(ex);
        };

        using var context = new TrayAppContext(command);
        Application.Run(context);

        GC.KeepAlive(mutex);
    }

    /// <summary>把命令行参数归一化成内部命令名。</summary>
    private static string? ParseCommand(string[] args)
    {
        foreach (string arg in args.Skip(1))
        {
            switch (arg.Trim().ToLowerInvariant())
            {
                case "--show":
                case "-s":
                    return "show";
                case "--settings":
                    return "settings";
                case "--on":
                    return "on";
                case "--off":
                    return "off";
                case "--toggle":
                    return "toggle";
            }
        }
        return null;
    }

    private static void ShowError(Exception ex)
    {
        try
        {
            MessageBox.Show($"{AppInfo.DisplayName} 发生未处理的错误：\n\n{ex.Message}", AppInfo.DisplayName,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch
        {
            // 连弹窗都失败就放弃
        }
    }
}
