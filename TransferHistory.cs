using System.Text.Json;

namespace TailscaleCapsule;

public sealed record TransferEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string EventKey { get; set; } = "";
    public string Direction { get; init; } = "send";
    public string Name { get; init; } = "";
    public string PeerId { get; set; } = "";
    public string PeerName { get; set; } = "";
    public DateTimeOffset Started { get; init; } = DateTimeOffset.Now;
    public long Size { get; set; } = -1;
    public long Bytes { get; set; }
    public string Status { get; set; } = "進行中";
    public string Path { get; set; } = "";
    public string Error { get; set; } = "";
    public bool FromCapsule { get; init; }
    public bool Active => Status is "進行中" or "等待傳送";
}

public sealed class TransferHistory
{
    readonly string file;
    readonly List<TransferEntry> entries = [];
    DateTimeOffset lastWrite = DateTimeOffset.MinValue;
    public IReadOnlyList<TransferEntry> Entries => entries.OrderByDescending(x => x.Started).ToList();
    public event Action? Changed;
    public TransferHistory(string? path = null)
    {
        file = path ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TailscaleCapsule", "transfers.json");
        try { entries = JsonSerializer.Deserialize<List<TransferEntry>>(File.ReadAllText(file)) ?? []; }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
        foreach (var entry in entries.Where(x => x.Active)) entry.Status = "狀態待確認";
    }
    public TransferEntry AddSend(string path, Peer peer)
    {
        var entry = new TransferEntry { Name = System.IO.Path.GetFileName(path), Path = path, PeerId = peer.StableId, PeerName = peer.Name,
            Size = new FileInfo(path).Length, FromCapsule = true, Status = "進行中" };
        entries.Add(entry); Save(true); return entry;
    }
    public void Complete(string id, bool success, string error = "")
    {
        var entry = entries.FirstOrDefault(x => x.Id == id); if (entry == null) return;
        entry.Status = success ? "已傳送" : error.Contains("取消", StringComparison.Ordinal) ? "已取消" : "傳送失敗"; entry.Error = error;
        if (success && entry.Size >= 0) entry.Bytes = entry.Size;
        Save(true);
    }
    public void ApplyNotification(JsonElement root, IReadOnlyList<Peer> peers)
    {
        bool changed = false, terminal = false;
        if (root.TryGetProperty("OutgoingFiles", out var outgoing) && outgoing.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in outgoing.EnumerateArray())
            {
                string key = Str(item, "ID"); if (key.Length == 0) continue;
                string name = Str(item, "Name"), peerId = Str(item, "PeerID");
                var started = Date(item, "Started");
                var entry = entries.FirstOrDefault(x => x.EventKey == key) ?? entries.FirstOrDefault(x => x.FromCapsule && x.Active && x.Name == name && x.PeerId == peerId && Math.Abs((x.Started - started).TotalSeconds) < 20);
                if (entry == null) { entry = new TransferEntry { EventKey = key, Direction = "send", Name = name, PeerId = peerId, Started = started }; entries.Add(entry); }
                entry.EventKey = key; entry.PeerName = peers.FirstOrDefault(x => x.StableId == peerId)?.Name ?? (entry.PeerName.Length > 0 ? entry.PeerName : "目標裝置");
                entry.Size = Num(item, "DeclaredSize", -1); entry.Bytes = Num(item, "Sent");
                entry.Status = Bool(item, "Finished") ? Bool(item, "Succeeded") ? "已傳送" : "傳送失敗" : "進行中";
                terminal |= Bool(item, "Finished");
                changed = true;
            }
        }
        if (root.TryGetProperty("IncomingFiles", out var incoming) && incoming.ValueKind == JsonValueKind.Array)
        {
            var present = new HashSet<string>();
            foreach (var item in incoming.EnumerateArray())
            {
                string name = Str(item, "Name"); var started = Date(item, "Started");
                string key = "receive:" + started.ToString("O") + ":" + name; present.Add(key);
                var entry = entries.FirstOrDefault(x => x.EventKey == key);
                if (entry == null) { entry = new TransferEntry { EventKey = key, Direction = "receive", Name = name, PeerName = "來源裝置未提供", Started = started }; entries.Add(entry); }
                entry.Size = Num(item, "DeclaredSize", -1); entry.Bytes = Num(item, "Received"); entry.Path = Str(item, "FinalPath");
                entry.Status = Bool(item, "Done") ? "已接收" : "進行中"; terminal |= Bool(item, "Done"); changed = true;
            }
            foreach (var entry in entries.Where(x => x.Direction == "receive" && x.Active && !present.Contains(x.EventKey)))
            {
                entry.Status = entry.Path.Length > 0 && File.Exists(entry.Path) && (entry.Size < 0 || new FileInfo(entry.Path).Length == entry.Size) ? "已接收" : "狀態待確認";
                changed = true; terminal = true;
            }
        }
        if (changed) Save(terminal);
    }
    static string Str(JsonElement element, string name) => element.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";
    static long Num(JsonElement element, string name, long fallback = 0) => element.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var n) ? n : fallback;
    static bool Bool(JsonElement element, string name) => element.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;
    static DateTimeOffset Date(JsonElement element, string name) => DateTimeOffset.TryParse(Str(element, name), out var value) ? value : DateTimeOffset.Now;
    public void Flush() => Save(true);
    void Save(bool force = false)
    {
        // Bound the history without dropping active operations.
        if (entries.Count > 1000) { var retain = entries.OrderByDescending(x => x.Started).Take(1000).Select(x => x.Id).ToHashSet(); entries.RemoveAll(x => !x.Active && !retain.Contains(x.Id)); }
        if (force || (DateTimeOffset.UtcNow - lastWrite).TotalSeconds >= 1)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(file))!);
            File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(entries)); File.Move(file + ".tmp", file, true); lastWrite = DateTimeOffset.UtcNow;
        }
        Changed?.Invoke();
    }
}
