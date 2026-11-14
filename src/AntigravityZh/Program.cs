using System.Text;

namespace AntigravityZh;

internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        try
        {
            var command = args.Length == 0 ? "run" : args[0].ToLowerInvariant();
            var launch = !args.Contains("--no-launch", StringComparer.OrdinalIgnoreCase);

            switch (command)
            {
                case "run":
                    return RunHelper(background: false);
                case "--background":
                case "background":
                    return RunHelper(background: true);
                case "install":
                    Installer.Install(launch);
                    return 0;
                case "uninstall":
                    Installer.Uninstall();
                    return 0;
                case "status":
                    Installer.Status();
                    return 0;
                case "launch":
                    AppPaths.LaunchAntigravity();
                    return 0;
                case "version":
                case "--version":
                    Console.WriteLine("AntigravityZh 0.1.0");
                    return 0;
                default:
                    PrintHelp();
                    return 1;
            }
        }
        catch (Exception ex)
        {
            try { Console.Error.WriteLine("错误: " + ex.Message); } catch { }
            Log.Write("fatal: " + ex);
            return 1;
        }
    }

    private static int RunHelper(bool background)
    {
        if (background)
        {
            Native.HideConsole();
        }
        else
        {
            Console.WriteLine("Antigravity 中文汉化 v0.1.0");
            Console.WriteLine("等待 Antigravity 启动... 按 Ctrl+C 退出。");
        }

        Helper.RunAsync().GetAwaiter().GetResult();
        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            AntigravityZh - Antigravity 非官方中文汉化

            用法:
              AntigravityZh.exe                前台运行汉化助手
              AntigravityZh.exe --background   后台运行(无窗口)
              AntigravityZh.exe install        安装到本机并加入开机自启
              AntigravityZh.exe install --no-launch
                                               安装但不自动启动 Antigravity
              AntigravityZh.exe uninstall      移除开机自启并停止
              AntigravityZh.exe status         查看当前状态
              AntigravityZh.exe launch         启动 Antigravity

            安装目录: %LOCALAPPDATA%\AntigravityZh
            词典文件: %LOCALAPPDATA%\AntigravityZh\dict.json
            """);
    }
}
