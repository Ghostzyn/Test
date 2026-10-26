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
    private bool rebuilding;
    private readonly DispatcherTimer recoveryTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    public StartupRegistration Startup { get; } = StartupService.Create();
    public string IntegrationStatus { get; private set; } = "";
    public event Action? Updated;
    public Workspace()
    {
        Store = new StateStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopDrawers"));
        State = Store.Load();
        Shortcuts = new ShortcutService(Store, State, [Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)]);
        recoveryTimer.Tick += (_, _) => { recoveryTimer.Stop(); RecoverDesktop(); };
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); Safe(() => Store.Save(State)); };
    }
    public void Initialize(Window manager)
    {
        if (Store.RecoveredFromBackup) MessageBox.Show("Se recuperó la copia de seguridad del estado.");
        desktop = new DesktopVisibility(manager, ScheduleRecovery, Toggle);
        if (!desktop.HotkeyRegistered) MessageBox.Show("Ctrl+Alt+D está ocupado. Usa la bandeja o el administrador.");
        RecoverDesktop();
    }
    private void ScheduleRecovery()
    {
        if (disposed || recoveryTimer.IsEnabled) return;
        recoveryTimer.Interval = TimeSpan.FromSeconds(3); recoveryTimer.Start();
    }
    private void RecoverDesktop()
    {
        if (disposed || rebuilding) return;
        rebuilding = true;
        try
        {
            foreach (var w in windows.ToArray()) w.Close();
            windows.Clear();
            foreach (var d in State.Drawers) AddWindow(d);
            IntegrationStatus = "Cajones alojados en el escritorio; sin ocultación por foco.";
        }
        catch (Exception ex)
        {
            IntegrationStatus = ex.Message;
            // Retry only while integration is unavailable, never poll foreground windows.
            recoveryTimer.Interval = TimeSpan.FromSeconds(30); recoveryTimer.Start();
        }
        finally { rebuilding = false; RefreshVisibility(); Updated?.Invoke(); }
    }
    private void AddWindow(Drawer d)
    {
        var w = new DrawerWindow(this, d);
        try
        {
            // Initialize WPF's top-level styles before SetParent. Never call Show/
            // Hide on the managed window again; native visibility preserves WS_CHILD.
            w.Opacity = 0;
            w.Show();
            desktop!.Attach(w);
            w.IsDesktopHosted = true;
            DesktopVisibility.Place(w, d.Left, d.Top);
            DesktopVisibility.SetVisible(w, State.Visible);
            w.Opacity = 1;
            windows.Add(w);
            w.Closed += (_, _) =>
            {
                windows.Remove(w);
                if (!disposed && !rebuilding && State.Drawers.Contains(d)) ScheduleRecovery();
            };
        }
        catch { w.Close(); throw; }
    }
    public void SetStartup(bool enabled)
    {
        Startup.SetEnabled(enabled);
        Updated?.Invoke();
    }
    public void Flush() { saveTimer.Stop(); Safe(() => Store.Save(State)); }
    public void Create()
    {
        var d = new Drawer { Left = 80 + State.Drawers.Count % 8 * 28, Top = 80 + State.Drawers.Count % 8 * 28 };
        State.Drawers.Add(d);
        Safe(() => { try { AddWindow(d); } catch { ScheduleRecovery(); throw; } });
        Changed(); RefreshVisibility();
    }
    public void Changed()
    {
        saveTimer.Stop(); saveTimer.Start(); Updated?.Invoke();
    }
    public void Toggle() { State.Visible = !State.Visible; Changed(); RefreshVisibility(); }
    private void RefreshVisibility()
    {
        foreach (var w in windows) DesktopVisibility.SetVisible(w, State.Visible);
    }
    public void Delete(Drawer d)
    {
        if (MessageBox.Show($"¿Eliminar «{d.Name}»? Sus accesos se restaurarán antes de eliminar el cajón.", "Confirmar", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        Safe(() =>
        {
            foreach (var item in d.Items.ToArray()) Shortcuts.Restore(d, item);
            State.Drawers.Remove(d);
            var w = windows.SingleOrDefault(w => w.Model == d);
            if (w is not null) { windows.Remove(w); w.Close(); }
            Store.Save(State); Updated?.Invoke();
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
        disposed = true; saveTimer.Stop(); recoveryTimer.Stop(); desktop?.Dispose(); Safe(() => Store.Save(State));
    }
}
