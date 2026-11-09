using TailscaleCapsule;
using System.Diagnostics;
Console.OutputEncoding = System.Text.Encoding.UTF8;
if (args.Contains("--hang")) { await Task.Delay(30000); return; }
if (args.Contains("--flood"))
{
    for (int i = 0; i < 5000; i++) { Console.WriteLine("本機 MacBook · 中文測試 " + i); Console.Error.WriteLine(new string('x', 100)); }
    return;
}
void Check(bool condition, string label) { if (!condition) throw new Exception(label); Console.WriteLine("PASS " + label); }
var state = TailscaleService.Parse("""
{"BackendState":"Running","Self":{"HostName":"我的電腦","TailscaleIPs":["fd7a::1","100.1.2.3"]},"CurrentTailnet":{"Name":"private"},"Peer":{"a":{"HostName":"MacBook 中文","OS":"macOS","TailscaleIPs":["100.4.5.6"],"Online":true,"DNSName":"mac.private."},"b":{"HostName":"phone","Online":false}}}
""");
Check(state.Connected && state.HostName == "我的電腦" && state.IP == "100.1.2.3", "UTF-8 host and IPv4 preference");
Check(state.Peers.Count == 2 && state.Peers[0].Online && !state.Peers[1].Online && state.Peers[0].DNSName == "mac.private", "actual online and offline peer data");
Check(TailscaleService.Parse("""{"BackendState":"Stopped","Self":null,"Peer":null}""").Peers.Count == 0, "stopped/null state is not cached as live devices");
Check(TailscaleService.Parse("""{"BackendState":"NeedsLogin"}""").Connected == false, "login is distinct from connected");
try { TailscaleService.Parse("not json"); throw new Exception("invalid JSON accepted"); } catch (System.Text.Json.JsonException) { Console.WriteLine("PASS malformed JSON rejection"); }
var service = new TailscaleService(Environment.ProcessPath);
var flood = await service.RunAsync(["--flood"], TimeSpan.FromSeconds(10));
Check(flood.Code == 0 && flood.Output.Contains("本機") && flood.Error.Length > 400000, "stdout and stderr drain concurrently without deadlock");
var clock = Stopwatch.StartNew();
try { await service.RunAsync(["--hang"], TimeSpan.FromMilliseconds(400)); throw new Exception("timeout missing"); }
catch (OperationCanceledException) { Check(clock.Elapsed.TotalSeconds < 4, "hung process is cancelled and terminated"); }
if (!args.Contains("--offline")) {
var live = await new TailscaleService().StatusAsync();
Check(live.Error == null, "installed Tailscale status read");
Console.WriteLine($"Live backend: {live.BackendState}; peers: {live.Peers.Count}");
Check(live.Peers.All(x => x.Name == x.Name.Trim()), "leading host name whitespace removed");
}
var fixtureDir = Path.Combine(AppContext.BaseDirectory, "history-fixture-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(fixtureDir);
var historyPath = Path.Combine(fixtureDir, "transfers.json"); var history = new TransferHistory(historyPath);
var target = new Peer("public", "Test PC", "windows", "100.1.1.1", true, "test.local", "stable-1");
void Notify(string json) { using var doc = System.Text.Json.JsonDocument.Parse(json); history.ApplyNotification(doc.RootElement, [target]); }
const string pending = """{"OutgoingFiles":[{"ID":"tx-1","PeerID":"stable-1","Name":"中文 測試.txt","Started":"2026-11-10T00:00:00Z","DeclaredSize":128,"Sent":64,"Finished":false}]}""";
Notify(pending); Notify(pending); Check(history.Entries.Count == 1 && history.Entries[0].Bytes == 64 && history.Entries[0].PeerName == "Test PC", "outgoing progress events deduplicate and resolve device");
Notify("""{"OutgoingFiles":[{"ID":"tx-1","PeerID":"stable-1","Name":"中文 測試.txt","Started":"2026-11-10T00:00:00Z","DeclaredSize":128,"Sent":128,"Finished":true,"Succeeded":false}]}""");
Check(history.Entries[0].Status == "傳送失敗", "finished is not assumed successful");
Notify("""{"IncomingFiles":[{"Name":"received.txt","Started":"2026-11-10T00:01:00Z","DeclaredSize":10,"Received":3}]}"""); Notify("""{"IncomingFiles":[]}""");
Check(history.Entries.Single(x => x.Direction == "receive").Status == "狀態待確認", "disappearing partial receive is not falsely completed");
Notify("""{"IncomingFiles":[{"Name":"received.txt","Started":"2026-11-10T00:01:00Z","DeclaredSize":10,"Received":10,"Done":true}]}""");
Check(history.Entries.Single(x => x.Direction == "receive").Status == "已接收", "received completion uses actual Done event");
Check(new TransferHistory(historyPath).Entries.Count == 2, "history survives reload");
var localFile = Path.Combine(fixtureDir, "local-test.txt"); File.WriteAllText(localFile, "sample");
var own = history.AddSend(localFile, target);
var outgoingFixture = System.Text.Json.JsonSerializer.Serialize(new { OutgoingFiles = new[] { new { ID="own-tx",PeerID="stable-1",Name=own.Name,Started=own.Started,DeclaredSize=own.Size,Sent=own.Size,Finished=true,Succeeded=true } } });
Notify(outgoingFixture); history.Complete(own.Id, true);
Check(history.Entries.Count == 3 && history.Entries.Single(x => x.Id == own.Id).EventKey == "own-tx", "Capsule and daemon events merge to one record");
Check(ReleaseUpdateClient.IsNewer("v2.1.10", "2.1.9") && !ReleaseUpdateClient.IsNewer("v2.1.0", "2.1.0"), "update versions compare numerically");
var releaseFixture = System.Text.Json.JsonSerializer.Serialize(new { tag_name="v2.1.1", html_url="https://github.com/aunight/tailscale-capsule/releases/tag/v2.1.1", draft=false,prerelease=false, body="Notes", assets=new[] { new { name="TailscaleCapsule-2.1.1-Setup-x64.exe",browser_download_url="https://github.com/aunight/tailscale-capsule/releases/download/v2.1.1/TailscaleCapsule-2.1.1-Setup-x64.exe",digest="sha256:"+new string('a',64) } } });
Check(ReleaseUpdateClient.Parse(releaseFixture)?.Download.Length > 0, "update accepts verifiable installer from the configured repository");
Check(ReleaseUpdateClient.Parse(releaseFixture.Replace("https://github.com/aunight/tailscale-capsule/releases/tag/", "https://example.com/tag/")) == null, "update rejects release pages outside the configured repository");
Check(ReleaseUpdateClient.Parse(releaseFixture.Replace("\"prerelease\":false", "\"prerelease\":true")) == null, "stable updates ignore prereleases");
string fileDigest="sha256:"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(localFile))).ToLowerInvariant();
Check(await ReleaseUpdateClient.VerifyAsync(localFile,fileDigest) && !await ReleaseUpdateClient.VerifyAsync(localFile,"sha256:"+new string('0',64)), "installer SHA-256 rejects modified files");
int releaseArg=Array.IndexOf(args,"--release-json");
if(releaseArg>=0){var published=ReleaseUpdateClient.Parse(File.ReadAllText(args[releaseArg+1]),"0.0.0");Check(published!=null&&published.Download.Length>0,"published GitHub release exposes the verifiable update installer");}
Locale.Initialize("en-US");
Check(Locale.Translate("本機裝置")=="This device" && Locale.Translate("已傳送 · 7 MB")=="Sent · 7 MB", "English device and transfer labels");
Check(Locale.Translate("複製 My PC 的 IP")=="Copy IP for My PC" && Locale.Translate("MacBook Pro 的更多操作")=="More actions for MacBook Pro", "English tooltips preserve device names");
Locale.Initialize("zh-TW");Check(Locale.Translate("本機裝置")=="本機裝置", "Traditional Chinese is preserved");
if (!args.Contains("--offline")) {
using var monitorCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2)); int notifications = 0;
try { await new TailscaleService().WatchTransfersAsync(_ => notifications++, _ => {}, monitorCancellation.Token); } catch(OperationCanceledException) { }
Check(notifications > 0, "live Taildrop event subscription receives JSON and cancels cleanly");

}
