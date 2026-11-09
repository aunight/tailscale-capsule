using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TailscaleCapsule;

public record ReleaseUpdate(string Version, string Page, string Notes, string AssetName, string Download, string Digest);
public static class ReleaseUpdateClient
{
    public const string CurrentVersion = "2.1.2";
    public const string Repository = "aunight/tailscale-capsule";
    const string Prefix = "https://github.com/" + Repository + "/releases/";
    public static bool IsNewer(string candidate, string current) => Version.TryParse(candidate.TrimStart('v','V'), out var a) && Version.TryParse(current.TrimStart('v','V'), out var b) && a > b;
    static string Str(JsonElement el, string name) => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";
    public static ReleaseUpdate? Parse(string json, string current = CurrentVersion)
    {
        using var doc = JsonDocument.Parse(json); var root = doc.RootElement;
        if (root.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True || root.TryGetProperty("prerelease", out var pre) && pre.ValueKind == JsonValueKind.True) return null;
        string tag = Str(root, "tag_name"), page = Str(root,"html_url");
        if (!IsNewer(tag, current) || !page.StartsWith(Prefix + "tag/", StringComparison.Ordinal)) return null;
        string name = "", download = "", digest = "";
        if (root.TryGetProperty("assets",out var assets) && assets.ValueKind == JsonValueKind.Array)
            foreach (var asset in assets.EnumerateArray())
            {
                string n=Str(asset,"name"), url=Str(asset,"browser_download_url"), hash=Str(asset,"digest");
                if (Regex.IsMatch(n,@"^TailscaleCapsule-[0-9.]+-Setup-x64\.exe$") && url.StartsWith(Prefix+"download/"+tag+"/",StringComparison.Ordinal) && Regex.IsMatch(hash,@"^sha256:[a-fA-F0-9]{64}$"))
                { name=n;download=url;digest=hash;break; }
            }
        return new(tag,page,Str(root,"body"),name,download,digest);
    }
    static HttpClient Client()
    {
        var client=new HttpClient { Timeout=TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TailscaleCapsule-AuNight/"+CurrentVersion); return client;
    }
    public static async Task<ReleaseUpdate?> CheckAsync(CancellationToken cancellation)
    {
        using var client=Client(); client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);timeout.CancelAfter(TimeSpan.FromSeconds(12));
        return Parse(await client.GetStringAsync("https://api.github.com/repos/"+Repository+"/releases/latest",timeout.Token));
    }
    public static async Task<bool> VerifyAsync(string path,string digest,CancellationToken cancellation=default)
    {
        if(!Regex.IsMatch(digest,@"^sha256:[a-fA-F0-9]{64}$"))return false;
        await using var file=File.OpenRead(path); var hash=await SHA256.HashDataAsync(file,cancellation);
        return Convert.ToHexString(hash).Equals(digest[7..],StringComparison.OrdinalIgnoreCase);
    }
    public static async Task<string> DownloadAsync(ReleaseUpdate update,CancellationToken cancellation)
    {
        if(update.Download.Length==0||update.AssetName.Length==0)throw new InvalidOperationException("更新尚未提供可驗證的安裝程式。");
        string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TailscaleCapsule","updates");Directory.CreateDirectory(folder);
        string target=Path.Combine(folder,update.AssetName),partial=target+".partial";
        try
        {
            using var client=Client();using var response=await client.GetAsync(update.Download,HttpCompletionOption.ResponseHeadersRead,cancellation);response.EnsureSuccessStatusCode();
            const long maximum=512L*1024*1024;if(response.Content.Headers.ContentLength>maximum)throw new InvalidDataException("更新檔案大小異常。");
            await using(var input=await response.Content.ReadAsStreamAsync(cancellation))
            await using(var output=File.Create(partial))
            {
                var buffer=new byte[65536];long total=0;int count;
                while((count=await input.ReadAsync(buffer,cancellation))>0){total+=count;if(total>maximum)throw new InvalidDataException("更新檔案大小異常。");await output.WriteAsync(buffer.AsMemory(0,count),cancellation);}
            }
            if(!await VerifyAsync(partial,update.Digest,cancellation))throw new CryptographicException("更新驗證失敗，檔案未執行。");
            File.Move(partial,target,true);return target;
        }
        finally { try{if(File.Exists(partial))File.Delete(partial);}catch{} }
    }
}
