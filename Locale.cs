using System.Text.RegularExpressions;
namespace TailscaleCapsule;
public static class Locale
{
    public static string Language { get; private set; } = "zh-TW";
    public static bool English => Language == "en-US";
    public static void Initialize(string language) => Language = language.StartsWith("en",StringComparison.OrdinalIgnoreCase) ? "en-US" : "zh-TW";
    static readonly Dictionary<string,string> labels = new()
    {
        ["設定"]="Settings",["本機裝置"]="This device",["其他裝置"]="Other devices",["快速設定"]="Quick settings",["傳送紀錄"]="Transfer history",
        ["已連線"]="Connected",["已離線"]="Disconnected",["尚未登入"]="Sign in required",["等待核准"]="Awaiting approval",["正在連線"]="Connecting",["正在中斷"]="Disconnecting",["正在讀取"]="Loading",["無法取得狀態"]="Status unavailable",
        ["確認你的私人網路"]="Checking your private network",["你的裝置已在私人網路中"]="Your device is on your private network",["開啟右側開關以重新連線"]="Turn on the switch to reconnect",["請從設定開啟 Tailscale 登入"]="Open Tailscale from settings to sign in",["請在 Tailscale 管理頁核准此裝置"]="Approve this device in the Tailscale admin console",["正在建立私人網路連線"]="Connecting to your private network",["請確認 Tailscale 服務已啟動"]="Check that the Tailscale service is running",["請稍候…"]="Please wait…",
        ["尚未取得 IP"]="No IP address",["尚未指派"]="Not assigned",["未知裝置"]="Unknown device",["已複製 IP 位址"]="IP address copied",["複製本機 IP"]="Copy this device's IP",["本機的更多操作"]="More actions for this device",
        ["開啟官方 App"]="Open Tailscale app",["管理網頁"]="Admin console",["只看線上裝置"]="Show online devices",["顯示全部裝置"]="Show all devices",["切換顯示全部裝置或僅線上裝置"]="Show all or online devices",["重新整理"]="Refresh",["裝置狀態已更新"]="Device status refreshed",["更新失敗，請重新整理。"]="Refresh failed. Please try again.",
        ["目前沒有其他線上裝置"]="No other devices are online",["尚無其他裝置"]="No other devices",["加入相同 Tailnet 後，裝置會顯示在這裡"]="Devices on your tailnet will appear here",["線上"]="Online",["離線"]="Offline",["傳送檔案…"]="Send files…",["遠端桌面連線"]="Remote desktop",["查看傳送紀錄"]="View transfer history",
        ["開啟裝置面板"]="Open devices",["檢查更新"]="Check for updates",["Tailscale 連線"]="Tailscale connection",["重新整理裝置"]="Refresh devices",["Win7 玻璃質感"]="Win7 glass",["更新通知"]="Update notifications",["開機啟動 Capsule"]="Launch at sign-in",["開啟官方 Tailscale"]="Open Tailscale app",["關於 Capsule"]="About Capsule",["結束程式"]="Quit",["無法儲存開機啟動設定。"]="Couldn't save the startup preference.",
        ["關閉"]="Close",["取消"]="Cancel",["彩色裝置圖示：Icons8"]="Colored device icons: Icons8",["WinUI 3 · MiSans TC\nFont Awesome Free 6.7.2\n連線與 Taildrop 由官方 Tailscale 服務管理。"]="WinUI 3 · MiSans TC\nFont Awesome Free 6.7.2\nConnections and Taildrop are managed by the official Tailscale service.",
        ["傳送紀錄／返回裝置"]="Transfer history / back to devices",["記錄 Capsule 執行期間的 Taildrop 傳送與接收"]="Records Taildrop transfers while Capsule is running",["記錄中"]="Recording",["正在重新連線"]="Reconnecting",["尚無紀錄"]="No transfers yet",["來源裝置未提供"]="Sender unavailable",["目標裝置"]="Target device",["取消這個檔案的傳送"]="Cancel this transfer",["在資料夾中顯示"]="Show in folder",["大小未知"]="Unknown size",
        ["進行中"]="In progress",["等待傳送"]="Queued",["已傳送"]="Sent",["傳送失敗"]="Failed",["已接收"]="Received",["狀態待確認"]="Unconfirmed",["已取消"]="Cancelled",["傳送已取消或程式已結束。"]="Transfer cancelled or the application closed.",
        ["遠端桌面"]="Remote desktop",["這台電腦尚未安裝遠端桌面用戶端。"]="A remote desktop client is not installed on this PC.",["取得用戶端"]="Get a client",["無法開啟遠端桌面用戶端。"]="Couldn't open the remote desktop client.",
        ["查看更新"]="Update",["正在下載並驗證更新…"]="Downloading and verifying the update…",["更新未完成，請稍後再試。"]="Update didn't complete. Please try again later.",["檢查更新逾時，請稍後再試。"]="The update check timed out. Try again later.",["暫時無法檢查更新。"]="Couldn't check for updates right now.",
        ["無法讀取 Tailscale 狀態，請確認服務已啟動。"]="Couldn't read Tailscale status. Check that the service is running.",["Tailscale 回應逾時，請稍後重新整理。"]="Tailscale timed out. Please refresh again later.",["找不到 Tailscale，請先安裝官方 Tailscale 用戶端。"]="Tailscale was not found. Install the official Tailscale client first.",["操作逾時，請重新整理以確認目前連線狀態。"]="Operation timed out. Refresh to check the current connection.",["操作未完成，請開啟官方 Tailscale 查看狀態。"]="Operation didn't complete. Open Tailscale to check its status.",
        ["官方 Tailscale 已啟動，請從系統匣開啟。"]="Tailscale has started. Open it from the notification area.",
        ["語言 / Language"]="Language",["語言將於下次啟動套用。"]="The language will apply the next time Capsule starts."
    };
    static readonly Dictionary<string,string> prefixes = new()
    {
        ["傳出至 "]="To ",["接收自 "]="From ",["傳送至 "]="Send to ",["無法儲存傳送紀錄："]="Couldn't save transfer history: ",["無法傳送檔案："]="Couldn't send files: ",["無法複製："]="Couldn't copy: ",["無法讀取狀態："]="Couldn't read status: ",
        ["目前已是最新版本 · "]="You're up to date · ",["有新版本 "]="New version available: ",
        ["進行中 · "]="In progress · ",["等待傳送 · "]="Queued · ",["已傳送 · "]="Sent · ",["傳送失敗 · "]="Failed · ",["已接收 · "]="Received · ",["狀態待確認 · "]="Unconfirmed · ",["已取消 · "]="Cancelled · "
    };
    public static string Translate(string text)
    {
        if(!English||string.IsNullOrEmpty(text))return text;
        if(labels.TryGetValue(text,out var result))return result;
        foreach(var pair in prefixes)if(text.StartsWith(pair.Key,StringComparison.Ordinal))return pair.Value+Translate(text[pair.Key.Length..]);
        var count=Regex.Match(text,@"^(\d+) 台$");if(count.Success)return count.Groups[1].Value;
        var copy=Regex.Match(text,@"^複製 (.+) 的 IP$");if(copy.Success)return "Copy IP for "+copy.Groups[1].Value;
        if(text.EndsWith(" 的更多操作",StringComparison.Ordinal))return "More actions for "+text[..^6];
        return text;
    }
}
