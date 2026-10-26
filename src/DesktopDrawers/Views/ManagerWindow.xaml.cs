using System.ComponentModel;
using System.Windows.Controls;
using DesktopDrawers.Core.Services;
using System.Windows;
using System.Windows.Input;
using DesktopDrawers.Core.Models;
using DesktopDrawers.Services;
namespace DesktopDrawers.Views;
public partial class ManagerWindow : Window
{
    private readonly Workspace workspace;
    private bool refreshing;
    public ManagerWindow(Workspace workspace)
    {
        InitializeComponent(); this.workspace = workspace;
        workspace.Updated += () => { if (IsVisible) Refresh(); };
        Refresh();
    }
    private void Refresh()
    {
        refreshing = true;
        DesktopModeChoice.SelectedIndex = (int)workspace.Mode;
        Drawers.ItemsSource = null; Drawers.ItemsSource = workspace.State.Drawers;
        Workspace.Safe(() => StartupCheck.IsChecked = workspace.Startup.Enabled);
        IntegrationStatus.Text = workspace.IntegrationStatus + (workspace.State.Visible ? "" : " Los cajones están ocultos por el interruptor global.");
        refreshing = false;
    }
    private void DesktopModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (refreshing || workspace is null || DesktopModeChoice.SelectedIndex < 0) return;
        Workspace.Safe(() => workspace.SelectMode((DesktopMode)DesktopModeChoice.SelectedIndex));
        Refresh();
    }
    private void RecoverDrawers(object sender, RoutedEventArgs e) => workspace.RecoverVisible();
    private void ShowDiagnostics(object sender, RoutedEventArgs e)
    {
        Workspace.Safe(() => { workspace.CaptureDiagnostics(); MessageBox.Show("Diagnóstico guardado en:\n" + workspace.Diagnostics.FilePath, "Desktop Drawers"); });
    }
    public void OpenConfiguration()
    {
        Refresh(); Show(); WindowState = WindowState.Normal; Activate();
    }
    private void StartupChanged(object sender, RoutedEventArgs e)
    {
        Workspace.Safe(() => workspace.SetStartup(StartupCheck.IsChecked == true));
        Refresh();
    }
    private void Create(object sender, RoutedEventArgs e) => workspace.Create();
    private void Toggle(object sender, RoutedEventArgs e) => workspace.Toggle();
    private void Restore(object sender, RoutedEventArgs e) => workspace.RestoreAll();
    private void Edit(object sender, MouseButtonEventArgs e)
    {
        if (Drawers.SelectedItem is Drawer d) new DrawerSettings(workspace, d) { Owner = this }.ShowDialog();
    }
    private void HideConfiguration(object sender, RoutedEventArgs e) { workspace.Flush(); Hide(); }
    protected override void OnClosing(CancelEventArgs e)
    {
        if (((App)Application.Current).Lifetime.ShouldHideOnClose)
        { e.Cancel = true; workspace.Flush(); Hide(); }
        base.OnClosing(e);
    }
}
