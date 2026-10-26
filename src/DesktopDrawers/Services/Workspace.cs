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
    private int automaticRecoveries;
    private readonly IntegrationSession integration;
    private readonly DispatcherTimer validationTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    public DiagnosticLog Diagnostics { get; }
    public DesktopMode Mode => integration.Mode;
    private readonly DispatcherTimer recoveryTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    public StartupRegistration Startup { get; } = StartupService.Create();
    public string IntegrationStatus { get; private set; } = "";
    public event Action? Updated;
    public Workspace()
    {
        Store = new StateStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopDrawers"));
        State = Store.Load();
        integration = new IntegrationSession(State.DesktopMode);
        Diagnostics = new DiagnosticLog(Store.DirectoryPath);
        Diagnostics.Write("start", new { version = "0.2.1", os = Environment.OSVersion.VersionString, mode = Mode.ToString(), State.Visible, drawers = State.Drawers.Count });
        validationTimer.Tick += (_, _) => { validationTimer.Stop(); ValidateIntegrated(); };
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
        if (disposed || rebuilding || recoveryTimer.IsEnabled) return;
        if (Mode == DesktopMode.Compatibility && windows.Count == State.Drawers.Count)
        {
            foreach (var w in windows) Safe(() => DesktopVisibility.LowerCompatibility(w));
            return;
        }
        if (automaticRecoveries >= 2)
        {
            FallbackToCompatibility("Se alcanzó el límite de dos recuperaciones automáticas.");
            return;
        }
        automaticRecoveries++;
        recoveryTimer.Start();
    }
    public void SelectMode(DesktopMode mode)
    {
        integration.Select(mode); State.DesktopMode = mode; automaticRecoveries = 0;
        Flush(); RecoverDesktop();
    }
    public void RecoverVisible()
    {
        // Explicit user action: recover independently even if global visibility was off.
        State.Visible = true; SelectMode(DesktopMode.Compatibility);
    }
    public void ReportPlacementFailure(Exception ex)
    {
        Diagnostics.Write("placement-failed", new { error = ex.Message });
        Application.Current.Dispatcher.BeginInvoke(() => { if (!disposed) ScheduleRecovery(); });
    }
    private void RecoverDesktop()
    {
        if (disposed || rebuilding) return;
        rebuilding = true; recoveryTimer.Stop(); validationTimer.Stop();
        try
        {
            CloseDrawers();
            foreach (var d in State.Drawers) AddWindow(d);
            RefreshVisibility();
            if (Mode == DesktopMode.Integrated)
            {
                ValidateIntegratedCore();
                IntegrationStatus = "Integrado: comprobaciones nativas superadas; confirma que ves los cajones. Si no, usa Recuperar cajones.";
                validationTimer.Start(); // One deferred check after WPF rendering, no endless retries.
            }
            else IntegrationStatus = "Compatibilidad: ventanas independientes, sin Topmost. Selecciona Integrado solo para probar Explorer.";
        }
        catch (Exception ex)
        {
            Diagnostics.Write("rebuild-failed", new { mode = Mode.ToString(), error = ex.ToString() });
            if (Mode == DesktopMode.Integrated)
            {
                integration.FailIntegrated(); State.DesktopMode = Mode; Flush();
                CloseDrawers();
                try
                {
                    foreach (var d in State.Drawers) AddWindow(d);
                    RefreshVisibility();
                    IntegrationStatus = "Se activó compatibilidad: " + ex.Message;
                }
                catch (Exception fallbackError)
                {
                    Diagnostics.Write("compatibility-failed", new { error = fallbackError.ToString() });
                    IntegrationStatus = "No se pudieron recuperar todas las ventanas. Usa Recuperar cajones. " + fallbackError.Message;
                }
            }
            else IntegrationStatus = "Error de compatibilidad; usa Recuperar cajones. " + ex.Message;
        }
        finally
        {
            rebuilding = false;
            Updated?.Invoke();
        }
    }
    private void CloseDrawers()
    {
        foreach (var w in windows.ToArray()) w.Close();
        windows.Clear();
    }
    private void FallbackToCompatibility(string reason)
    {
        if (disposed || rebuilding) return;
        Diagnostics.Write("fallback", new { reason });
        integration.FailIntegrated(); State.DesktopMode = DesktopMode.Compatibility;
        integration.Select(DesktopMode.Compatibility); Flush();
        RecoverDesktop();
        IntegrationStatus = "Compatibilidad activada: " + reason;
        Updated?.Invoke();
    }
    private void ValidateIntegratedCore()
    {
        foreach (var w in windows)
        {
            var snapshot = desktop!.Inspect(w, integrated: true);
            var failures = snapshot.Probe.Failures(State.Visible);
            Diagnostics.Write("integrated-check", new { drawer = w.Model.Id, State.Visible, snapshot.Details, failures });
            if (failures.Length > 0) throw new InvalidOperationException(string.Join("; ", failures));
        }
    }
    private void ValidateIntegrated()
    {
        if (disposed || rebuilding || Mode != DesktopMode.Integrated) return;
        try { ValidateIntegratedCore(); }
        catch (Exception ex) { FallbackToCompatibility(ex.Message); }
    }
    public void CaptureDiagnostics()
    {
        Diagnostics.Write("user-report", new { mode = Mode.ToString(), State.Visible, drawers = State.Drawers.Count, windows = windows.Count });
        foreach (var w in windows)
        {
            var snapshot = desktop!.Inspect(w, w.IsDesktopHosted);
            Diagnostics.Write("window", new { drawer = w.Model.Id, snapshot.Details });
        }
        if (Diagnostics.LastError is string error) throw new IOException("No se pudo guardar el diagnóstico: " + error);
    }
    private void AddWindow(Drawer d)
    {
        var w = new DrawerWindow(this, d);
        try
        {
            if (Mode == DesktopMode.Integrated) w.Opacity = 0;
            if (Mode == DesktopMode.Compatibility) DesktopVisibility.ConfigureCompatibility(w);
            w.Show();
            if (Mode == DesktopMode.Integrated)
            {
                desktop!.Attach(w); w.IsDesktopHosted = true;
                DesktopVisibility.Place(w, d.Left, d.Top);
            }
            else
            {
                DesktopVisibility.ConfigureCompatibility(w);
                DesktopVisibility.EnsureReachable(w);
                DesktopVisibility.LowerCompatibility(w);
            }
            // Opacity changes can cause WPF to recalculate layered-window geometry.
            // Place and validate again after the final nonzero opacity has been set.
            if (Mode == DesktopMode.Integrated) w.Opacity = 1;
            w.UpdateLayout();
            if (w.IsDesktopHosted) DesktopVisibility.Place(w, d.Left, d.Top);
            DesktopVisibility.SetVisible(w, State.Visible);
            if (!w.IsDesktopHosted) DesktopVisibility.LowerCompatibility(w);
            w.CompleteInitialization();
            windows.Add(w);
            var snapshot = desktop!.Inspect(w, w.IsDesktopHosted);
            Diagnostics.Write("window-created", new { drawer = d.Id, mode = Mode.ToString(), model = new { d.Left, d.Top, d.Width, d.Height }, snapshot.Details });
            if (!w.IsDesktopHosted && (!snapshot.Probe.WpfVisible || (State.Visible && !snapshot.Probe.NativeVisible)
                || !snapshot.Probe.SizeValid || !snapshot.Probe.OnScreen || snapshot.Probe.ChildStyle
                || snapshot.Probe.TopmostStyle || snapshot.Probe.Cloaked))
                throw new InvalidOperationException("La ventana independiente no superó las comprobaciones de visibilidad. Consulta el diagnóstico.");
            w.Closed += (_, _) =>
            {
                windows.Remove(w);
                if (!disposed && !rebuilding && State.Drawers.Contains(d)) ScheduleRecovery();
            };
        }
        catch
        {
            // Record the partially attached HWND before destroying it too.
            try { Diagnostics.Write("window-create-failed", desktop!.Inspect(w, w.IsDesktopHosted).Details); }
            catch (Exception ex) { Diagnostics.Write("snapshot-failed", new { error = ex.Message }); }
            windows.Remove(w); w.Close(); throw;
        }
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
        Changed(); RecoverDesktop();
    }
    public void Changed()
    {
        saveTimer.Stop(); saveTimer.Start(); Updated?.Invoke();
    }
    public void Toggle()
    {
        State.Visible = !State.Visible; Changed(); RefreshVisibility();
        if (Mode == DesktopMode.Integrated) { validationTimer.Stop(); validationTimer.Start(); }
    }
    private void RefreshVisibility()
    {
        foreach (var w in windows)
        {
            DesktopVisibility.SetVisible(w, State.Visible);
            if (!w.IsDesktopHosted) DesktopVisibility.LowerCompatibility(w);
        }
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
        disposed = true; saveTimer.Stop(); recoveryTimer.Stop(); validationTimer.Stop(); desktop?.Dispose(); Safe(() => Store.Save(State));
    }
}
