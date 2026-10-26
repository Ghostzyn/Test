using System.IO;
using System.Windows;
using System.Windows.Threading;
using DesktopDrawers.Core.Models;
using DesktopDrawers.Core.Persistence;
using DesktopDrawers.Core.Services;
using DesktopDrawers.NativeInterop;
using DesktopDrawers.Views;

namespace DesktopDrawers.Services;
public sealed class Workspace : IDisposable
{
    public AppState State { get; }
    public StateStore Store { get; }
    public ShortcutService Shortcuts { get; }
    private readonly List<DrawerWindow> windows = [];
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private DesktopVisibility? desktop;
    private bool disposed;
    public event Action? Updated;
    public Workspace()
    {
        Store = new StateStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopDrawers"));
        State = Store.Load();
        Shortcuts = new ShortcutService(Store, State, [Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)]);
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); Safe(() => Store.Save(State)); };
    }
    public void Initialize(Window manager)
    {
        if (Store.RecoveredFromBackup) MessageBox.Show("Se recuperó la copia de seguridad del estado.");
        foreach (var d in State.Drawers) AddWindow(d);
        desktop = new DesktopVisibility(manager, RefreshVisibility, Toggle);
        if (!desktop.HotkeyRegistered) MessageBox.Show("Ctrl+Alt+D está ocupado. Usa el botón Mostrar / ocultar del administrador.");
        RefreshVisibility();
    }
    private void AddWindow(Drawer d)
    {
        var w = new DrawerWindow(this, d);
        windows.Add(w);
        w.Show();
    }
    public void Create()
    {
        var d = new Drawer { Left = 80 + State.Drawers.Count % 8 * 28, Top = 80 + State.Drawers.Count % 8 * 28 };
        State.Drawers.Add(d);
        AddWindow(d);
        Changed(); RefreshVisibility();
    }
    public void Changed()
    {
        saveTimer.Stop(); saveTimer.Start(); Updated?.Invoke();
    }
    public void Toggle() { State.Visible = !State.Visible; Changed(); RefreshVisibility(); }
    private void RefreshVisibility()
    {
        var show = State.Visible && DesktopVisibility.CanShowDrawers();
        foreach (var w in windows) { if (show) w.Show(); else w.Hide(); }
    }
    public void Delete(Drawer d)
    {
        if (MessageBox.Show($"¿Eliminar «{d.Name}»? Sus accesos se restaurarán antes de eliminar el cajón.", "Confirmar", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        Safe(() =>
        {
            foreach (var item in d.Items.ToArray()) Shortcuts.Restore(d, item);
            var w = windows.Single(w => w.Model == d);
            windows.Remove(w); w.Close(); State.Drawers.Remove(d); Store.Save(State); Updated?.Invoke();
        });
        RefreshItems();
    }
    public void RestoreAll()
    {
        if (MessageBox.Show("¿Restaurar todos los accesos a sus escritorios? No se sobrescribirán archivos.", "Restaurar", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        foreach (var d in State.Drawers)
            foreach (var i in d.Items.ToArray()) Safe(() => Shortcuts.Restore(d, i));
        RefreshItems();
    }
    public void RefreshItems() { foreach (var w in windows) w.RefreshItems(); Updated?.Invoke(); }
    public static void Safe(Action action)
    {
        try { action(); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Desktop Drawers — operación no completada", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; saveTimer.Stop(); desktop?.Dispose(); Safe(() => Store.Save(State));
    }
}
