using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;

namespace TailscaleCapsule;

internal static class Native
{
    delegate bool EnumWindowProc(nint hwnd, nint parameter);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowProc callback, nint parameter);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(nint hwnd, StringBuilder name, int count);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);
    public static bool ActivateOfficialClient()
    {
        nint target = 0;
        EnumWindows((window, _) =>
        {
            var name = new StringBuilder(128); GetClassName(window, name, name.Capacity);
            if (name.ToString() != "WalkNotifyIconSink") return true;
            GetWindowThreadProcessId(window, out uint pid);
            try { using var process = Process.GetProcessById((int)pid); if (process.ProcessName != "tailscale-ipn") return true; }
            catch { return true; }
            target = window; return false;
        }, 0);
        if (target == 0) return false;
        GetCursorPos(out var point);
        nint position = (nint)((point.Y & 0xffff) << 16 | (point.X & 0xffff));
        // Tailscale's Walk tray sink allocates WM_USER+3 as its first class
        // message; WM_CONTEXTMENU opens its own official menu.
        return PostMessage(target, 0x0403, position, 0x007B);
    }
    public const uint WM_SHOWAPP = 0x8002, WM_TRAY = 0x8001;
    public delegate nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam);
    public delegate nint SubclassProc(nint hwnd, uint msg, nint wParam, nint lParam, nuint id, nuint data);
    [DllImport("comctl32.dll")] public static extern bool SetWindowSubclass(nint hwnd, SubclassProc proc, nuint id, nuint data);
    [DllImport("comctl32.dll")] public static extern bool RemoveWindowSubclass(nint hwnd, SubclassProc proc, nuint id);
    [DllImport("comctl32.dll")] public static extern nint DefSubclassProc(nint hwnd, uint msg, nint wParam, nint lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern nint FindWindow(string? cls, string title);
    [DllImport("user32.dll")] public static extern bool PostMessage(nint hwnd, uint msg, nint wp, nint lp);
    [DllImport("user32.dll")] public static extern nint SendMessage(nint hwnd, uint msg, nint wp, nint lp);
    [DllImport("user32.dll")] public static extern bool ReleaseCapture();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] public static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] public static extern int SetWindowRgn(nint hwnd, nint region, bool redraw);
    [DllImport("gdi32.dll")] public static extern nint CreateRoundRectRgn(int left, int top, int right, int bottom, int ellipseWidth, int ellipseHeight);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(nint handle);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    public static void RemoveFrame(nint hwnd)
    {
        const long frameStyles = 0x00C00000L | 0x00040000L | 0x00080000L | 0x00030000L;
        long style = GetWindowLongPtr(hwnd, -16).ToInt64();
        if ((style & frameStyles) != 0)
        {
            SetWindowLongPtr(hwnd, -16, (nint)(style & ~frameStyles));
            SetWindowPos(hwnd, 0, 0, 0, 0, 0, 0x0027); // FRAMECHANGED, no move/size/z-order
        }
        int dark = 1; DwmSetWindowAttribute(hwnd, 20, ref dark, 4);
        // Own the rounded outline: this also clips the residual WinUI non-client
        // stroke on systems where DWM's border-color suppression is ignored.
        int round = 1; DwmSetWindowAttribute(hwnd, 33, ref round, 4);
        int policy = 1; DwmSetWindowAttribute(hwnd, 2, ref policy, 4);
        int none = unchecked((int)0xFFFFFFFE); int setResult = DwmSetWindowAttribute(hwnd, 34, ref none, 4);
        if (GetWindowRect(hwnd, out var bounds))
        {
            int diameter = Math.Max(16, (int)(24 * GetDpiForWindow(hwnd) / 96.0));
            nint region = CreateRoundRectRgn(1, 1, bounds.Right - bounds.Left - 1, bounds.Bottom - bounds.Top - 1, diameter, diameter);
            if (region != 0 && SetWindowRgn(hwnd, region, true) == 0) DeleteObject(region);
        }
        if (Environment.GetCommandLineArgs().Contains("--inspect"))
        {
            int hr = DwmGetWindowAttribute(hwnd, 34, out int actual, 4);
            App.Log($"Frame: style=0x{GetWindowLongPtr(hwnd,-16):X}; border=0x{actual:X8}; set=0x{setResult:X8}; get=0x{hr:X8}");
        }
    }
    [DllImport("user32.dll")] public static extern nint CallWindowProc(nint previous, nint hwnd, uint msg, nint wp, nint lp);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern nint LoadImage(nint instance, string name, uint type, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(nint icon);
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(nint hwnd, int attr, out int value, int size);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern bool Shell_NotifyIcon(uint message, ref NotifyData data);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern uint ExtractIconEx(string file, int index, nint[] large, nint[] small, uint count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")] public static extern nint CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool AppendMenu(nint menu, uint flags, nuint id, string text);
    [DllImport("user32.dll")] public static extern int TrackPopupMenu(nint menu, uint flags, int x, int y, int reserved, nint hwnd, nint rect);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] public static extern bool DestroyMenu(nint menu);
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct NotifyData
    {
        public uint Size; public nint Hwnd; public uint Id, Flags, Callback; public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Timeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags; public Guid Guid; public nint BalloonIcon;
    }
}

