using System.Threading;
using System.Windows;
using System.Windows.Interop;
using DesktopDrawers.Core.Services;
using DesktopDrawers.Services;
namespace DesktopDrawers;
public partial class App : Application
{
    private Mutex? instance;
    private Workspace? workspace;
    private TrayService? tray;
    public ResidentLifetime Lifetime { get; } = new();
    public void ExitCompletely()
    {
        Lifetime.RequestExit();
        Shutdown();
    }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        instance = new Mutex(true, "Local\\DesktopDrawers-" + Environment.UserName, out var created);
        if (!created) { MessageBox.Show("Desktop Drawers ya está abierto. Usa su icono en la bandeja."); ExitCompletely(); return; }
        try
        {
            workspace = new Workspace();
            var manager = new Views.ManagerWindow(workspace);
            MainWindow = manager;
            // Create the native handle without showing configuration on automatic startup.
            new WindowInteropHelper(manager).EnsureHandle();
            tray = new TrayService(workspace, manager, ExitCompletely);
            workspace.Initialize(manager);
            if (!e.Args.Contains("--background", StringComparer.OrdinalIgnoreCase)) manager.OpenConfiguration();
            SessionEnding += (_, _) => { Lifetime.RequestExit(); workspace.Flush(); };
        }
        catch (Exception ex)
        {
            MessageBox.Show("No se pudo iniciar. Se conservan los datos.\n" + ex.Message, "Desktop Drawers");
            Lifetime.RequestExit(); Shutdown(1);
        }
    }
    protected override void OnExit(ExitEventArgs e)
    {
        tray?.Dispose(); workspace?.Dispose(); instance?.Dispose(); base.OnExit(e);
    }
}
