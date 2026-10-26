using System.ComponentModel;
using DesktopDrawers.Core.Services;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace DesktopDrawers.NativeInterop;

// No foreground-based visibility rules. Compatibility uses independent non-activating
// windows. Experimental integration uses native children of Explorer's icon surface.
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
                // Use the icon surface itself, not a sibling behind DefView/wallpaper.
                if ((ClassName(host) is "Progman" or "WorkerW") && IsWindowVisible(child)) result = child;
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
        if (parent == 0) throw new InvalidOperationException("Explorer no expone un escritorio compatible (Progman/WorkerW con SHELLDLL_DefView). Se utilizará compatibilidad.");
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var style = GetWindowLongPtr(hwnd, -16).ToInt64();
        SetStyle(hwnd, -16, (style | 0x40000000L | 0x04000000L) & ~0x80000000L); // CHILD, CLIPSIBLINGS, !POPUP
        var ex = GetWindowLongPtr(hwnd, -20).ToInt64();
        SetStyle(hwnd, -20, (ex | 0x80L) & ~0x40000L); // TOOLWINDOW, !APPWINDOW
        Marshal.SetLastPInvokeError(0);
        var previous = SetParent(hwnd, parent);
        if (previous == 0 && Marshal.GetLastPInvokeError() != 0) throw new Win32Exception(Marshal.GetLastPInvokeError(), "No se pudo alojar el cajón en Explorer (posible incompatibilidad DPI).");
        if (GetParent(hwnd) != parent) throw new InvalidOperationException("Explorer no conservó el alojamiento del cajón.");
        Require(SetWindowPos(hwnd, 0, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 | 0x0020), "SetWindowPos al alojar"); // frame changed, no activation
    }
    private static void SetStyle(nint hwnd, int index, long value)
    {
        Marshal.SetLastPInvokeError(0);
        var previous = SetWindowLongPtr(hwnd, index, (nint)value);
        if (previous == 0 && Marshal.GetLastPInvokeError() != 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
    }
    public static void ConfigureCompatibility(Window window)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        // Independent tool window. Clicking its content must not raise it over apps.
        var ex = GetWindowLongPtr(hwnd, -20).ToInt64();
        SetStyle(hwnd, -20, (ex | 0x80L | 0x08000000L) & ~0x40008L); // NOACTIVATE, TOOLWINDOW, !APPWINDOW, !TOPMOST
    }
    public static void EnsureReachable(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (!GetWindowRect(hwnd, out var rect)) throw new Win32Exception(Marshal.GetLastPInvokeError());
        // A virtual-screen bounding box may contain gaps between monitors.
        if (MonitorFromPoint(new POINT { X = rect.Left + 20, Y = rect.Top + 15 }, 0) == 0)
        {
            window.Left = SystemParameters.WorkArea.Left + 40;
            window.Top = SystemParameters.WorkArea.Top + 40;
        }
    }
    public static void LowerCompatibility(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        nint after = 0;
        // Keep the drawer after normal applications but before the first visible
        // desktop root. HWND_BOTTOM would also put it behind the wallpaper.
        var shell = GetShellWindow(); GetWindowThreadProcessId(shell, out var shellPid);
        EnumWindows((candidate, _) =>
        {
            if (candidate == hwnd || !IsWindowVisible(candidate) || IsCloaked(candidate)) return true;
            GetWindowThreadProcessId(candidate, out var pid);
            if (pid == shellPid && (ClassName(candidate) is "Progman" or "WorkerW")) return false;
            // Do not order after a topmost window: that would promote this window.
            if ((GetWindowLongPtr(candidate, -20).ToInt64() & 8) == 0) after = candidate;
            return true;
        }, 0);
        Require(SetWindowPos(hwnd, after, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010), "Orden de compatibilidad");
    }
    private static void Require(bool success, string operation)
    {
        if (!success) throw new Win32Exception(Marshal.GetLastPInvokeError(), operation);
    }
    public static void Place(Window window, double left, double top)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var host = GetParent(hwnd);
        if (!IsWindow(hwnd) || !IsWindow(host)) throw new InvalidOperationException("El padre del cajón dejó de existir.");
        var scale = PresentationSource.FromVisual(window)?.CompositionTarget?.TransformToDevice ?? System.Windows.Media.Matrix.Identity;
        var position = new POINT { X = (int)Math.Round(left * scale.M11), Y = (int)Math.Round(top * scale.M22) };
        Require(ScreenToClient(host, ref position), "ScreenToClient");
        var width = (int)Math.Round(window.Width * scale.M11);
        var height = (int)Math.Round(window.Height * scale.M22);
        if (width < 1 || height < 1) throw new InvalidOperationException("Dimensiones nativas no válidas.");
        // Reapply dimensions and HWND_TOP among icon-surface children. Do not retain
        // a zero-sized or stale rectangle after WPF's layered-window updates.
        Require(SetWindowPos(hwnd, 0, position.X, position.Y, width, height, 0x0010), "Posición/tamaño del cajón");
    }
    public (DesktopProbe Probe, object Details) Inspect(Window window, bool integrated)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var actualParent = GetParent(hwnd);
        var root = GetAncestor(hwnd, 2);
        var style = GetWindowLongPtr(hwnd, -16).ToInt64();
        var exStyle = GetWindowLongPtr(hwnd, -20).ToInt64();
        var hasRect = GetWindowRect(hwnd, out var rect);
        GetClientRect(actualParent, out var client);
        var clientOrigin = new POINT(); ClientToScreen(actualParent, ref clientOrigin);
        var center = new POINT { X = rect.Left + (rect.Right - rect.Left) / 2, Y = rect.Top + Math.Min(20, (rect.Bottom - rect.Top) / 2) };
        var screenCenter = center;
        ScreenToClient(actualParent, ref center);
        var topChild = ChildWindowFromPointEx(actualParent, center, 1 | 2 | 4);
        var covered = false;
        var layers = new List<object>();
        GetWindowThreadProcessId(GetShellWindow(), out var shellPid);
        var beforeRoot = true;
        EnumWindows((candidate, _) =>
        {
            if (candidate == root) beforeRoot = false;
            GetWindowThreadProcessId(candidate, out var pid);
            if (pid != shellPid || ClassName(candidate) is not ("Progman" or "WorkerW")) return true;
            GetWindowRect(candidate, out var layerRect);
            var visible = IsWindowVisible(candidate); var cloaked = IsCloaked(candidate);
            if (beforeRoot && visible && !cloaked && Contains(layerRect, screenCenter)) covered = true;
            layers.Add(new { hwnd = candidate.ToInt64(), type = ClassName(candidate), visible, cloaked, beforeRoot, rect = Describe(layerRect) });
            return true;
        }, 0);
        var probe = new DesktopProbe
        {
            ParentValid = !integrated || IsWindow(parent), ParentMatches = !integrated || actualParent == parent,
            AncestorsVisible = !integrated || IsWindowVisible(parent), WpfVisible = window.Visibility == Visibility.Visible && window.Opacity > 0,
            NativeVisible = IsWindowVisible(hwnd), SizeValid = hasRect && rect.Right > rect.Left && rect.Bottom > rect.Top,
            OnScreen = MonitorFromRect(ref rect, 0) != 0,
            WithinParent = !integrated || (rect.Left >= clientOrigin.X && rect.Top >= clientOrigin.Y && rect.Right <= clientOrigin.X + client.Right && rect.Bottom <= clientOrigin.Y + client.Bottom),
            ChildStyle = (style & 0x40000000L) != 0, PopupStyle = (style & 0x80000000L) != 0,
            TopmostStyle = (exStyle & 8) != 0, Cloaked = IsCloaked(hwnd) || (integrated && IsCloaked(parent)),
            SurfaceOnTop = topChild == hwnd || IsChild(hwnd, topChild), ExplorerLayerAbove = integrated && covered
        };
        return (probe, new
        {
            hwnd = hwnd.ToInt64(), actualParent = actualParent.ToInt64(), parentClass = ClassName(actualParent),
            expectedParent = parent.ToInt64(), parentValid = IsWindow(parent), root = root.ToInt64(), style, exStyle,
            nativeRect = Describe(rect), clientRect = Describe(client), parentScreenOrigin = new { clientOrigin.X, clientOrigin.Y },
            window.Width, window.Height, window.ActualWidth, window.ActualHeight, window.Left, window.Top,
            visibility = window.Visibility.ToString(), window.IsVisible, window.Opacity,
            dpi = GetDpiForWindow(hwnd), childAtTitle = topChild.ToInt64(), probe, shellLayers = layers
        });
    }
    private static bool Contains(RECT r, POINT p) => p.X >= r.Left && p.X < r.Right && p.Y >= r.Top && p.Y < r.Bottom;
    private static object Describe(RECT r) => new { r.Left, r.Top, r.Right, r.Bottom };
    private static bool IsCloaked(nint hwnd) => hwnd != 0 && DwmGetWindowAttribute(hwnd, 14, out var cloaked, sizeof(int)) == 0 && cloaked != 0;
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
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    private delegate bool EnumDelegate(nint hwnd, nint param);
    private delegate void WinEventDelegate(nint hook, uint evt, nint hwnd, int objectId, int childId, uint thread, uint time);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint hwnd, int attribute, out int value, int size);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint hwnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(nint hwnd, ref POINT point);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsChild(nint parent, nint child);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint hwnd, uint flags);
    [DllImport("user32.dll")] private static extern nint ChildWindowFromPointEx(nint hwnd, POINT point, uint flags);
    [DllImport("user32.dll")] private static extern nint MonitorFromPoint(POINT point, uint flags);
    [DllImport("user32.dll")] private static extern nint MonitorFromRect(ref RECT rect, uint flags);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumDelegate callback, nint param);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(nint parent, EnumDelegate callback, nint param);
    [DllImport("user32.dll")] private static extern nint GetShellWindow();
    [DllImport("user32.dll")] private static extern nint GetParent(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetParent(nint hwnd, nint parent);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool ScreenToClient(nint hwnd, ref POINT point);
    [DllImport("user32.dll")] private static extern nint SetWinEventHook(uint min, uint max, nint module, WinEventDelegate callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(nint hook);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint hwnd, StringBuilder name, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint hwnd, int id);
}