internal sealed class TrayIcon : IDisposable
{
    private Native.NotifyData data;
    private readonly Native.SubclassProc procedure;
    private readonly Action show, toggleVisibility, showQuick;
    private readonly uint taskbarCreated;
    private bool? connected;
    readonly bool enabled;
    static nint OfficialIcon(bool online)
    {
        string official = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tailscale", "tailscale-ipn.exe");
        if (File.Exists(official))
        {
            var large = new nint[1]; var small = new nint[1];
            if (Native.ExtractIconEx(official, online ? 4 : 3, large, small, 1) > 0)
            {
                if (large[0] != 0) Native.DestroyIcon(large[0]);
                if (small[0] != 0) return small[0];
            }
        }
        return Native.LoadImage(0, Path.Combine(AppContext.BaseDirectory, "Assets", "tray-dots.ico"), 1, 16, 16, 0x10);
    }
    public TrayIcon(nint hwnd, Action show, Action toggleVisibility, Action showQuick, bool enabled = true)
    {
        this.enabled = enabled;
        this.show = show; this.toggleVisibility = toggleVisibility; this.showQuick = showQuick;
        procedure = OnMessage;
        if (!Native.SetWindowSubclass(hwnd, procedure, 1, 0)) throw new InvalidOperationException("無法初始化系統匣視窗訊息。");
        taskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");
        data = new Native.NotifyData { Size = (uint)Marshal.SizeOf<Native.NotifyData>(), Hwnd = hwnd, Id = 1,
            Flags = 1 | 2 | 4, Callback = Native.WM_TRAY, Tip = "Tailscale Capsule",
            Icon = OfficialIcon(false), Info = "", InfoTitle = "" };
        if(enabled) Native.Shell_NotifyIcon(0, ref data);
    }
    public void Update(string text, bool online)
    {
        if(!enabled)return;
        data.Tip = text.Length > 127 ? text[..127] : text;
        nint old = 0;
        if (connected != online) { old = data.Icon; data.Icon = OfficialIcon(online); connected = online; }
        Native.Shell_NotifyIcon(1, ref data); if (old != 0) Native.DestroyIcon(old);
    }
    nint OnMessage(nint hwnd, uint msg, nint wp, nint lp, nuint id, nuint context)
    {
        if (msg == 0x0083) return 0; // WM_NCCALCSIZE: the whole window is client content.
        if (msg == taskbarCreated && enabled) Native.Shell_NotifyIcon(0, ref data);
        if (msg == Native.WM_SHOWAPP) { show(); return 0; }
        if (msg == Native.WM_TRAY)
        {
            if ((int)lp == 0x202) toggleVisibility();
            if ((int)lp == 0x205)
            {
                showQuick();
            }
            return 0;
        }
        return Native.DefSubclassProc(hwnd, msg, wp, lp);
    }
    public void Dispose()
    {
        if(enabled) Native.Shell_NotifyIcon(2, ref data);
        Native.RemoveWindowSubclass(data.Hwnd, procedure, 1);
        if (data.Icon != 0) Native.DestroyIcon(data.Icon);
    }
}
