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
        workspace.Updated += Refresh;
        Refresh();
    }
    private void Refresh() { Drawers.ItemsSource = null; Drawers.ItemsSource = workspace.State.Drawers; }
    private void Create(object sender, RoutedEventArgs e) => workspace.Create();
    private void Toggle(object sender, RoutedEventArgs e) => workspace.Toggle();
    private void Restore(object sender, RoutedEventArgs e) => workspace.RestoreAll();
    private void Edit(object sender, MouseButtonEventArgs e)
    {
        if (Drawers.SelectedItem is Drawer d) new DrawerSettings(workspace, d) { Owner = this }.ShowDialog();
    }
    private void Exit(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
    protected override void OnClosing(CancelEventArgs e) { base.OnClosing(e); Application.Current.Shutdown(); }
}
