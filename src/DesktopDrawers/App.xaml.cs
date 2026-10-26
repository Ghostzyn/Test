using System.Threading;
using System.Windows;
using DesktopDrawers.Services;

namespace DesktopDrawers;
public partial class App : Application
{
    private Mutex? instance;
    private Workspace? workspace;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        instance = new Mutex(true, "Local\\DesktopDrawers-" + Environment.UserName, out var created);
        if (!created) { MessageBox.Show("Desktop Drawers ya está abierto."); Shutdown(); return; }
        try
        {
            workspace = new Workspace();
            var manager = new Views.ManagerWindow(workspace);
            MainWindow = manager;
            manager.Show();
            workspace.Initialize(manager);
        }
        catch (Exception ex)
        {
            MessageBox.Show("No se pudo iniciar. No se sobrescribirán los datos.\n" + ex.Message, "Desktop Drawers");
            Shutdown(1);
        }
    }
    protected override void OnExit(ExitEventArgs e)
    {
        workspace?.Dispose();
        instance?.Dispose();
        base.OnExit(e);
    }
}
