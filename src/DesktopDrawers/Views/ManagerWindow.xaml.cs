using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using DesktopDrawers.Core.Models;
using DesktopDrawers.Services;
namespace DesktopDrawers.Views;
public partial class ManagerWindow : Window
{
    private readonly Workspace workspace;
    public ManagerWindow(Workspace workspace)
    {
        InitializeComponent(); this.workspace = workspace;
        workspace.Updated += () => { if (IsVisible) Refresh(); };
        Refresh();
    }
    private void Refresh()
    {
        Drawers.ItemsSource = null; Drawers.ItemsSource = workspace.State.Drawers;
        Workspace.Safe(() => StartupCheck.IsChecked = workspace.Startup.Enabled);
        IntegrationStatus.Text = workspace.IntegrationStatus;
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
