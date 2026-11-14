using System.Diagnostics;
using System.Text;
using Microsoft.Win32;

namespace AntigravityZh;

internal static class Installer
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "AntigravityZh";

    private static string StartupLauncher => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup), "AntigravityZh.vbs");

    public static void Install(bool launch)
    {
        ResourceStore.EnsureExtracted();
        var home = AppPaths.Home;
        Directory.CreateDirectory(home);

        var source = Environment.ProcessPath ?? throw new InvalidOperationException("无法确定程序路径");
        var target = Path.Combine(home, "AntigravityZh.exe");
        StopHelpers();
        if (!string.Equals(Path.GetFullPath(source), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
            File.Copy(source, target, overwrite: true);

        var vbs = Path.Combine(home, "start-hidden.vbs");
        WriteLauncher(vbs, target);
        WriteLauncher(StartupLauncher, target);
        StartHidden(vbs);

        Console.WriteLine("安装目录: " + home);
        Console.WriteLine("已加入开机自启: " + StartupLauncher);
        Console.WriteLine("汉化助手已在后台启动。");

        if (launch && !AppPaths.IsAntigravityRunning())
        {
            AppPaths.LaunchAntigravity();
            Console.WriteLine("已启动 Antigravity。界面加载后几秒内会切换为中文。");
        }
        else if (AppPaths.IsAntigravityRunning())
        {
            Console.WriteLine("Antigravity 正在运行,汉化会自动生效。");
        }
        else
        {
            Console.WriteLine("请手动启动 Antigravity;汉化助手会等它出现后自动连接。");
        }
    }

    public static void Uninstall()
    {
        RemoveRunValue();
        try { if (File.Exists(StartupLauncher)) File.Delete(StartupLauncher); } catch { }
        StopHelpers();
        Console.WriteLine("已移除开机自启,并停止汉化助手。");

        var home = AppPaths.Home;
        if (Directory.Exists(home))
        {
            try
            {
                var psi = new ProcessStartInfo("cmd.exe",
                    $"/c timeout /t 2 /nobreak >nul & rmdir /s /q \"{home}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                Process.Start(psi);
                Console.WriteLine("程序文件会在退出后删除: " + home);
            }
            catch
            {
                Console.WriteLine("请手动删除目录: " + home);
            }
        }
    }

    public static void Status()
    {
        Console.WriteLine("安装目录: " + AppPaths.Home);
        Console.WriteLine("词典条数: " + ResourceStore.DictEntryCount());
        var port = AppPaths.ReadPort();
        Console.WriteLine("Antigravity 调试端口: " + (port is null ? "未检测到(应用可能未运行)" : port.ToString()));

        if (File.Exists(AppPaths.StatusPath))
        {
            try
            {
                Console.WriteLine("助手状态:");
                Console.WriteLine(File.ReadAllText(AppPaths.StatusPath, Encoding.UTF8).TrimEnd());
            }
            catch { }
        }
        else
        {
            Console.WriteLine("助手状态: 尚未启动或没有写过状态文件");
        }

        var runEnabled = false;
        using (var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false))
            runEnabled = key?.GetValue(RunValue) is string;
        var startupEnabled = File.Exists(StartupLauncher);
        Console.WriteLine(runEnabled || startupEnabled
            ? "开机自启: 已启用"
            : "开机自启: 未启用");
    }

    private static void WriteLauncher(string path, string exe)
    {
        var command = $"\"{exe}\" --background";
        var vbs = "Set sh = CreateObject(\"WScript.Shell\")\r\n" +
                  "sh.Run \"" + command.Replace("\"", "\"\"") + "\", 0, False\r\n";
        File.WriteAllText(path, vbs, Encoding.Unicode);
    }

    private static void RemoveRunValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(RunValue, throwOnMissingValue: false);
        // Some startup managers park disabled entries in a subkey instead of deleting them.
        using var disabled = Registry.CurrentUser.OpenSubKey(RunKey + @"\LenovoDisabled", writable: true);
        disabled?.DeleteValue(RunValue, throwOnMissingValue: false);
    }

    private static void StartHidden(string vbs)
    {
        var wscript = Path.Combine(Environment.SystemDirectory, "wscript.exe");
        var psi = new ProcessStartInfo(wscript, $"\"{vbs}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        Process.Start(psi);
    }

    private static void StopHelpers()
    {
        foreach (var process in Process.GetProcessesByName("AntigravityZh"))
        {
            if (process.Id == Environment.ProcessId) continue;
            try
            {
                process.Kill();
                process.WaitForExit(2000);
            }
            catch
            {
                // Ignore processes that already exited.
            }
        }
    }
}
