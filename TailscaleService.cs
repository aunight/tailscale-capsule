using System.Diagnostics;
using System.Text.Json;

namespace TailscaleCapsule;

public record Peer(string Id, string Name, string OS, string IP, bool Online, string DNSName, string StableId = "");
public record NetworkState(string BackendState, string HostName, string IP, string Tailnet, List<Peer> Peers, string? Error = null)
{
    public bool Connected => BackendState == "Running";
}
public record CommandResult(bool Success, string Message);

public sealed class TailscaleService
{
    private readonly string exe;
    public TailscaleService(string? executable = null)
    {
        exe = executable ?? new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tailscale", "tailscale.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tailscale", "tailscale.exe") }
            .FirstOrDefault(File.Exists) ?? "tailscale.exe";
    }
    internal async Task<(int Code, string Output, string Error)> RunAsync(string[] args, TimeSpan timeout, CancellationToken cancellation = default)
    {
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8 };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = psi };
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        cts.CancelAfter(timeout);
        try
        {
            process.Start();
            var stdout = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderr = process.StandardError.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);
            return (process.ExitCode, await stdout, await stderr);
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            throw;
        }
    }
    public async Task<NetworkState> StatusAsync(CancellationToken cancellation = default)
    {
        try
        {
            var result = await RunAsync(["status", "--json"], TimeSpan.FromSeconds(8), cancellation);
            if (string.IsNullOrWhiteSpace(result.Output))
                return Failure(string.IsNullOrWhiteSpace(result.Error) ? "無法讀取 Tailscale 狀態，請確認服務已啟動。" : result.Error.Trim());
            return Parse(result.Output);
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { return Failure("Tailscale 回應逾時，請稍後重新整理。"); }
        catch (System.ComponentModel.Win32Exception) { return Failure("找不到 Tailscale，請先安裝官方 Tailscale 用戶端。"); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return Failure($"無法讀取狀態：{ex.Message}"); }
    }
    public static NetworkState Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var backend = Str(root, "BackendState", "Unknown");
        root.TryGetProperty("Self", out var self);
        root.TryGetProperty("CurrentTailnet", out var tailnet);
        var peers = new List<Peer>();
        if (root.TryGetProperty("Peer", out var map) && map.ValueKind == JsonValueKind.Object)
        {
            foreach (var entry in map.EnumerateObject())
            {
                var p = entry.Value;
                peers.Add(new Peer(entry.Name, Str(p, "HostName", "未知裝置").Trim(), Str(p, "OS"), IP(p),
                    p.TryGetProperty("Online", out var online) && online.ValueKind == JsonValueKind.True, Str(p, "DNSName").TrimEnd('.'), Str(p, "ID")));
            }
        }
        return new NetworkState(backend, Str(self, "HostName", Environment.MachineName).Trim(), IP(self), Str(tailnet, "Name"), peers);
    }
    static string Str(JsonElement el, string key, string fallback = "") => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(key, out var val) && val.ValueKind == JsonValueKind.String ? val.GetString() ?? fallback : fallback;
    static string IP(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty("TailscaleIPs", out var ips) || ips.ValueKind != JsonValueKind.Array) return "";
        var list = ips.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToArray();
        return list.FirstOrDefault(x => !x.Contains(':')) ?? list.FirstOrDefault() ?? "";
    }
    static NetworkState Failure(string message) => new("Error", Environment.MachineName, "", "", [], message);
    public async Task<CommandResult> ConnectAsync(bool connect, CancellationToken cancellation = default)
    {
        try
        {
            var result = await RunAsync(connect ? ["up", "--timeout=15s"] : ["down"], TimeSpan.FromSeconds(20), cancellation);
            return new(result.Code == 0, result.Code == 0 ? "" : result.Error.Trim());
        }
        catch (OperationCanceledException) { return new(false, "操作逾時，請重新整理以確認目前連線狀態。"); }
        catch (Exception ex) { return new(false, ex.Message); }
    }
    public async Task<CommandResult> PingAsync(string ip, CancellationToken cancellation = default)
    {
        try
        {
            var result = await RunAsync(["ping", "--c", "1", "--timeout", "5s", ip], TimeSpan.FromSeconds(8), cancellation);
            return new(result.Code == 0, string.IsNullOrWhiteSpace(result.Output) ? result.Error.Trim() : result.Output.Trim());
        }
        catch (OperationCanceledException) { return new(false, "連線測試逾時。"); }
        catch (Exception ex) { return new(false, ex.Message); }
    }
    public async Task<CommandResult> SendFileAsync(string path, string ip, CancellationToken cancellation)
    {
        try
        {
            var result = await RunAsync(["file", "cp", "--update-interval=0", System.IO.Path.GetFullPath(path), ip + ":"], TimeSpan.FromHours(24), cancellation);
            return new(result.Code == 0, result.Error.Trim());
        }
        catch (OperationCanceledException) { return new(false, "傳送已取消或程式已結束。"); }
        catch (Exception ex) { return new(false, ex.Message); }
    }
    public async Task WatchTransfersAsync(Action<JsonElement> onMessage, Action<bool> onConnection, CancellationToken cancellation)
    {
        while (!cancellation.IsCancellationRequested)
        {
            using var process = new Process { StartInfo = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } };
            foreach (var arg in new[] { "debug", "watch-ipn", "--initial-outgoing-files", "--peer-changes=false", "--peer-patches=false" }) process.StartInfo.ArgumentList.Add(arg);
            try
            {
                process.Start();
                using var kill = cancellation.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch { } });
                var errorDrain = process.StandardError.ReadToEndAsync(cancellation);
                await foreach (var message in JsonSerializer.DeserializeAsyncEnumerable<JsonElement>(process.StandardOutput.BaseStream, topLevelValues: true,
                    options: new JsonSerializerOptions { DefaultBufferSize = 1024 }, cancellationToken: cancellation))
                {
                    onConnection(true); onMessage(message);
                }
                await process.WaitForExitAsync(cancellation); await errorDrain;
            }
            catch (Exception) when (!cancellation.IsCancellationRequested) { }
            finally { try { if (!process.HasExited) process.Kill(true); } catch { } onConnection(false); }
            if (!cancellation.IsCancellationRequested) await Task.Delay(3000, cancellation);
        }
    }
}
