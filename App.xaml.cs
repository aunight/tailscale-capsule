using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;

namespace TailscaleCapsule;

public partial class App : Application
{
    private MainWindow? window;
    private Mutex? mutex;
    private EventWaitHandle? showSignal;
    private RegisteredWaitHandle? showWait;
    public App()
    {
        var languageArg=Environment.GetCommandLineArgs().FirstOrDefault(x=>x.StartsWith("--lang=",StringComparison.Ordinal));
        Locale.Initialize(languageArg==null?Preferences.Load().Language:languageArg[7..]);
        RequestedTheme = ApplicationTheme.Dark;
        InitializeComponent();
        UnhandledException += (_, e) => Log(e.Exception.ToString());
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var arguments=Environment.GetCommandLineArgs();int restartIndex=Array.IndexOf(arguments,"--restart");
        if(restartIndex>=0&&restartIndex+1<arguments.Length&&int.TryParse(arguments[restartIndex+1],out var previousPid))
        {try{using var previous=System.Diagnostics.Process.GetProcessById(previousPid);previous.WaitForExit(5000);}catch(ArgumentException){} }
        mutex = new Mutex(true, @"Local\TailscaleCapsule.WinUI3" + (MainWindow.DemoMode ? ".Demo" : ""), out var first);
        if (!first)
        {
            try { using var signal=EventWaitHandle.OpenExisting(@"Local\TailscaleCapsule.Show"+(MainWindow.DemoMode?".Demo":""));signal.Set(); }
            catch { Native.PostMessage(Native.FindWindow(null, "Tailscale Capsule · WinUI 3"), Native.WM_SHOWAPP, 0, 0); }
            Exit(); return;
        }
        try { window = new MainWindow(); } catch (Exception ex) { Log(ex.ToString()); throw; }
        showSignal=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\TailscaleCapsule.Show"+(MainWindow.DemoMode?".Demo":""));
        showWait=ThreadPool.RegisterWaitForSingleObject(showSignal,(_,_)=>window.DispatcherQueue.TryEnqueue(()=>window.ShowPanel()),null,Timeout.Infinite,false);
        window.Activate();
        bool show = Environment.GetCommandLineArgs().Contains("--show"), quick = Environment.GetCommandLineArgs().Contains("--quick"), history = Environment.GetCommandLineArgs().Contains("--history");
        if (!show && !quick && !history) window.AppWindow.Hide();
        window.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, async () =>
        {
            await window.InitializeAsync();
            if (history) window.ShowHistory();
            else if (quick) window.ShowQuickPanel();
            else if (show) window.ShowPanel();
        });
    }
    public static void Log(string message)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TailscaleCapsule");
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "app.log"), $"{DateTimeOffset.Now:O} {message}\n");
        }
        catch { }
    }
}
