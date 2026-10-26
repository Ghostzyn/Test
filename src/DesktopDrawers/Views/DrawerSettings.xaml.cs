using System.Text.RegularExpressions;
using System.Windows;
using DesktopDrawers.Core.Models;
using DesktopDrawers.Services;
namespace DesktopDrawers.Views;
public partial class DrawerSettings : Window
{
    private readonly Workspace workspace;
    private readonly Drawer drawer;
    public DrawerSettings(Workspace workspace, Drawer drawer)
    {
        InitializeComponent(); this.workspace = workspace; this.drawer = drawer;
        DrawerName.Text = drawer.Name; BackgroundHex.Text = drawer.Background;
        TitleHex.Text = drawer.TitleColor; OpacitySlider.Value = drawer.Opacity * 100;
    }
    private void Save(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(DrawerName.Text) || !Regex.IsMatch(BackgroundHex.Text, "^#[0-9a-fA-F]{6}$") || !Regex.IsMatch(TitleHex.Text, "^#[0-9a-fA-F]{6}$"))
        { MessageBox.Show("Escribe un nombre y colores HEX válidos, por ejemplo #172235."); return; }
        drawer.Name = DrawerName.Text.Trim(); drawer.Background = BackgroundHex.Text;
        drawer.TitleColor = TitleHex.Text; drawer.Opacity = OpacitySlider.Value / 100;
        workspace.Changed(); DialogResult = true;
    }
    private void Delete(object sender, RoutedEventArgs e) { Close(); workspace.Delete(drawer); }
}
