using System.IO.Pipes;

namespace NightTint.Core;

/// <summary>
/// 单实例之间的轻量命令通道（命名管道）。
/// 让「已经在托盘里跑着」的实例也能响应 --show / --settings / --on / --off / --toggle，
/// 这样把命令做成快捷方式、脚本或 Stream Deck 按钮都能用。
/// </summary>
internal static class InstancePipe
{
    private const string PipeName = "NightTint.Ipc";

    /// <summary>把命令发给已在运行的实例；没有实例在跑或超时返回 false。</summary>
    public static bool TrySend(string command, int timeoutMs = 1500)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(timeoutMs);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine(command);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>在后台线程等待命令，交给 handler（handler 负责切回 UI 线程）。</summary>
    public static void StartServer(Action<string> handler)
    {
        var thread = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1);
                    server.WaitForConnection();
                    using var reader = new StreamReader(server);
                    string? line = reader.ReadLine();
                    if (!string.IsNullOrWhiteSpace(line))
                        handler(line.Trim().ToLowerInvariant());
                }
                catch
                {
                    Thread.Sleep(400); // 管道被占用 / 异常时稍等再重建
                }
            }
        })
        {
            IsBackground = true,
            Name = "NightTint.Ipc"
        };
        thread.Start();
    }
}
