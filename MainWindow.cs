using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Win32;
using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.UI;

namespace TailscaleCapsule;

public sealed partial class MainWindow : Window
{
    public static bool DemoMode => Environment.GetCommandLineArgs().Contains("--demo");
    static Color Gold = new Windows.UI.ViewManagement.UISettings().GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
    static readonly SolidColorBrush Ink = Brush(0xFFF8F5ED), Muted = Brush(0xFFDDD8CC), Accent = new(Gold);
    static readonly FontFamily SolidFont = new("ms-appx:///Assets/fa-solid-900.ttf#Font Awesome 6 Free");
    static readonly FontFamily BrandsFont = new("ms-appx:///Assets/fa-brands-400.ttf#Font Awesome 6 Brands");
    static readonly FontFamily RegularFont = new("ms-appx:///Assets/fa-regular-400.ttf#Font Awesome 6 Free");
    static ControlTemplate? cleanButtonTemplate;
    static ControlTemplate CleanButtonTemplate => cleanButtonTemplate ??= (ControlTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
    <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Button">
      <Grid Background="Transparent">
        <VisualStateManager.VisualStateGroups>
          <VisualStateGroup x:Name="CommonStates">
            <VisualState x:Name="Normal" />
            <VisualState x:Name="PointerOver"><VisualState.Setters><Setter Target="Surface.Background" Value="#18FFFFFF" /></VisualState.Setters></VisualState>
            <VisualState x:Name="Pressed"><VisualState.Setters><Setter Target="Surface.Background" Value="#28FFFFFF" /></VisualState.Setters></VisualState>
            <VisualState x:Name="Disabled"><VisualState.Setters><Setter Target="Surface.Opacity" Value="0.4" /></VisualState.Setters></VisualState>
          </VisualStateGroup>
        </VisualStateManager.VisualStateGroups>
        <Border x:Name="Surface" Background="{TemplateBinding Background}" CornerRadius="{TemplateBinding CornerRadius}" Padding="{TemplateBinding Padding}">
          <ContentPresenter Content="{TemplateBinding Content}" ContentTemplate="{TemplateBinding ContentTemplate}" HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="{TemplateBinding VerticalContentAlignment}" />
        </Border>
      </Grid>
    </ControlTemplate>
    """);
    readonly TailscaleService service = new();
    readonly Preferences preferences = DemoMode ? new Preferences { Transient = true } : Preferences.Load();
    readonly CancellationTokenSource lifetime = new();
    readonly Grid root = new(), header = new();
    readonly StackPanel body = new() { Spacing = 18 }, peers = new() { Spacing = 20 };
    readonly StackPanel quickBody = new() { Spacing = 8, Margin = new(20, 0, 20, 20), Visibility = Visibility.Collapsed };
    readonly ScrollViewer quickScroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Visibility = Visibility.Collapsed };
    readonly TextBlock titleCaption = Text("Tailscale Capsule", 19, Ink, true);
    ToggleSwitch? quickConnect, glassToggle, startupToggle, updateToggle;
    Button? quickCopy;
    bool quickMode;
    readonly TextBlock headerStatus = Text("", 12, Muted), statusTitle = Text("正在讀取", 18),
        statusDetail = Text("確認你的私人網路", 13, Muted), selfName = Text("", 17),
        selfIP = Text("尚未取得 IP", 12), deviceCount = Text("", 11, Muted);
    readonly ToggleSwitch connect = new() { OffContent = "", OnContent = "", MinWidth = 44, VerticalAlignment = VerticalAlignment.Center };
    readonly ScrollViewer peerScroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    readonly InfoBar notice = new() { IsOpen = false, IsClosable = true, CornerRadius = new(10), Visibility = Visibility.Collapsed };
    readonly Button refresh, selfCopy, onlineFilter;
    readonly Border copyToast = new() { Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom,
        Margin = new(20, 0, 20, 18), Padding = new(16, 10, 16, 10), CornerRadius = new(999), Background = new AcrylicBrush { TintColor = Color.FromArgb(255, 32, 31, 27), TintOpacity = .50, TintLuminosityOpacity = .40, FallbackColor = Color.FromArgb(240, 43, 41, 35) } };
    readonly TextBlock copyToastText = Text("已複製 IP 位址", 13, Ink);
    readonly DispatcherTimer copyToastTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    int visibilityRevision;
    Storyboard? surfaceAnimation;
    Storyboard? pageAnimation;
    bool switchingPanel;
    readonly CompositeTransform surfaceTransform = new();
    readonly TextBlock connectionIcon = Icon("\uf3ed", 22, Muted);
    readonly DispatcherTimer timer, toastTimer;
    readonly TrayIcon tray;
    readonly nint hwnd;
    readonly Windows.UI.ViewManagement.UISettings systemColors = new();
    readonly Microsoft.UI.Xaml.Shapes.Ellipse localStatusDot = new() { Width = 8, Height = 8, Fill = Brush(0xFF42F58D), VerticalAlignment = VerticalAlignment.Center };
    NetworkState? state;
    bool refreshing, mutating, syncing, exiting;
    string renderedPeers = "";
    bool everLoaded;

    public MainWindow()
    {
        preferences.TrayFlyout = true;
        Title = DemoMode ? "Tailscale Capsule · Preview" : "Tailscale Capsule · WinUI 3";
        hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        root.RequestedTheme = ElementTheme.Dark;
        root.RenderTransform = surfaceTransform;
        root.Resources["ContentControlThemeFontFamily"] = new FontFamily("MiSans TC");
        root.Resources["SystemAccentColor"] = Gold;
        root.Resources["ToggleSwitchFillOn"] = Accent;
        root.Resources["ToggleSwitchFillOnPointerOver"] = Brush(0xFFFFDA85);
        root.Resources["ToggleSwitchFillOnPressed"] = Brush(0xFFE8B43F);
        root.Resources["ToggleSwitchKnobFillOn"] = Brush(0xFF352A14);
        root.Resources["TextControlBorderBrushFocused"] = Accent;
        root.Resources["TextControlBackground"] = Brush(0x18000000);
        root.Resources["TextControlBorderBrush"] = Brush(0x18FFFFFF);
        ApplyMaterial();
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        root.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });

        header.Height = 72; header.Margin = new(24, 0, 16, 0);
        header.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        var emblem = new Grid { Width = 21, Height = 21, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        for (int i = 0; i < 9; i++) emblem.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse { Width = 5, Height = 5, Fill = i % 3 == 1 ? Ink : Brush(0xFF878787), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new((i % 3) * 8, (i / 3) * 8, 0, 0) });
        title.Children.Add(new Border { Width = 32, Height = 32, Background = Brush(0xFF242426), CornerRadius = new(8), Child = emblem, VerticalAlignment = VerticalAlignment.Center });
        var titleText = new StackPanel { Spacing = 1 };
        titleText.Children.Add(titleCaption);
        title.Children.Add(titleText);
        header.Children.Add(title);
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        AddHistoryHeader(tools);
        tools.Children.Add(IconButton("\uf013", "設定", (_, _) => { if (quickMode) ShowPanel(); else ShowQuickPanel(); }));
        Grid.SetColumn(tools, 1); header.Children.Add(tools); root.Children.Add(header);

        body.Margin = new(24, 0, 24, 20);
        Grid.SetRow(body, 1); root.Children.Add(body);
        body.Children.Add(notice);
        var connection = new Grid { ColumnSpacing = 12 };
        connection.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        connection.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        connection.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        connection.Children.Add(new Border { Width = 30, Child = connectionIcon, VerticalAlignment = VerticalAlignment.Center });
        var status = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        statusTitle.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
        status.Children.Add(statusTitle); status.Children.Add(statusDetail);
        Grid.SetColumn(status, 1); connection.Children.Add(status);
        Grid.SetColumn(connect, 2); connection.Children.Add(connect);
        SetAccessibleName(connect, "Tailscale 連線");
        connect.Toggled += async (_, _) => { if (!syncing) await ToggleConnectionAsync(connect.IsOn); };
        body.Children.Add(Card(connection, 16, 0x0FFFFFFF));

        var machine = new StackPanel { Spacing = 0 };
        var localHeader = new Grid { Height = 38, Margin = new(4, 0, 4, 0) };
        localHeader.Children.Add(SectionLabel("本機裝置"));
        machine.Children.Add(localHeader);
        var machineLine = new Grid { ColumnSpacing = 12, MinHeight = 56 };
        machineLine.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        machineLine.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        machineLine.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var localIdentity = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        localIdentity.Children.Add(localStatusDot);
        localIdentity.Children.Add(DeviceIcon("windows")); machineLine.Children.Add(localIdentity);
        var machineText = new StackPanel { Spacing = 7, VerticalAlignment = VerticalAlignment.Center };
        selfName.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
        selfName.FontSize = 15; selfIP.FontSize = 11; selfIP.Opacity = .5;
        machineText.Children.Add(selfName); machineText.Children.Add(selfIP); Grid.SetColumn(machineText, 1); machineLine.Children.Add(machineText);
        selfCopy = IconButton("\uf0c5", "複製本機 IP", (_, _) => Copy(state?.IP ?? ""));
        var localActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var localMore=IconButton("\uf142","本機的更多操作",null);
        var localMenu=new MenuFlyout();
        var openApp=new MenuFlyoutItem { Text="開啟官方 App",FontFamily=new("MiSans TC"),FontSize=14,MinHeight=44,Icon=new FontIcon{Glyph="\uf35d",FontFamily=SolidFont,FontSize=20} };
        openApp.Click+=(_,_)=>{if(!DemoMode)OpenOfficial();};localMenu.Items.Add(openApp);
        var manage=new MenuFlyoutItem { Text="管理網頁",FontFamily=new("MiSans TC"),FontSize=14,MinHeight=44,Icon=new FontIcon{Glyph="\uf0ac",FontFamily=SolidFont,FontSize=20} };
        manage.Click+=(_,_)=>{if(!DemoMode)Process.Start(new ProcessStartInfo("https://login.tailscale.com/admin/machines"){UseShellExecute=true});};localMenu.Items.Add(manage);
        LocalizeMenu(localMenu);localMore.Flyout=localMenu;localActions.Children.Add(selfCopy);localActions.Children.Add(localMore);
        Grid.SetColumn(localActions, 2); machineLine.Children.Add(localActions);
        machine.Children.Add(Card(machineLine, 4, 0)); body.Children.Add(machine);

        var listHeader = new Grid { Height = 38, Margin = new(4, 0, 4, 0) };
        listHeader.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        listHeader.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var listTitle = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        listTitle.Children.Add(SectionLabel("其他裝置")); deviceCount.Opacity = .45; listTitle.Children.Add(deviceCount); listHeader.Children.Add(listTitle);
        onlineFilter = IconButton("\uf0b0", "只看線上裝置", null);
        SetTip(onlineFilter, "切換顯示全部裝置或僅線上裝置");
        onlineFilter.Click += async (_, _) => await ToggleFilterAsync();
        var deviceTools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        deviceTools.Children.Add(onlineFilter);
        refresh = IconButton("\uf2f1", "重新整理", async (_, _) => await RefreshAsync(true)); refresh.Opacity = .55; deviceTools.Children.Add(refresh);
        Grid.SetColumn(deviceTools, 1); listHeader.Children.Add(deviceTools);
        var devices = new StackPanel { Spacing = 0 };
        devices.Children.Add(listHeader);
        peerScroll.Content = peers; peerScroll.MaxHeight = 270; devices.Children.Add(peerScroll);
        body.Children.Add(devices);

        Content = root;
        BuildQuickPanel(); quickScroll.Content = quickBody; Grid.SetRow(quickScroll, 1); root.Children.Add(quickScroll);
        InitializeHistoryUI();
        var toastContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        toastContent.Children.Add(Icon("\uf00c", 12, Ink)); toastContent.Children.Add(copyToastText); copyToast.Child = toastContent;
        Grid.SetRowSpan(copyToast, 2); Canvas.SetZIndex(copyToast, 10); root.Children.Add(copyToast);
        copyToastTimer.Tick += (_, _) => { copyToast.Visibility = Visibility.Collapsed; copyToastTimer.Stop(); };
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false); presenter.IsResizable = false; presenter.IsMaximizable = false;
        }
        AppWindow.IsShownInSwitchers = false;
        int rounded = 2; Native.DwmSetWindowAttribute(hwnd, 33, ref rounded, 4);
        int border = unchecked((int)0xFFFFFFFE); Native.DwmSetWindowAttribute(hwnd, 34, ref border, 4);
        if (AppWindow.Presenter is OverlappedPresenter popupPresenter) popupPresenter.IsAlwaysOnTop = true;
        ResizePanel();
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        int x = area.X + area.Width - AppWindow.Size.Width - 12;
        int y = area.Y + area.Height - AppWindow.Size.Height - 12;
        AppWindow.Move(new(Math.Clamp(x, area.X, Math.Max(area.X, area.X + area.Width - AppWindow.Size.Width)), Math.Clamp(y, area.Y, Math.Max(area.Y, area.Y + area.Height - AppWindow.Size.Height))));
        AppWindow.Changed += (_, args) => { if (args.DidPositionChange) { preferences.X = AppWindow.Position.X; preferences.Y = AppWindow.Position.Y; preferences.Save(); } };
        AppWindow.Closing += (_, args) => { if (!exiting) { args.Cancel = true; HidePanel(); } };
        tray = new TrayIcon(hwnd, ShowPanel, TogglePanel, ShowQuickPanel, !DemoMode);
        Activated += (_, args) => { if (args.WindowActivationState == WindowActivationState.Deactivated && !switchingPanel && !pickingFiles && !Environment.GetCommandLineArgs().Contains("--inspect")) HidePanel(); };
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += async (_, _) => { await RefreshAsync(); if (!DemoMode && preferences.UpdateNotifications && DateTimeOffset.UtcNow - lastUpdateCheck > TimeSpan.FromHours(6)) await CheckUpdatesAsync(false); };
        toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        toastTimer.Tick += (_, _) => { notice.IsOpen = false; notice.Visibility = Visibility.Collapsed; toastTimer.Stop(); ResizePanel(); };
        notice.CloseButtonClick += (_, _) => { notice.IsOpen = false; notice.Visibility = Visibility.Collapsed; ResizePanel(); };
        systemColors.ColorValuesChanged += SystemColorsChanged;
        UpdateAccent();
        Closed += (_, _) => { systemColors.ColorValuesChanged -= SystemColorsChanged; try { transferHistory.Flush(); } catch (Exception ex) { App.Log(ex.Message); } lifetime.Cancel(); timer.Stop(); toastTimer.Stop(); copyToastTimer.Stop(); historyTimer.Stop(); tray.Dispose(); lifetime.Dispose(); };
    }

    public async Task InitializeAsync()
    {
        if (everLoaded) return;
        everLoaded = true;
        await RefreshAsync(); timer.Start();
        if (!DemoMode) { StartTransferMonitor(); if (preferences.UpdateNotifications) _ = CheckUpdatesAsync(false); }
        else PopulateDemoHistory();
        App.Log("Ready: WinUI 3");
    }

    static SolidColorBrush Brush(uint argb) => new(Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
    static string L(string text)=>Locale.Translate(text);
    static void SetTip(DependencyObject element,object? value)=>ToolTipService.SetToolTip(element,value is string text?L(text):value);
    static void SetAccessibleName(DependencyObject element,string value)=>AutomationProperties.SetName(element,L(value));
    static void LocalizeMenu(MenuFlyout menu){foreach(var item in menu.Items.OfType<MenuFlyoutItem>())item.Text=L(item.Text);}
    void ChangeLanguage(string language)
    {
        if(DemoMode||preferences.Language==language)return;
        preferences.Language=language;preferences.Save();
        if(transferHistory.Entries.Any(x=>x.Active)){Toast("語言將於下次啟動套用。");return;}
        var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=true};
        start.ArgumentList.Add("--show");start.ArgumentList.Add("--restart");start.ArgumentList.Add(Environment.ProcessId.ToString());
        Process.Start(start);ExitApp();
    }
    static TextBlock Text(string text, double size = 13, Brush? foreground = null, bool bold = false) => new()
    { Text = L(text), FontSize = size, Foreground = foreground ?? Ink, FontFamily = new("MiSans TC, Microsoft JhengHei UI"),
      FontWeight = bold ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal,
      TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    static TextBlock Icon(string glyph, double size = 20, Brush? color = null, bool brand = false) => new()
    { Text = glyph, FontFamily = brand ? BrandsFont : SolidFont, FontSize = size, Foreground = color ?? Ink, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    static TextBlock SectionLabel(string label) { var text = Text(label, 11, Ink); text.FontWeight = Microsoft.UI.Text.FontWeights.Light; text.Opacity = .45; return text; }
    static Border DeviceIcon(string os)
    {
        bool apple = os.ToLowerInvariant() is "macos" or "darwin";
        bool iphone = os.Equals("ios",StringComparison.OrdinalIgnoreCase);
        string asset = os.ToLowerInvariant() switch { "ios" => "ios.png", "android" => "android.png", "linux" => "linux.png", _ => "windows.png" };
        UIElement visual = apple ? new FontIcon { Glyph = "\uf179", FontFamily = BrandsFont, FontSize = 26, Foreground = Ink, Width = 30, Height = 30, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } : new Image { Source = new BitmapImage(new Uri("ms-appx:///Assets/" + asset)), Width = 28, Height = 28, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        return new Border { Width = 40, Height = 40, VerticalAlignment = VerticalAlignment.Center, Child = visual };
    }
    static Border Card(UIElement child, double padding, uint bg = 0x08FFFFFF) => new()
    { Background = Brush(bg), BorderThickness = new(0), CornerRadius = new(14), Padding = new(padding), Child = child };
    static Button IconButton(string glyph, string label, RoutedEventHandler? click)
    {
        var button = new Button { Content = Icon(glyph == "\uf0c5" ? "\uf24d" : glyph, glyph is "\uf0b0" or "\uf2f1" ? 14 : 16), Width = 38, Height = 38, MinWidth = 38, MinHeight = 38, Padding = new(0), Background = Brush(0), BorderThickness = new(0), CornerRadius = new(8), Template = CleanButtonTemplate };
        SetAccessibleName(button, label); SetTip(button, label);
        if (click != null) button.Click += click;
        return button;
    }
    Button CopyButton(TextBlock label, string name, Func<string> value)
    {
        label.FontFamily = new("MiSans TC, Microsoft JhengHei UI"); label.Foreground = Ink; label.Opacity = .5;
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        line.Children.Add(label); line.Children.Add(Icon("\uf24d", 14, Ink));
        var button = new Button { Content = line, CornerRadius = new(6), BorderThickness = new(0), Padding = new(8, 10, 8, 10), Background = Brush(0), VerticalAlignment = VerticalAlignment.Center };
        SetAccessibleName(button, name); SetTip(button, name);
        button.Click += (_, _) => Copy(value());
        return button;
    }
    void Copy(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        try { var data = new DataPackage(); data.SetText(value); Clipboard.SetContent(data); ShowCopyToast("已複製 IP 位址"); }
        catch (Exception ex) { Toast($"無法複製：{ex.Message}", true); }
    }
    async Task RefreshAsync(bool manual = false)
    {
        if (refreshing || mutating || exiting) return;
        refreshing = true; refresh.IsEnabled = false;
        try
        {
            state = DemoMode ? new NetworkState("Running", "Workstation", "100.90.0.1", "", [new("demo-a", "Studio PC", "windows", "100.90.0.2", true, "", "demo-a"), new("demo-b", "MacBook Pro", "macOS", "100.90.0.3", true, "", "demo-b"), new("demo-c", "Android", "android", "100.90.0.4", false, "", "demo-c"), new("demo-d", "iPhone", "ios", "100.90.0.5", true, "", "demo-d")]) : await service.StatusAsync(lifetime.Token);
            syncing = true; connect.IsOn = state.Connected; syncing = false;
            connect.IsEnabled = state.Error == null;
            SyncQuickPanel();
            (statusTitle.Text, statusDetail.Text, headerStatus.Text) = state.BackendState switch
            {
                "Running" => ("已連線", "你的裝置已在私人網路中", "私人網路 · 已連線"),
                "Stopped" => ("已離線", "開啟右側開關以重新連線", "私人網路 · 已離線"),
                "NeedsLogin" => ("尚未登入", "請從設定開啟 Tailscale 登入", "需要登入"),
                "NeedsMachineAuth" => ("等待核准", "請在 Tailscale 管理頁核准此裝置", "等待裝置核准"),
                "Starting" => ("正在連線", "正在建立私人網路連線", "正在連線"),
                _ => ("無法取得狀態", "請確認 Tailscale 服務已啟動", "狀態無法取得")
            };
            statusTitle.Text=L(statusTitle.Text);statusDetail.Text=L(statusDetail.Text);
            connectionIcon.Text = state.Connected ? "\uf3ed" : "\uf011";
            connectionIcon.Foreground = Muted;
            selfName.Text = state.HostName;
            selfIP.Text = string.IsNullOrEmpty(state.IP) ? L("尚未指派") : state.IP;
            selfCopy.IsEnabled = !string.IsNullOrEmpty(state.IP);
            deviceCount.Text = L($"{state.Peers.Count} 台");
            RenderPeers();
            localStatusDot.Fill = state.Connected ? Brush(0xFF42F58D) : Brush(0xFFFF7777);
            tray.Update($"Tailscale Capsule · {statusTitle.Text}", state.Connected);
            if (state.Error != null) Toast(state.Error, true, false);
            else if (manual) Toast("裝置狀態已更新");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { App.Log(ex.ToString()); Toast("更新失敗，請重新整理。", true); }
        finally { syncing = false; refreshing = false; if (!exiting) refresh.IsEnabled = true; }
    }
    void RenderPeers()
    {
        if (state == null) return;
        onlineFilter.Background = preferences.OnlineOnly ? Brush(0x18FFFFFF) : Brush(0);
        ((TextBlock)onlineFilter.Content).Foreground = preferences.OnlineOnly ? Accent : Ink;
        onlineFilter.Opacity = preferences.OnlineOnly ? .9 : .55;
        SetTip(onlineFilter, preferences.OnlineOnly ? "顯示全部裝置" : "只看線上裝置");
        var visible = state.Peers.Where(p => !preferences.OnlineOnly || p.Online)
            .OrderByDescending(p => p.Online).ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var key = System.Text.Json.JsonSerializer.Serialize(new { visible, state.BackendState, preferences.OnlineOnly });
        if (key == renderedPeers) return;
        renderedPeers = key; peers.Children.Clear();
        foreach (var peer in visible) peers.Children.Add(PeerRow(peer));
        if (visible.Count == 0)
        {
            var empty = new StackPanel { Spacing = 6, Padding = new(16), HorizontalAlignment = HorizontalAlignment.Center };
            empty.Children.Add(Text(preferences.OnlineOnly ? "目前沒有其他線上裝置" : "尚無其他裝置", 14, Muted));
            empty.Children.Add(Text("加入相同 Tailnet 後，裝置會顯示在這裡", 12, Muted)); peers.Children.Add(empty);
        }
        ResizePanel();
    }
    int filterRevision;
    async Task ToggleFilterAsync()
    {
        int revision = ++filterRevision;
        preferences.OnlineOnly = !preferences.OnlineOnly; preferences.Save();
        if (new Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
        {
            var fade = new DoubleAnimation { To = 0, Duration = new(TimeSpan.FromMilliseconds(90)) };
            Storyboard.SetTarget(fade, peers); Storyboard.SetTargetProperty(fade, "Opacity");
            var animation = new Storyboard(); animation.Children.Add(fade); animation.Begin(); await Task.Delay(95);
            if (revision != filterRevision) return;
            animation.Stop();
        }
        RenderPeers();
        var enter = new DoubleAnimation { From = 0, To = 1, Duration = new(TimeSpan.FromMilliseconds(200)), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        Storyboard.SetTarget(enter, peers); Storyboard.SetTargetProperty(enter, "Opacity"); var reveal = new Storyboard(); reveal.Children.Add(enter); reveal.Begin();
    }
    Border PeerRow(Peer peer)
    {
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var identity = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        var statusDot = new Microsoft.UI.Xaml.Shapes.Ellipse { Width = 8, Height = 8, Fill = peer.Online ? Brush(0xFF42F58D) : Brush(0xFFFF7777), VerticalAlignment = VerticalAlignment.Center };
        SetAccessibleName(statusDot, peer.Online ? "線上" : "離線"); SetTip(statusDot, peer.Online ? "線上" : "離線");
        identity.Children.Add(statusDot); identity.Children.Add(DeviceIcon(peer.OS)); row.Children.Add(identity);
        var text = new StackPanel { Spacing = 7, VerticalAlignment = VerticalAlignment.Center };
        var name = Text(peer.Name, 15, Ink, true); SetTip(name, peer.Name); text.Children.Add(name);
        var secondary = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        var ipLabel = Text(peer.IP, 11, Ink); ipLabel.Opacity = .5; secondary.Children.Add(ipLabel);
        text.Children.Add(secondary); Grid.SetColumn(text, 1); row.Children.Add(text);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var copy = IconButton("\uf0c5", $"複製 {peer.Name} 的 IP", (_, _) => Copy(peer.IP)); copy.IsEnabled = !string.IsNullOrEmpty(peer.IP);
        var more = IconButton("\uf142", $"{peer.Name} 的更多操作", null);
        var menu = new MenuFlyout();
        var send = new MenuFlyoutItem { Text = "傳送檔案…", FontFamily = new("MiSans TC"), FontSize = 14, MinHeight = 44, Icon = new FontIcon { Glyph = "\uf1d8", FontFamily = SolidFont, FontSize = 20 }, IsEnabled = state?.Connected == true && peer.Online && !string.IsNullOrEmpty(peer.IP) };
        send.Click += async (_, _) => await PickAndSendAsync(peer); menu.Items.Add(send);
        string desktopClient = Path.Combine(Environment.SystemDirectory, "mstsc.exe");
        if (peer.OS.Equals("windows", StringComparison.OrdinalIgnoreCase))
        {
            var remote = new MenuFlyoutItem { Text = "遠端桌面連線", FontFamily = new("MiSans TC"), FontSize = 14, MinHeight = 44, Icon = new FontIcon { Glyph = "\uf390", FontFamily = SolidFont, FontSize = 20 }, IsEnabled = peer.Online };
            remote.Click += async (_, _) => await OpenRemoteDesktopAsync(peer, desktopClient);
            menu.Items.Add(remote);
        }
        var historyItem = new MenuFlyoutItem { Text = "查看傳送紀錄", FontFamily = new("MiSans TC"), FontSize = 14, MinHeight = 44, Icon = new FontIcon { Glyph = "\uf1da", FontFamily = SolidFont, FontSize = 20 } }; historyItem.Click += (_, _) => ShowHistory(); menu.Items.Add(historyItem);
        LocalizeMenu(menu);more.Flyout = menu; actions.Children.Add(copy); actions.Children.Add(more);
        Grid.SetColumn(actions, 2); row.Children.Add(actions);
        row.MinHeight = 56;
        return Card(row, 4, 0);
    }
    static string DisplayOS(string os) => os.ToLowerInvariant() switch { "macos" or "darwin" => "macOS", "ios" => "iOS", "windows" => "Windows", "android" => "Android", "linux" => "Linux", _ => os };
    async Task ToggleConnectionAsync(bool target)
    {
        if (DemoMode) { syncing = true; connect.IsOn = true; syncing = false; return; }
        if (mutating || refreshing) { syncing = true; connect.IsOn = state?.Connected == true; syncing = false; return; }
        mutating = true; connect.IsEnabled = false; refresh.IsEnabled = false;
        statusTitle.Text = L(target ? "正在連線" : "正在中斷"); statusDetail.Text = L("請稍候…");
        var result = await service.ConnectAsync(target, lifetime.Token);
        mutating = false;
        await RefreshAsync();
        if (!result.Success) Toast(string.IsNullOrWhiteSpace(result.Message) ? "操作未完成，請開啟官方 Tailscale 查看狀態。" : result.Message, true, false);
    }
    void ResizePanel()
    {
        body.Visibility = quickMode || historyMode ? Visibility.Collapsed : Visibility.Visible;
        quickBody.Visibility = quickMode ? Visibility.Visible : Visibility.Collapsed;
        quickScroll.Visibility = quickMode ? Visibility.Visible : Visibility.Collapsed;
        historyScroll.Visibility = historyMode ? Visibility.Visible : Visibility.Collapsed;
        titleCaption.Text = L(historyMode ? "傳送紀錄" : quickMode ? "快速設定" : "Tailscale Capsule");
        if (historyHeaderButton != null) ((TextBlock)historyHeaderButton.Content).Text = historyMode ? "\uf060" : "\uf1da";
        double scale = Native.GetDpiForWindow(hwnd) / 96d;
        var work = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        peerScroll.MaxHeight = Math.Clamp(work.Height / scale - 400, 80, DemoMode ? 360 : 290);
        // The viewport and window must use the same height limit. The old
        // independent scroll cap left an empty strip below long settings pages.
        double maxBodyHeight = Math.Max(0, (work.Height - 16) / scale - header.Height);
        quickScroll.MaxHeight = maxBodyHeight;
        historyScroll.MaxHeight = maxBodyHeight;
        double width = quickMode ? 368 : 468;
        FrameworkElement panel = historyMode ? historyBody : quickMode ? quickBody : body;
        panel.Measure(new Windows.Foundation.Size(width, maxBodyHeight));
        double desired = header.Height + Math.Min(panel.DesiredSize.Height, maxBodyHeight);
        int height = Math.Min((int)Math.Ceiling(desired * scale), work.Height - 16);
        AppWindow.Resize(new((int)(width * scale), height));
        Native.RemoveFrame(hwnd);
        PositionFlyout();
    }
    void Toast(string message, bool error = false, bool autoClose = true)
    {
        if (exiting) return;
        notice.ActionButton = null; notice.Message = L(message.Length > 600 ? message[..600] + "…" : message);
        notice.Severity = error ? InfoBarSeverity.Warning : InfoBarSeverity.Success;
        notice.IsOpen = true; notice.Visibility = Visibility.Visible; toastTimer.Stop(); if (autoClose) toastTimer.Start(); ResizePanel();
    }
    void SystemColorsChanged(Windows.UI.ViewManagement.UISettings sender, object args)
    { DispatcherQueue.TryEnqueue(() => { if (!exiting) UpdateAccent(); }); }
    void UpdateAccent()
    {
        Gold = systemColors.GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent); Accent.Color = Gold;
        root.Resources["SystemAccentColor"] = Gold;
        root.Resources["ToggleSwitchFillOn"] = Accent;
        root.Resources["ToggleSwitchFillOnPointerOver"] = new SolidColorBrush(Color.FromArgb(255, (byte)Math.Min(255, Gold.R * .8 + 51), (byte)Math.Min(255, Gold.G * .8 + 51), (byte)Math.Min(255, Gold.B * .8 + 51)));
        root.Resources["ToggleSwitchFillOnPressed"] = Accent;
        root.Resources["ToggleSwitchKnobFillOn"] = (Gold.R * .2126 + Gold.G * .7152 + Gold.B * .0722) > 140 ? Brush(0xFF28251E) : Ink;
        ApplyMaterial(); RenderPeers();
    }
    void ApplyMaterial()
    {
        SystemBackdrop = preferences.Win7Glass ? new GoldGlassBackdrop(Gold) : null;
        if (!preferences.Win7Glass) { root.Background = Brush(0xFF202020); return; }
        var reflection = new LinearGradientBrush { StartPoint = new(1, 0), EndPoint = new(0, 1) };
        foreach (var (offset, argb) in new (double, uint)[]
        { (0, 0x60000000), (.06, 0x55000000), (.125, 0x45000000), (.156, 0x59000000), (.219, 0x50000000),
          (.31, 0x40000000), (.375, 0x52000000), (.437, 0x60000000), (.50, 0x4B000000), (.56, 0x56000000),
          (.625, 0x60000000), (.69, 0x49000000), (.75, 0x55000000), (.84, 0x62000000), (.93, 0x55000000), (1, 0x51000000) })
        {
            reflection.GradientStops.Add(new GradientStop { Offset = offset, Color = ((SolidColorBrush)Brush(argb)).Color });
        }
        root.Background = reflection;
    }
    void BuildQuickPanel()
    {
        quickBody.Children.Add(QuickButton("\uf390", "開啟裝置面板", (_, _) => ShowPanel()));
        quickBody.Children.Add(QuickButton("\uf1da", "傳送紀錄", (_, _) => ShowHistory()));
        quickBody.Children.Add(QuickButton("\uf2f1", "檢查更新", async (_, _) => await CheckUpdatesAsync(true)));
        quickBody.Children.Add(QuickSwitch("\uf011", "Tailscale 連線", out quickConnect, async (_, _) =>
        { if (!syncing) await ToggleConnectionAsync(quickConnect!.IsOn); }));
        quickCopy = QuickButton("\uf0c5", "複製本機 IP", (_, _) => Copy(state?.IP ?? "")); quickBody.Children.Add(quickCopy);
        quickBody.Children.Add(QuickButton("\uf2f1", "重新整理裝置", async (_, _) => await RefreshAsync(true)));
        quickBody.Children.Add(QuickSeparator());
        var languageRow=new Grid { MinHeight=50,Padding=new(12,0,12,0),ColumnSpacing=10 };
        languageRow.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        languageRow.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
        languageRow.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        languageRow.Children.Add(new Border{Width=24,Child=Icon("\uf1ab",17,Ink)});
        var languageLabel=Text(Locale.English?"Language":"語言",14,Ink);Grid.SetColumn(languageLabel,1);languageRow.Children.Add(languageLabel);
        var languageChoice=new ComboBox { MinWidth=132,FontSize=14,FontFamily=new("MiSans TC"),VerticalAlignment=VerticalAlignment.Center,SelectedIndex=Locale.English?1:0 };
        languageChoice.Items.Add("繁體中文");languageChoice.Items.Add("English");languageChoice.SelectedIndex=Locale.English?1:0;
        SetAccessibleName(languageChoice,"語言 / Language");
        languageChoice.SelectionChanged+=(_,_)=>ChangeLanguage(languageChoice.SelectedIndex==1?"en-US":"zh-TW");
        Grid.SetColumn(languageChoice,2);languageRow.Children.Add(languageChoice);quickBody.Children.Add(languageRow);
        quickBody.Children.Add(QuickSwitch("\uf5bd", "Win7 玻璃質感", out glassToggle, (_, _) =>
        {
            if (syncing) return;
            preferences.Win7Glass = glassToggle!.IsOn; preferences.Save(); ApplyMaterial();
        }));
        quickBody.Children.Add(QuickSwitch("\uf0f3", "更新通知", out updateToggle, async (_, _) =>
        { if(syncing)return;preferences.UpdateNotifications=updateToggle!.IsOn;preferences.Save();if(preferences.UpdateNotifications)await CheckUpdatesAsync(false); }));
        quickBody.Children.Add(QuickSwitch("\uf017", "開機啟動 Capsule", out startupToggle, (_, _) =>
        {
            if (syncing) return;
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                if (startupToggle!.IsOn) key.SetValue("TailscaleCapsule", $"\"{Environment.ProcessPath}\""); else key.DeleteValue("TailscaleCapsule", false);
            }
            catch (Exception ex) { App.Log(ex.Message); SyncQuickPanel(); ShowPanel(); Toast("無法儲存開機啟動設定。", true); }
        }));
        quickBody.Children.Add(QuickSeparator());
        quickBody.Children.Add(QuickButton("\uf35d", "開啟官方 Tailscale", (_, _) => OpenOfficial()));
        quickBody.Children.Add(QuickButton("\uf05a", "關於 Capsule", async (_, _) =>
        {
            pickingFiles = true;
            try
            {
                var content = new StackPanel { Spacing = 12 };
                var version=Text("v"+ReleaseUpdateClient.CurrentVersion,12,Muted);version.Opacity=.65;content.Children.Add(version);
                content.Children.Add(Text("WinUI 3 · MiSans TC",13,Muted));
                content.Children.Add(Text("Font Awesome Free 6.7.2",13,Muted));
                content.Children.Add(new HyperlinkButton { Content = L("彩色裝置圖示：Icons8"), NavigateUri = new Uri("https://icons8.com/icons"), FontSize=13,Padding=new(0),FontFamily = new("MiSans TC") });
                content.Children.Add(new Border { Height=1,Background=Brush(0x18FFFFFF),Margin=new(0,4,0,4) });
                content.Children.Add(Text("由 AuNight 製作",13,Ink));
                var dialog = new ContentDialog { Title = "Tailscale Capsule", Content = content, CloseButtonText = L("關閉"), XamlRoot = root.XamlRoot, RequestedTheme = ElementTheme.Dark }; await dialog.ShowAsync();
            }
            finally { pickingFiles = false; }
        }));
        quickBody.Children.Add(QuickSeparator());
        quickBody.Children.Add(QuickButton("\uf011", "結束程式", (_, _) => ExitApp()));
        SyncQuickPanel();
    }
    static Border QuickSeparator() => new() { Height = 1, Background = Brush(0x20FFFFFF), Margin = new(8, 8, 8, 8) };
    static Button QuickButton(string glyph, string label, RoutedEventHandler click)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
        content.Children.Add(new Border { Width = 24, Child = Icon(glyph, 17, Ink) }); content.Children.Add(Text(label, 14, Ink));
        var button = new Button { Content = content, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
            Height = 48, MinHeight = 48, Padding = new(12, 0, 12, 0), Background = Brush(0), BorderThickness = new(0), CornerRadius = new(8), Template = CleanButtonTemplate };
        SetAccessibleName(button, label); button.Click += click; return button;
    }
    static Grid QuickSwitch(string glyph, string label, out ToggleSwitch toggle, RoutedEventHandler changed)
    {
        var row = new Grid { Height = 50, Padding = new(12, 0, 12, 0), ColumnSpacing = 14 };
        row.ColumnDefinitions.Add(new() { Width = new(24) });
        row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        row.Children.Add(Icon(glyph, 17, Ink)); var text = Text(label, 14, Ink); Grid.SetColumn(text, 1); row.Children.Add(text);
        toggle = new ToggleSwitch { OffContent = "", OnContent = "", MinWidth = 44, VerticalAlignment = VerticalAlignment.Center };
        SetAccessibleName(toggle, label); toggle.Toggled += changed; Grid.SetColumn(toggle, 2); row.Children.Add(toggle);
        return row;
    }
    void SyncQuickPanel()
    {
        if (quickConnect == null || glassToggle == null || startupToggle == null) return;
        var previous = syncing; syncing = true;
        quickConnect.IsOn = state?.Connected == true; quickConnect.IsEnabled = state != null && state.Error == null && !mutating;
        glassToggle.IsOn = preferences.Win7Glass; startupToggle.IsOn = StartupEnabled();
        if(updateToggle!=null)updateToggle.IsOn=preferences.UpdateNotifications;
        if (quickCopy != null) quickCopy.IsEnabled = !string.IsNullOrEmpty(state?.IP);
        syncing = previous;
    }
    static bool StartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        return key?.GetValue("TailscaleCapsule") != null;
    }
    async void OpenOfficial()
    {
        if (DemoMode) return;
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tailscale", "tailscale-ipn.exe");
            var launch = new ProcessStartInfo(File.Exists(path) ? path : "https://tailscale.com/download/windows") { UseShellExecute = true };
            if (File.Exists(path))
            {
                launch.WorkingDirectory = Path.GetDirectoryName(path)!;
            }
            Process.Start(launch);
            if (File.Exists(path))
            {
                HidePanel();
                await Task.Delay(200);
                for (int attempt=0; attempt<12; attempt++)
                {
                    if (Native.ActivateOfficialClient()) return;
                    await Task.Delay(150);
                }
                ShowPanel(); Toast("官方 Tailscale 已啟動，請從系統匣開啟。", true);
            }
        }
        catch (Exception ex) { Toast(ex.Message, true); }
    }
    void PositionFlyout()
    {
        var work = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        AppWindow.Move(new(work.X + work.Width - AppWindow.Size.Width - 12, work.Y + work.Height - AppWindow.Size.Height - 12));
    }
    void TogglePanel() { if (AppWindow.IsVisible && !quickMode && !historyMode) HidePanel(); else ShowPanel(); }
    public void ShowPanel() { _ = SwitchPanelAsync(false); }
    public void ShowQuickPanel() { _ = SwitchPanelAsync(true); }
    async Task SwitchPanelAsync(bool quick, bool history = false)
    {
        if (exiting) return;
        ++visibilityRevision;
        bool switching = AppWindow.IsVisible && (quickMode != quick || historyMode != history);
        switchingPanel = true;
        try
        {
            // Resize an opaque surface. Fading the entire window to zero before
            // resizing can leave WinUI's backdrop visible without its content.
            surfaceAnimation?.Stop(); surfaceAnimation = null; root.Opacity = 1;
            pageAnimation?.Stop(); pageAnimation = null;
            quickMode = quick; historyMode = history; SyncQuickPanel(); if (history) RenderHistory();
            ResizePanel(); PositionFlyout(); root.UpdateLayout(); OpenAnimated(!switching);
            if (switching && new Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
            {
                FrameworkElement panel = history ? historyBody : quick ? quickBody : body;
                panel.Opacity = 1;
                var fade = new DoubleAnimation { From=.8,To=1,Duration=new(TimeSpan.FromMilliseconds(140)),FillBehavior=FillBehavior.Stop };
                Storyboard.SetTarget(fade,panel);Storyboard.SetTargetProperty(fade,"Opacity");
                var offset=new TranslateTransform();panel.RenderTransform=offset;
                var slide=new DoubleAnimation { From=10,To=0,Duration=new(TimeSpan.FromMilliseconds(160)),FillBehavior=FillBehavior.Stop,EnableDependentAnimation=true,
                    EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut} };
                Storyboard.SetTarget(slide,offset);Storyboard.SetTargetProperty(slide,"Y");
                pageAnimation = new Storyboard();pageAnimation.Children.Add(fade);pageAnimation.Children.Add(slide);pageAnimation.Begin();
            }
        }
        finally { switchingPanel = false; }
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        { if (quick) quickBody.Children.OfType<Button>().FirstOrDefault()?.Focus(FocusState.Programmatic); else connect.Focus(FocusState.Programmatic); });
        await RefreshAsync();
    }
    void OpenAnimated(bool animate = true)
    {
        visibilityRevision++;
        bool wasHidden = !AppWindow.IsVisible;
        if (wasHidden) { surfaceAnimation?.Stop(); root.Opacity = 0; surfaceTransform.ScaleX = surfaceTransform.ScaleY = 1; }
        AppWindow.Show(); Activate(); Native.SetForegroundWindow(hwnd);
        Native.RemoveFrame(hwnd);
        if (!animate) { surfaceAnimation?.Stop();surfaceAnimation=null;root.Opacity=1;return; }
        if (root.IsLoaded) AnimateSurface(1, 1, 120);
        else DispatcherQueue.TryEnqueue(() => AnimateSurface(1, 1, 120));
    }
    async void HidePanel()
    {
        if (exiting || switchingPanel || !AppWindow.IsVisible) return;
        int revision = ++visibilityRevision;
        bool animate = new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
        if (animate) { AnimateSurface(0, .99f, 90); await Task.Delay(95); }
        if (revision == visibilityRevision && !exiting)
        {
            AppWindow.Hide();surfaceAnimation?.Stop();surfaceAnimation=null;root.Opacity=1;
        }
    }
    void AnimateSurface(float opacity, float scale, int ms)
    {
        double currentOpacity = root.Opacity, currentScale = 1;
        surfaceAnimation?.Stop(); root.Opacity = opacity; surfaceTransform.ScaleX = surfaceTransform.ScaleY = currentScale;
        if (!new Windows.UI.ViewManagement.UISettings().AnimationsEnabled) { root.Opacity = opacity; surfaceTransform.ScaleX = surfaceTransform.ScaleY = scale; return; }
        surfaceTransform.CenterX = root.ActualWidth; surfaceTransform.CenterY = root.ActualHeight;
        var storyboard = new Storyboard();
        void Add(DependencyObject target, string property, double from, double to)
        {
            var animation = new DoubleAnimation { From = from, To = to, Duration = new(TimeSpan.FromMilliseconds(ms)), EnableDependentAnimation = true, FillBehavior=FillBehavior.Stop,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(animation, target); Storyboard.SetTargetProperty(animation, property); storyboard.Children.Add(animation);
        }
        Add(root, "Opacity", currentOpacity, opacity);
        storyboard.Completed += (_,_) => { if(ReferenceEquals(surfaceAnimation,storyboard)){root.Opacity=opacity;surfaceAnimation=null;} };
        surfaceAnimation = storyboard; storyboard.Begin();
    }
    void ShowCopyToast(string message)
    {
        copyToastText.Text = L(message); copyToast.Visibility = Visibility.Visible; copyToastTimer.Stop(); copyToastTimer.Start();
        if (new Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
        {
            var fade = new DoubleAnimation { From = 0, To = 1, Duration = new(TimeSpan.FromMilliseconds(130)) };
            Storyboard.SetTarget(fade, copyToast); Storyboard.SetTargetProperty(fade, "Opacity"); var animation = new Storyboard(); animation.Children.Add(fade); animation.Begin();
        }
    }
    async void QuickAction(int command)
    {
        try
        {
            switch (command)
            {
                case 3: await ToggleConnectionAsync(state?.Connected != true); break;
                case 4: Copy(state?.IP ?? ""); break;
                case 5: await RefreshAsync(true); break;
                case 6: OpenOfficial(); break;
            }
        }
        catch (Exception ex) { App.Log(ex.ToString()); Toast(ex.Message, true); }
    }
    void ExitApp() { exiting = true; preferences.Save(); Close(); Application.Current.Exit(); }
}
