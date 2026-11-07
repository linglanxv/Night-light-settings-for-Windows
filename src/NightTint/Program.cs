namespace NightTint;

internal static class Program
{
    private const string MutexName = @"Global\NightTint.SingleInstance";

    [STAThread]
    private static void Main()
    {
        // 单实例：重复启动直接退出，避免出现两个托盘图标
        using var mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew) return;

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Application.ThreadException += (_, e) => ShowError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) ShowError(ex);
        };

        using var context = new TrayAppContext();
        Application.Run(context);

        GC.KeepAlive(mutex);
    }

    private static void ShowError(Exception ex)
    {
        try
        {
            MessageBox.Show($"NightTint 发生未处理的错误：\n\n{ex.Message}", "NightTint",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch
        {
            // 连弹窗都失败就放弃
        }
    }
}
