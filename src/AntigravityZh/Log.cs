using System.Text;

namespace AntigravityZh;

internal static class Log
{
    private static readonly object Gate = new();

    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Home);
            var file = AppPaths.LogPath;
            var info = new FileInfo(file);
            if (info.Exists && info.Length > 1_500_000)
            {
                try { File.Move(file, file + ".old", overwrite: true); } catch { }
            }
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            lock (Gate) File.AppendAllText(file, line, new UTF8Encoding(false));
        }
        catch
        {
            // Logging must never take the helper down.
        }
    }
}
