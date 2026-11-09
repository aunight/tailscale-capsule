using System.Text.Json;

namespace TailscaleCapsule;

public sealed class Preferences
{
    public bool TrayFlyout { get; set; }
    public bool Win7Glass { get; set; } = true;
    public bool OnlineOnly { get; set; }
    public bool UpdateNotifications { get; set; } = true;
    public string Language { get; set; } = "zh-TW";
    [System.Text.Json.Serialization.JsonIgnore] public bool Transient { get; set; }
    public int? X { get; set; }
    public int? Y { get; set; }
    static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TailscaleCapsule", "settings.json");
    public static Preferences Load()
    {
        try { return JsonSerializer.Deserialize<Preferences>(File.ReadAllText(FilePath)) ?? new(); }
        catch { return new(); }
    }
    public void Save()
    {
        if (Transient) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath + ".tmp", JsonSerializer.Serialize(this));
            File.Move(FilePath + ".tmp", FilePath, true);
        }
        catch (Exception ex) { App.Log(ex.Message); }
    }
}
