using System.Reflection;
using System.Text;
using System.Text.Json;

namespace AntigravityZh;

internal static class ResourceStore
{
    private const string DictResource = "AntigravityZh.Resources.dict.json";
    private const string InjectResource = "AntigravityZh.Resources.inject.js";

    public static void EnsureExtracted()
    {
        Directory.CreateDirectory(AppPaths.Home);
        if (!File.Exists(AppPaths.DictPath))
            File.WriteAllText(AppPaths.DictPath, ReadResource(DictResource), new UTF8Encoding(false));
        if (!File.Exists(AppPaths.InjectPath))
            File.WriteAllText(AppPaths.InjectPath, ReadResource(InjectResource), new UTF8Encoding(false));
    }

    public static string BuildScript()
    {
        var dict = File.ReadAllText(AppPaths.DictPath, Encoding.UTF8).TrimStart('\uFEFF');
        var inject = File.ReadAllText(AppPaths.InjectPath, Encoding.UTF8).TrimStart('\uFEFF');
        return ";window.__agyZhDict = " + dict + ";\n" +
               inject +
               "\n;window.__agyZhSetDict && window.__agyZhSetDict(window.__agyZhDict);";
    }

    public static int DictEntryCount()
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(AppPaths.DictPath, Encoding.UTF8));
            return doc.RootElement.ValueKind == JsonValueKind.Object
                ? doc.RootElement.EnumerateObject().Count()
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static string ReadResource(string name)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
        {
            var suffix = "." + Path.GetFileName(name);
            stream = assembly.GetManifestResourceNames()
                .Where(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                .Select(assembly.GetManifestResourceStream)
                .FirstOrDefault(s => s is not null);
        }
        if (stream is null) throw new InvalidOperationException("缺少内置资源: " + name);
        using (stream)
        using (var reader = new StreamReader(stream, Encoding.UTF8))
            return reader.ReadToEnd();
    }
}
