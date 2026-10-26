using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace DesktopDrawers.NativeInterop;

// Supported foreground notifications, no injected code or global mouse hooks.
// Explorer's internal WorkerW hierarchy is intentionally not modified.
public sealed class DesktopVisibility : IDisposable
{
    private readonly Action changed;
    private readonly WinEventDelegate callback;
    private readonly HwndSource source;
    private nint hook;
    private const int HotkeyId = 8137;
    public bool HotkeyRegistered { get; }
    public DesktopVisibility(Window manager, Action changed, Action toggle)
    {
        this.changed = changed;
        source = HwndSource.FromHwnd(new WindowInteropHelper(manager).Handle);
        callback = (_, _, _, _, _, _, _) => manager.Dispatcher.BeginInvoke(changed);
        hook = SetWinEventHook(3, 3, 0, callback, 0, 0, 0); // EVENT_SYSTEM_FOREGROUND, OUTOFCONTEXT
        if (hook == 0) throw new InvalidOperationException("Windows no permitió observar el cambio de ventana activa.");
        HotkeyRegistered = RegisterHotKey(source.Handle, HotkeyId, 0x4000 | 0x0002 | 0x0001, 0x44); // Ctrl+Alt+D, no repeat
        source.AddHook((nint hwnd, int msg, nint w, nint l, ref bool handled) =>
        {
            if (msg == 0x0312 && w == HotkeyId) { toggle(); handled = true; }
            return 0;
        });
    }
    public static bool CanShowDrawers()
    {
        var hwnd = GetForegroundWindow();
        GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == Environment.ProcessId) return true;
        var name = new StringBuilder(256);
        GetClassName(hwnd, name, name.Capacity);
        return name.ToString() is "Progman" or "WorkerW";
    }
    public void Refresh() => changed();
    public void Dispose()
    {
        if (hook != 0) { UnhookWinEvent(hook); hook = 0; }
        if (HotkeyRegistered) UnregisterHotKey(source.Handle, HotkeyId);
    }
    private delegate void WinEventDelegate(nint hook, uint evt, nint hwnd, int objectId, int childId, uint thread, uint time);
    [DllImport("user32.dll")] private static extern nint SetWinEventHook(uint min, uint max, nint module, WinEventDelegate callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(nint hook);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint hwnd, StringBuilder name, int count);
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint hwnd, int id);
}
