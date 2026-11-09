using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using Windows.Storage.Pickers;

namespace TailscaleCapsule;

public sealed partial class MainWindow
{
    readonly TransferHistory transferHistory = new(DemoMode ? Path.Combine(Path.GetTempPath(), "TailscaleCapsule-Preview-" + Environment.ProcessId, "transfers.json") : null);
    readonly StackPanel historyBody = new() { Spacing = 16, Margin = new(24, 8, 24, 24) };
    readonly StackPanel historyRows = new() { Spacing = 10 };
    readonly ScrollViewer historyScroll = new() { Visibility = Visibility.Collapsed, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    readonly TextBlock monitorStatus = Text("Taildrop", 12, Muted);
    readonly Microsoft.UI.Xaml.Shapes.Ellipse monitorDot = new() { Width = 6, Height = 6, Fill = Brush(0xFFFFC65A), VerticalAlignment = VerticalAlignment.Center };
    readonly DispatcherTimer historyTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    readonly Dictionary<string, CancellationTokenSource> sends = [];
    Button? historyHeaderButton;
    bool historyMode, pickingFiles, historyDirty, monitorConnected;
    void AddHistoryHeader(StackPanel tools)
    {
        historyHeaderButton = IconButton("\uf1da", "傳送紀錄／返回裝置", (_, _) => { if (historyMode) ShowPanel(); else ShowHistory(); }); tools.Children.Add(historyHeaderButton);
    }
    void InitializeHistoryUI()
    {
        monitorStatus.TextWrapping = TextWrapping.Wrap;
        var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        statusRow.Children.Add(Icon("\uf362", 14, Muted)); statusRow.Children.Add(monitorStatus); statusRow.Children.Add(monitorDot);
        SetTip(statusRow, "記錄 Capsule 執行期間的 Taildrop 傳送與接收"); historyBody.Children.Add(statusRow);
        historyBody.Children.Add(historyRows);
        historyScroll.Content = historyBody; Grid.SetRow(historyScroll, 1); root.Children.Add(historyScroll);
        transferHistory.Changed += () => historyDirty = true;
        historyTimer.Tick += (_, _) => { if (historyDirty && historyMode) { historyDirty = false; RenderHistory(); } };
        historyTimer.Start();
        RenderHistory();
    }
    void StartTransferMonitor()
    {
        _ = service.WatchTransfersAsync(message =>
        {
            var copy = message.Clone();
            DispatcherQueue.TryEnqueue(() =>
            {
                if (exiting) return;
                try { transferHistory.ApplyNotification(copy, state?.Peers ?? []); }
                catch (Exception ex) { App.Log("Transfer history: " + ex.Message); monitorStatus.Text = L("無法儲存傳送紀錄：" + ex.Message); }
            });
        }, connected => DispatcherQueue.TryEnqueue(() =>
        {
            if (exiting || connected == monitorConnected) return;
            monitorConnected = connected; monitorStatus.Text = "Taildrop"; monitorDot.Fill = connected ? Brush(0xFF42F58D) : Brush(0xFFFFC65A);
            SetTip(monitorStatus, connected ? "記錄中" : "正在重新連線");
        }), lifetime.Token);
    }
    public void ShowHistory() { _ = SwitchPanelAsync(false, true); }
    void RenderHistory()
    {
        double offset = historyScroll.VerticalOffset;
        historyRows.Children.Clear();
        foreach (var entry in transferHistory.Entries.Take(100)) historyRows.Children.Add(TransferRow(entry));
        if (historyRows.Children.Count == 0)
        {
            var empty = new StackPanel { Spacing = 14, Padding = new(0, 36, 0, 44), HorizontalAlignment = HorizontalAlignment.Center };
            empty.Children.Add(Icon("\uf362", 28, Brush(0x80F8F5ED)));
            empty.Children.Add(Text("尚無紀錄", 14, Muted)); historyRows.Children.Add(empty);
        }
        if (historyMode) { ResizePanel(); historyScroll.ChangeView(null, offset, null, true); }
    }
    Border TransferRow(TransferEntry entry)
    {
        var row = new Grid { ColumnSpacing = 12, Padding = new(0, 10, 0, 10) };
        row.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        row.Children.Add(new Border { Width = 28, Child = Icon(entry.Direction == "send" ? "\uf093" : "\uf019", 18, Ink), VerticalAlignment = VerticalAlignment.Top, Margin = new(0, 4, 0, 0) });
        var text = new StackPanel { Spacing = 6 };
        var name = Text(entry.Name, 14, Ink, true); SetTip(name, entry.Name); text.Children.Add(name);
        text.Children.Add(Text($"{(entry.Direction == "send" ? "傳出至" : "接收自")} {entry.PeerName}", 12, Muted));
        var result = Text($"{entry.Status} · {FormatBytes(entry.Size)} · {entry.Started.LocalDateTime:MM/dd HH:mm}", 11, Ink); result.Opacity = .6; text.Children.Add(result);
        if (entry.Active) text.Children.Add(new ProgressBar { Minimum = 0, Maximum = Math.Max(1, entry.Size), Value = Math.Max(0, Math.Min(entry.Bytes, Math.Max(1, entry.Size))), IsIndeterminate = entry.Size <= 0, Height = 3, Margin = new(0, 4, 0, 0) });
        if (entry.Error.Length > 0) { var error = Text(entry.Error, 11, Brush(0xFFFFB29F)); error.TextWrapping = TextWrapping.Wrap; text.Children.Add(error); }
        Grid.SetColumn(text, 1); row.Children.Add(text);
        if (sends.TryGetValue(entry.Id, out var pending))
        {
            var cancel = IconButton("\uf05e", "取消這個檔案的傳送", (_, _) => pending.Cancel()); Grid.SetColumn(cancel, 2); row.Children.Add(cancel);
        }
        else if (entry.Path.Length > 0 && File.Exists(entry.Path))
        {
            var reveal = IconButton("\uf07b", "在資料夾中顯示", (_, _) =>
            { try { var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = true }; start.ArgumentList.Add("/select," + entry.Path); Process.Start(start); } catch (Exception ex) { Toast(ex.Message, true); } });
            Grid.SetColumn(reveal, 2); row.Children.Add(reveal);
        }
        return Card(row, 4, 0);
    }
    static string FormatBytes(long size) => size < 0 ? L("大小未知") : size >= 1024L * 1024 * 1024 ? $"{size / (1024d * 1024 * 1024):0.##} GB" : size >= 1024 * 1024 ? $"{size / (1024d * 1024):0.##} MB" : size >= 1024 ? $"{size / 1024d:0.##} KB" : $"{size} B";
    async Task PickAndSendAsync(Peer peer)
    {
        if (DemoMode) return;
        if (pickingFiles || exiting) return;
        pickingFiles = true;
        try
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.Downloads, CommitButtonText = L("傳送至 " + peer.Name) };
            picker.FileTypeFilter.Add("*"); WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            var files = await picker.PickMultipleFilesAsync();
            pickingFiles = false;
            if (files.Count == 0) return;
            ShowHistory();
            foreach (var file in files)
            {
                var entry = transferHistory.AddSend(file.Path, peer);
                using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); sends[entry.Id] = cancellation;
                var result = await service.SendFileAsync(file.Path, peer.IP, cancellation.Token);
                sends.Remove(entry.Id); transferHistory.Complete(entry.Id, result.Success, result.Message);
                if (exiting) break;
            }
        }
        catch (Exception ex) { App.Log("Taildrop send: " + ex); Toast("無法傳送檔案：" + ex.Message, true); }
        finally { pickingFiles = false; }
    }
    void PopulateDemoHistory()
    {
        string json = System.Text.Json.JsonSerializer.Serialize(new { OutgoingFiles = new[] {
            new { ID="demo-1", PeerID="demo-a", Name="Project Notes.pdf", Started=DateTimeOffset.Now.AddMinutes(-12), DeclaredSize=2516582L, Sent=2516582L, Finished=true, Succeeded=true },
            new { ID="demo-2", PeerID="demo-b", Name="Reference Images.zip", Started=DateTimeOffset.Now.AddMinutes(-3), DeclaredSize=7340032L, Sent=7340032L, Finished=true, Succeeded=true } },
            IncomingFiles = new[] { new { Name="Voice Memo.m4a",Started=DateTimeOffset.Now.AddMinutes(-6),DeclaredSize=1638400L,Received=1638400L,Done=true } } });
        using var doc = System.Text.Json.JsonDocument.Parse(json); transferHistory.ApplyNotification(doc.RootElement, state?.Peers ?? []);
        monitorConnected = true; monitorStatus.Text = "Taildrop"; monitorDot.Fill = Brush(0xFF42F58D);
    }
    async Task OpenRemoteDesktopAsync(Peer peer, string client)
    {
        if (DemoMode) return;
        try
        {
            if (File.Exists(client)) { var start = new ProcessStartInfo(client) { UseShellExecute = true }; start.ArgumentList.Add("/v:" + peer.IP); Process.Start(start); return; }
            pickingFiles = true;
            var dialog = new ContentDialog { Title=L("遠端桌面"), Content=L("這台電腦尚未安裝遠端桌面用戶端。"), PrimaryButtonText=L("取得用戶端"), CloseButtonText=L("取消"), XamlRoot=root.XamlRoot, RequestedTheme=ElementTheme.Dark };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary) Process.Start(new ProcessStartInfo("https://learn.microsoft.com/windows-server/remote/remote-desktop-services/clients/remote-desktop-clients") { UseShellExecute=true });
        }
        catch(Exception ex) { App.Log(ex.Message); Toast("無法開啟遠端桌面用戶端。",true); }
        finally { pickingFiles=false; }
    }
}
