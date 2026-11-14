using System.Text;
using System.Text.Json;

namespace AntigravityZh;

internal static class Helper
{
    private sealed class Attached
    {
        public required CdpClient Client { get; init; }
        public required string ScriptId { get; set; }
    }

    public static async Task RunAsync()
    {
        using var mutex = new Mutex(initiallyOwned: true, name: @"Local\AntigravityZhHelper", createdNew: out var created);
        if (!created)
        {
            Log.Write("helper already running");
            return;
        }

        ResourceStore.EnsureExtracted();
        Log.Write($"helper started, pid={Environment.ProcessId}");
        WriteStatus(AppPaths.ReadPort(), 0);
        Console.WriteLine("Antigravity 中文汉化已启动。按 Ctrl+C 退出。");

        var attached = new Dictionary<string, Attached>(StringComparer.Ordinal);
        var missing = LoadMissing();
        var script = ResourceStore.BuildScript();
        var dictMtime = SafeMtime(AppPaths.DictPath);
        var injectMtime = SafeMtime(AppPaths.InjectPath);
        int? lastPort = null;
        var lastMissing = DateTime.MinValue;
        var lastStatus = DateTime.MinValue;

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        while (!cts.IsCancellationRequested)
        {
            try
            {
                var port = AppPaths.ReadPort();
                if (port is int p)
                {
                    if (lastPort != p) Log.Write("debug port: " + p);
                    lastPort = p;

                    List<TargetInfo> targets;
                    try { targets = await CdpClient.ListTargetsAsync(p).ConfigureAwait(false); }
                    catch { targets = new List<TargetInfo>(); }

                    foreach (var target in targets.Where(IsHubPage))
                    {
                        if (attached.ContainsKey(target.Id)) continue;
                        try
                        {
                            var client = new CdpClient(target.WebSocketUrl);
                            await client.ConnectAsync().ConfigureAwait(false);
                            await client.EnablePageAsync().ConfigureAwait(false);
                            var scriptId = await client.AddScriptAsync(script).ConfigureAwait(false);
                            await client.EvaluateAsync(script).ConfigureAwait(false);
                            attached[target.Id] = new Attached { Client = client, ScriptId = scriptId };
                            Log.Write($"attached {target.Id} {Truncate(target.Url, 90)}");
                        }
                        catch (Exception ex)
                        {
                            Log.Write($"attach failed {target.Id}: {ex.Message}");
                        }
                    }
                }

                foreach (var key in attached.Where(kv => !kv.Value.Client.Alive).Select(kv => kv.Key).ToList())
                    attached.Remove(key);

                var dm = SafeMtime(AppPaths.DictPath);
                var im = SafeMtime(AppPaths.InjectPath);
                if (dm != dictMtime || im != injectMtime)
                {
                    dictMtime = dm;
                    injectMtime = im;
                    try
                    {
                        script = ResourceStore.BuildScript();
                        foreach (var entry in attached.Values)
                        {
                            try
                            {
                                await entry.Client.RemoveScriptAsync(entry.ScriptId).ConfigureAwait(false);
                                entry.ScriptId = await entry.Client.AddScriptAsync(script).ConfigureAwait(false);
                                await entry.Client.EvaluateAsync(script).ConfigureAwait(false);
                            }
                            catch
                            {
                                // The client may have gone away; it will be dropped next tick.
                            }
                        }
                        Log.Write("dictionary changed, reloaded");
                    }
                    catch (Exception ex)
                    {
                        Log.Write("reload failed: " + ex.Message);
                    }
                }

                if (DateTime.UtcNow - lastMissing > TimeSpan.FromSeconds(15))
                {
                    lastMissing = DateTime.UtcNow;
                    var changed = false;
                    foreach (var entry in attached.Values)
                    {
                        if (!entry.Client.Alive) continue;
                        try
                        {
                            var json = await entry.Client.EvaluateAsync(
                                "window.__agyZhMissing ? JSON.stringify(window.__agyZhMissing(300)) : \"[]\"").ConfigureAwait(false);
                            if (string.IsNullOrWhiteSpace(json)) continue;
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.ValueKind != JsonValueKind.Array) continue;
                            foreach (var item in doc.RootElement.EnumerateArray())
                            {
                                var text = item.GetString();
                                if (!string.IsNullOrWhiteSpace(text) && missing.Add(text)) changed = true;
                            }
                        }
                        catch
                        {
                            // Skip this pass.
                        }
                    }
                    if (changed) SaveMissing(missing);
                }

                if (DateTime.UtcNow - lastStatus > TimeSpan.FromSeconds(5))
                {
                    lastStatus = DateTime.UtcNow;
                    WriteStatus(lastPort, attached.Count);
                }
            }
            catch (Exception ex)
            {
                Log.Write("loop error: " + ex.Message);
            }

            try { await Task.Delay(1000, cts.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }

        foreach (var entry in attached.Values)
            await entry.Client.DisposeAsync().ConfigureAwait(false);
        Log.Write("helper stopped");
    }

    private static bool IsHubPage(TargetInfo target) =>
        target.Type == "page" &&
        target.Url.StartsWith("https://127.0.0.1:", StringComparison.Ordinal);

    private static long SafeMtime(string path)
    {
        try { return File.GetLastWriteTimeUtc(path).Ticks; } catch { return 0; }
    }

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..length];

    private static HashSet<string> LoadMissing()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            if (!File.Exists(AppPaths.MissingPath)) return set;
            using var doc = JsonDocument.Parse(File.ReadAllText(AppPaths.MissingPath, Encoding.UTF8));
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return set;
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var text = item.GetString();
                if (!string.IsNullOrWhiteSpace(text)) set.Add(text);
            }
        }
        catch
        {
            // A broken file is not worth failing over.
        }
        return set;
    }

    private static void SaveMissing(IEnumerable<string> values)
    {
        try
        {
            var sb = new StringBuilder();
            sb.Append("[\n");
            var items = values.OrderBy(v => v, StringComparer.Ordinal).ToList();
            for (var i = 0; i < items.Count; i++)
            {
                sb.Append("  ").Append(JsonUtil.Quote(items[i]));
                if (i < items.Count - 1) sb.Append(',');
                sb.Append('\n');
            }
            sb.Append("]\n");
            Directory.CreateDirectory(AppPaths.Home);
            File.WriteAllText(AppPaths.MissingPath, sb.ToString(), new UTF8Encoding(false));
        }
        catch
        {
            // Best effort.
        }
    }

    private static void WriteStatus(int? port, int attachedCount)
    {
        try
        {
            var json = "{\n" +
                       "  \"pid\": " + Environment.ProcessId + ",\n" +
                       "  \"port\": " + (port?.ToString() ?? "null") + ",\n" +
                       "  \"attached\": " + attachedCount + ",\n" +
                       "  \"dictEntries\": " + ResourceStore.DictEntryCount() + ",\n" +
                       "  \"updated\": " + JsonUtil.Quote(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")) + "\n" +
                       "}\n";
            Directory.CreateDirectory(AppPaths.Home);
            File.WriteAllText(AppPaths.StatusPath, json, new UTF8Encoding(false));
        }
        catch
        {
            // Best effort.
        }
    }
}
