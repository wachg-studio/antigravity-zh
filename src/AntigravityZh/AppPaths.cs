using System.Diagnostics;
using System.Globalization;

namespace AntigravityZh;

internal static class AppPaths
{
    public static string Home
    {
        get
        {
            var custom = Environment.GetEnvironmentVariable("ANTIGRAVITY_ZH_HOME");
            return string.IsNullOrWhiteSpace(custom)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AntigravityZh")
                : custom;
        }
    }

    public static string DictPath => Path.Combine(Home, "dict.json");
    public static string InjectPath => Path.Combine(Home, "inject.js");
    public static string MissingPath => Path.Combine(Home, "missing.json");
    public static string LogPath => Path.Combine(Home, "helper.log");
    public static string StatusPath => Path.Combine(Home, "status.json");

    public static IEnumerable<string> UserDataDirs()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dirs = new List<string>();
        var fallback = Path.Combine(appData, "Antigravity");
        dirs.Add(fallback);
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(appData))
            {
                var name = Path.GetFileName(dir);
                if (name.Contains("antigravity", StringComparison.OrdinalIgnoreCase) &&
                    !dirs.Contains(dir, StringComparer.OrdinalIgnoreCase))
                {
                    dirs.Add(dir);
                }
            }
        }
        catch
        {
            // Ignore inaccessible profile directories.
        }
        return dirs;
    }

    public static int? ReadPort()
    {
        foreach (var dir in UserDataDirs())
        {
            var file = Path.Combine(dir, "DevToolsActivePort");
            if (!File.Exists(file)) continue;
            try
            {
                var line = File.ReadLines(file).FirstOrDefault();
                if (int.TryParse(line?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) && port > 0)
                    return port;
            }
            catch
            {
                // Ignore a half-written port file and try again on the next tick.
            }
        }
        return null;
    }

    public static string? FindAntigravityExe()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        var candidates = new[]
        {
            Path.Combine(local, "Programs", "antigravity", "Antigravity.exe"),
            Path.Combine(local, "Programs", "Antigravity", "Antigravity.exe"),
            Path.Combine(local, "Programs", "Antigravity IDE", "Antigravity IDE.exe"),
            Path.Combine(local, "Programs", "Antigravity IDE", "Antigravity.exe"),
            Path.Combine(programFiles, "Antigravity", "Antigravity.exe"),
            Path.Combine(programFiles, "Google", "Antigravity", "Antigravity.exe"),
            Path.Combine(programFilesX86, "Antigravity", "Antigravity.exe"),
        };
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate)) return candidate;
        }

        foreach (var root in new[] { Path.Combine(local, "Programs"), programFiles, programFilesX86 })
        {
            try
            {
                if (!Directory.Exists(root)) continue;
                var options = new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    MaxRecursionDepth = 3,
                    IgnoreInaccessible = true,
                    MatchCasing = MatchCasing.CaseInsensitive,
                };
                foreach (var file in Directory.EnumerateFiles(root, "Antigravity*.exe", options))
                {
                    var name = Path.GetFileName(file);
                    if (name.StartsWith("Uninstall", StringComparison.OrdinalIgnoreCase)) continue;
                    if (name.Equals("Antigravity.exe", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("Antigravity IDE.exe", StringComparison.OrdinalIgnoreCase))
                        return file;
                }
            }
            catch
            {
                // Keep looking in the next root.
            }
        }
        return null;
    }

    public static bool IsAntigravityRunning() =>
        Process.GetProcessesByName("Antigravity").Length > 0;

    public static void LaunchAntigravity()
    {
        var exe = FindAntigravityExe();
        if (exe is null)
        {
            Console.WriteLine("未找到 Antigravity 安装位置,请手动启动 Antigravity。");
            return;
        }
        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
    }
}
