using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace AntigravityZh;

internal sealed record TargetInfo(string Id, string Type, string Url, string WebSocketUrl);

internal sealed class CdpClient : IAsyncDisposable
{
    private static readonly HttpClient Http = new(new HttpClientHandler { UseProxy = false })
    {
        Timeout = TimeSpan.FromSeconds(4),
    };

    public static async Task<List<TargetInfo>> ListTargetsAsync(int port)
    {
        var json = await Http.GetStringAsync($"http://127.0.0.1:{port}/json/list").ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var result = new List<TargetInfo>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            var type = item.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
            var url = item.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : null;
            var ws = item.TryGetProperty("webSocketDebuggerUrl", out var wsEl) ? wsEl.GetString() : null;
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(ws)) continue;
            result.Add(new TargetInfo(id, type ?? "", url ?? "", ws));
        }
        return result;
    }

    private readonly ClientWebSocket _ws = new();
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private Task? _receiver;
    private int _nextId;

    public string WebSocketUrl { get; }
    public bool Alive => _ws.State == WebSocketState.Open;

    public CdpClient(string webSocketUrl) => WebSocketUrl = webSocketUrl;

    public async Task ConnectAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _ws.ConnectAsync(new Uri(WebSocketUrl), timeout.Token).ConfigureAwait(false);
        _receiver = Task.Run(ReceiveLoopAsync);
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[64 * 1024];
        try
        {
            while (_ws.State == WebSocketState.Open)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    if (result.Count > 0) message.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close) break;
                if (message.Length == 0) continue;

                using var doc = JsonDocument.Parse(message.ToArray());
                if (doc.RootElement.TryGetProperty("id", out var idEl) &&
                    idEl.TryGetInt32(out var id) &&
                    _pending.TryRemove(id, out var completion))
                {
                    completion.TrySetResult(doc.RootElement.Clone());
                }
            }
        }
        catch
        {
            // Connection dropped; the helper will attach again on the next tick.
        }
        finally
        {
            foreach (var entry in _pending) entry.Value.TrySetException(new IOException("CDP connection closed"));
            _pending.Clear();
        }
    }

    private async Task<JsonElement> SendAsync(string method, string paramsJson, int timeoutMs = 15000)
    {
        if (!Alive) throw new IOException("CDP connection is not open");

        var id = Interlocked.Increment(ref _nextId);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = completion;

        var message = "{\"id\":" + id + ",\"method\":\"" + method + "\",\"params\":" + paramsJson + "}";
        var bytes = Encoding.UTF8.GetBytes(message);
        await _sendLock.WaitAsync(_cts.Token).ConfigureAwait(false);
        try
        {
            await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }

        try
        {
            return await completion.Task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs)).ConfigureAwait(false);
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    public async Task EnablePageAsync() => _ = await SendAsync("Page.enable", "{}").ConfigureAwait(false);

    public async Task<string> AddScriptAsync(string source)
    {
        var root = await SendAsync("Page.addScriptToEvaluateOnNewDocument",
            "{\"source\":" + JsonUtil.Quote(source) + "}").ConfigureAwait(false);
        return root.GetProperty("result").GetProperty("identifier").GetString() ?? "";
    }

    public async Task RemoveScriptAsync(string identifier)
    {
        _ = await SendAsync("Page.removeScriptToEvaluateOnNewDocument",
            "{\"identifier\":" + JsonUtil.Quote(identifier) + "}").ConfigureAwait(false);
    }

    public async Task<string?> EvaluateAsync(string expression)
    {
        var root = await SendAsync("Runtime.evaluate",
            "{\"expression\":" + JsonUtil.Quote(expression) +
            ",\"returnByValue\":true,\"awaitPromise\":true}").ConfigureAwait(false);
        if (root.TryGetProperty("error", out var error)) throw new IOException(error.GetRawText());
        var result = root.GetProperty("result");
        if (result.TryGetProperty("exceptionDetails", out _)) return null;
        if (result.TryGetProperty("result", out var inner) &&
            inner.TryGetProperty("value", out var value) &&
            value.ValueKind == JsonValueKind.String)
            return value.GetString();
        return null;
    }

    public async ValueTask DisposeAsync()
    {
        try { _cts.Cancel(); } catch { }
        try
        {
            if (_ws.State == WebSocketState.Open)
            {
                using var closeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", closeTimeout.Token).ConfigureAwait(false);
            }
        }
        catch
        {
            // Ignore failures while closing.
        }
        _ws.Dispose();
        if (_receiver is not null)
        {
            try { await _receiver.ConfigureAwait(false); } catch { }
        }
        _sendLock.Dispose();
        _cts.Dispose();
    }
}
