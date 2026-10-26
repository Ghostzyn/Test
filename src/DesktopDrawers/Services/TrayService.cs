using System.Windows;
using DesktopDrawers.Views;
using Forms = System.Windows.Forms;

namespace DesktopDrawers.Services;
public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon icon;
    private readonly Forms.ContextMenuStrip menu = new();
    private readonly Forms.ToolStripMenuItem startup = new("Iniciar con Windows") { CheckOnClick = false };
    private readonly Forms.ToolStripMenuItem visibility = new("Mostrar/ocultar cajones");
    public TrayService(Workspace workspace, ManagerWindow manager, Action exit)
    {
        menu.Items.Add("Abrir configuración", null, (_, _) => Dispatch(manager.OpenConfiguration));
        visibility.Click += (_, _) => Dispatch(workspace.Toggle);
        menu.Items.Add(visibility);
        startup.Click += (_, _) => Dispatch(() => workspace.SetStartup(!workspace.Startup.Enabled));
        menu.Items.Add(startup);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Salir completamente", null, (_, _) => Dispatch(exit));
        menu.Opening += (_, _) =>
        {
            Workspace.Safe(() => startup.Checked = workspace.Startup.Enabled);
            visibility.Checked = workspace.State.Visible;
        };
        icon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "Desktop Drawers", ContextMenuStrip = menu, Visible = true
        };
        icon.DoubleClick += (_, _) => Dispatch(manager.OpenConfiguration);
    }
    private static void Dispatch(Action action) => Application.Current.Dispatcher.BeginInvoke(() => Workspace.Safe(action));
    public void Dispose() { icon.Visible = false; icon.Dispose(); menu.Dispose(); }
}
