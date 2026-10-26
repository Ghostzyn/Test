using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace DesktopDrawers.NativeInterop;

// No foreground-based visibility rules. Drawers are native children of the shell
// desktop host, so normal top-level application windows naturally cover them.
// Explorer's hierarchy is undocumented: fail closed and retry, never use Topmost.
public sealed class DesktopVisibility : IDisposable
{
    private readonly HwndSource source;
    private readonly HwndSourceHook messageHook;
    private readonly WinEventDelegate destroyCallback;
    private readonly Action recover;
    private readonly uint taskbarCreated;
    private nint destroyHook;
    private nint parent;
    private bool disposed;
    private const int HotkeyId = 8137;
    public bool HotkeyRegistered { get; }

    public DesktopVisibility(Window manager, Action recover, Action toggle)
    {
        this.recover = recover;
        source = HwndSource.FromHwnd(new WindowInteropHelper(manager).Handle);
        taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        messageHook = (nint hwnd, int msg, nint w, nint l, ref bool handled) =>
        {
            if (msg == 0x0312 && w == HotkeyId) { toggle(); handled = true; }
            if ((uint)msg == taskbarCreated || msg == 0x007E) QueueRecovery(); // WM_DISPLAYCHANGE
            return 0;
        };
        source.AddHook(messageHook);
        HotkeyRegistered = RegisterHotKey(source.Handle, HotkeyId, 0x4000 | 0x0002 | 0x0001, 0x44);
        destroyCallback = (_, _, hwnd, objectId, _, _, _) =>
        {
            if (hwnd == parent && objectId == 0) QueueRecovery();
        };
        // Only native window destruction, never keyboard/mouse or focus events.
        destroyHook = SetWinEventHook(0x8001, 0x8001, 0, destroyCallback, 0, 0, 2);
    }
    private void QueueRecovery()
    {
        if (!disposed) source.Dispatcher.BeginInvoke(() => { if (!disposed) recover(); });
    }
    public static nint FindDesktopHost()
    {
        var shell = GetShellWindow();
        if (shell == 0) return 0;
        GetWindowThreadProcessId(shell, out var shellPid);
        nint result = 0;
        EnumWindows((top, _) =>
        {
            GetWindowThreadProcessId(top, out var pid);
            if (pid != shellPid) return true;
            // Search descendants too: recent Explorer versions can nest WorkerW.
            EnumChildWindows(top, (child, _) =>
            {
                if (ClassName(child) != "SHELLDLL_DefView") return true;
                var host = GetParent(child);
                if (ClassName(host) is "Progman" or "WorkerW") result = host;
                return result == 0;
            }, 0);
            return result == 0;
        }, 0);
        return result;
    }
    private static string ClassName(nint hwnd)
    {
        var text = new StringBuilder(256); GetClassName(hwnd, text, text.Capacity); return text.ToString();
    }
    public void Attach(Window window)
    {
        parent = FindDesktopHost();
        if (parent == 0) throw new InvalidOperationException("Explorer no expone un escritorio compatible (Progman/WorkerW con SHELLDLL_DefView). Se reintentará sin poner cajones encima de las aplicaciones.");
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var style = GetWindowLongPtr(hwnd, -16).ToInt64();
        SetStyle(hwnd, -16, (style | 0x40000000L | 0x04000000L) & ~0x80000000L); // CHILD, CLIPSIBLINGS, !POPUP
        var ex = GetWindowLongPtr(hwnd, -20).ToInt64();
        SetStyle(hwnd, -20, (ex | 0x80L) & ~0x40000L); // TOOLWINDOW, !APPWINDOW
        Marshal.SetLastPInvokeError(0);
        var previous = SetParent(hwnd, parent);
        if (previous == 0 && Marshal.GetLastPInvokeError() != 0) throw new Win32Exception(Marshal.GetLastPInvokeError(), "No se pudo alojar el cajón en Explorer (posible incompatibilidad DPI).");
        if (GetParent(hwnd) != parent) throw new InvalidOperationException("Explorer no conservó el alojamiento del cajón.");
        SetWindowPos(hwnd, 0, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 | 0x0020); // frame changed, no activation
    }
    private static void SetStyle(nint hwnd, int index, long value)
    {
        Marshal.SetLastPInvokeError(0);
        var previous = SetWindowLongPtr(hwnd, index, (nint)value);
        if (previous == 0 && Marshal.GetLastPInvokeError() != 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
    }
    public static void Place(Window window, double left, double top)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var host = GetParent(hwnd);
        if (host == 0 || !IsWindow(host)) return;
        var scale = PresentationSource.FromVisual(window)?.CompositionTarget?.TransformToDevice ?? System.Windows.Media.Matrix.Identity;
        var position = new POINT { X = (int)Math.Round(left * scale.M11), Y = (int)Math.Round(top * scale.M22) };
        ScreenToClient(host, ref position);
        // Size is managed by WPF; coordinates here are native parent-client pixels.
        SetWindowPos(hwnd, 0, position.X, position.Y, 0, 0, 0x0001 | 0x0004 | 0x0010);
    }
    public static void SetVisible(Window window, bool visible)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (IsWindow(hwnd)) ShowWindow(hwnd, visible ? 4 : 0); // SHOWNOACTIVATE / HIDE
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (destroyHook != 0) { UnhookWinEvent(destroyHook); destroyHook = 0; }
        source.RemoveHook(messageHook);
        if (HotkeyRegistered) UnregisterHotKey(source.Handle, HotkeyId);
    }
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    private delegate bool EnumDelegate(nint hwnd, nint param);
    private delegate void WinEventDelegate(nint hook, uint evt, nint hwnd, int objectId, int childId, uint thread, uint time);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumDelegate callback, nint param);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(nint parent, EnumDelegate callback, nint param);
    [DllImport("user32.dll")] private static extern nint GetShellWindow();
    [DllImport("user32.dll")] private static extern nint GetParent(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetParent(nint hwnd, nint parent);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll")] private static extern bool ScreenToClient(nint hwnd, ref POINT point);
    [DllImport("user32.dll")] private static extern nint SetWinEventHook(uint min, uint max, nint module, WinEventDelegate callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(nint hook);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint hwnd, StringBuilder name, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint hwnd, int id);
}
