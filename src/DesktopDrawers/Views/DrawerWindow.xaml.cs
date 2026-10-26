using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopDrawers.Core.Models;
using DesktopDrawers.Services;
using DesktopDrawers.ViewModels;
namespace DesktopDrawers.Views;
public partial class DrawerWindow : Window
{
    private readonly Workspace workspace;
    public Drawer Model { get; }
    private bool updating = true;
    private Point dragOrigin;
    public DrawerWindow(Workspace workspace, Drawer model)
    {
        this.workspace = workspace; Model = model;
        InitializeComponent();
        // Keep at least the title bar reachable after monitor configuration changes.
        Left = Math.Clamp(model.Left, SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 100);
        Top = Math.Clamp(model.Top, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 44);
        Width = model.Width;
        ApplyAppearance(); RefreshItems();
        workspace.Updated += ApplyAppearance;
        Closed += (_, _) => workspace.Updated -= ApplyAppearance;
        Loaded += (_, _) => { updating = false; CaptureGeometry(); };
    }
    private static Color Parse(string text, Color fallback)
    {
        try { return (Color)ColorConverter.ConvertFromString(text); } catch { return fallback; }
    }
    public void ApplyAppearance()
    {
        updating = true;
        var color = Parse(Model.Background, Colors.DarkSlateGray);
        color.A = (byte)(255 * Math.Clamp(Model.Opacity, .1, 1));
        Frame.Background = new SolidColorBrush(color);
        TitleText.Text = Model.Name;
        TitleText.Foreground = new SolidColorBrush(Parse(Model.TitleColor, Colors.White));
        Items.Visibility = Model.Expanded ? Visibility.Visible : Visibility.Collapsed;
        Arrow.Content = Model.Expanded ? "▾" : "▸";
        MinHeight = Model.Expanded ? 120 : 44;
        ResizeMode = Model.Expanded ? ResizeMode.CanResize : ResizeMode.NoResize;
        Height = Model.Expanded ? Model.Height : 46;
        updating = false;
    }
    public void RefreshItems() => Items.ItemsSource = Model.Items.Select(i => new ShortcutViewModel(i)).ToList();
    private void ToggleExpanded(object sender, RoutedEventArgs e) { Model.Expanded = !Model.Expanded; ApplyAppearance(); workspace.Changed(); }
    private void Settings(object sender, RoutedEventArgs e) => new DrawerSettings(workspace, Model) { Owner = this }.ShowDialog();
    private void DragHeader(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) { ToggleExpanded(sender, e); return; }
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
    private void GeometryChanged(object sender, SizeChangedEventArgs e) => CaptureGeometry();
    private void LocationChangedHandler(object? sender, EventArgs e) => CaptureGeometry();
    private void CaptureGeometry()
    {
        if (updating || !IsLoaded) return;
        Model.Left = Left; Model.Top = Top; Model.Width = Width;
        if (Model.Expanded) Model.Height = Height;
        workspace.Changed();
    }
    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(ShortcutDrag)) || e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }
    private void OnDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (e.Data.GetData(typeof(ShortcutDrag)) is ShortcutDrag move)
            Workspace.Safe(() => workspace.Shortcuts.Move(move.Source, Model, move.Item));
        else if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            foreach (var path in paths) Workspace.Safe(() => workspace.Shortcuts.Import(Model, path));
        workspace.RefreshItems();
    }
    private void RememberDrag(object sender, MouseButtonEventArgs e) => dragOrigin = e.GetPosition(Items);
    private void StartDrag(object sender, MouseEventArgs e)
    {
        var p = e.GetPosition(Items);
        if (e.LeftButton != MouseButtonState.Pressed || (Math.Abs(p.X - dragOrigin.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(p.Y - dragOrigin.Y) < SystemParameters.MinimumVerticalDragDistance)) return;
        if (Items.SelectedItem is ShortcutViewModel item)
            DragDrop.DoDragDrop(Items, new DataObject(typeof(ShortcutDrag), new ShortcutDrag(Model, item.Model)), DragDropEffects.Move);
    }
    private void SelectRightClicked(object sender, MouseButtonEventArgs e)
    {
        var target = Items.ContainerFromElement((DependencyObject)e.OriginalSource) as ListBoxItem;
        if (target != null) target.IsSelected = true;
    }
    private void Open(object sender, RoutedEventArgs e)
    {
        if (Items.SelectedItem is not ShortcutViewModel item) return;
        Workspace.Safe(() =>
        {
            workspace.Shortcuts.ValidateManagedPath(item.Model.StoredPath);
            if (!File.Exists(item.Model.StoredPath)) throw new FileNotFoundException("No se encuentra la copia administrada. Intenta restaurar el acceso.");
            Process.Start(new ProcessStartInfo(item.Model.StoredPath) { UseShellExecute = true });
        });
    }
    private void Restore(object sender, RoutedEventArgs e)
    {
        if (Items.SelectedItem is ShortcutViewModel item) Workspace.Safe(() => workspace.Shortcuts.Restore(Model, item.Model));
        workspace.RefreshItems();
    }
}
