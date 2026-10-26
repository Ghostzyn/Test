using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using DesktopDrawers.NativeInterop;
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
    private Point? headerAnchor;
    private Point drawerAnchor;
    public bool IsDesktopHosted { get; set; }
    public DrawerWindow(Workspace workspace, Drawer model)
    {
        this.workspace = workspace; Model = model;
        InitializeComponent();
        // Keep at least the title bar reachable after monitor configuration changes.
        Left = Math.Clamp(model.Left, SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 100);
        Top = Math.Clamp(model.Top, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 44);
        model.Left = Left; model.Top = Top;
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
        ResizeGrip.Visibility = Model.Expanded ? Visibility.Visible : Visibility.Collapsed;
        Height = Model.Expanded ? Model.Height : 46;
        updating = false;
        if (IsDesktopHosted) DesktopVisibility.Place(this, Model.Left, Model.Top);
    }
    public void RefreshItems() => Items.ItemsSource = Model.Items.Select(i => new ShortcutViewModel(i)).ToList();
    private void ToggleExpanded(object sender, RoutedEventArgs e) { Model.Expanded = !Model.Expanded; ApplyAppearance(); workspace.Changed(); }
    private void Settings(object sender, RoutedEventArgs e) => new DrawerSettings(workspace, Model) { Owner = Application.Current.MainWindow }.ShowDialog();
    private void DragHeader(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject original && FindButton(original)) return;
        if (e.ClickCount == 2) { ToggleExpanded(sender, e); return; }
        if (e.LeftButton != MouseButtonState.Pressed) return;
        headerAnchor = PointToScreen(e.GetPosition(this));
        drawerAnchor = new Point(Model.Left, Model.Top);
        ((UIElement)sender).CaptureMouse(); e.Handled = true;
    }
    private static bool FindButton(DependencyObject? current)
    {
        while (current is not null)
        {
            if (current is ButtonBase) return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }
    private void MoveHeader(object sender, MouseEventArgs e)
    {
        if (headerAnchor is not Point anchor || e.LeftButton != MouseButtonState.Pressed) return;
        var current = PointToScreen(e.GetPosition(this));
        var scale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        Model.Left = drawerAnchor.X + (current.X - anchor.X) / scale.M11;
        Model.Top = drawerAnchor.Y + (current.Y - anchor.Y) / scale.M22;
        DesktopVisibility.Place(this, Model.Left, Model.Top); workspace.Changed();
    }
    private void EndHeaderDrag(object sender, MouseButtonEventArgs e) { headerAnchor = null; ((UIElement)sender).ReleaseMouseCapture(); }
    private void EndHeaderCapture(object sender, MouseEventArgs e) => headerAnchor = null;
    private void ResizeDrawer(object sender, DragDeltaEventArgs e)
    {
        if (!Model.Expanded) return;
        Width = Math.Clamp(Width + e.HorizontalChange, MinWidth, 2000);
        Height = Math.Clamp(Height + e.VerticalChange, MinHeight, 2000);
        CaptureGeometry();
        DesktopVisibility.Place(this, Model.Left, Model.Top);
    }
    private void GeometryChanged(object sender, SizeChangedEventArgs e) => CaptureGeometry();
    private void LocationChangedHandler(object? sender, EventArgs e) => CaptureGeometry();
    private void CaptureGeometry()
    {
        if (updating || !IsLoaded) return;
        if (!IsDesktopHosted) { Model.Left = Left; Model.Top = Top; }
        Model.Width = Width;
        if (Model.Expanded) Model.Height = Height;
        workspace.Changed();
    }
    private bool Accepts(DragEventArgs e)
    {
        if ((e.AllowedEffects & DragDropEffects.Move) == 0) return false;
        if (e.Data.GetDataPresent(typeof(ShortcutDrag))) return true;
        return e.Data.GetData(DataFormats.FileDrop, true) is string[] { Length: > 0 } paths
            && paths.All(workspace.Shortcuts.CanImport);
    }
    private void OnDragOver(object sender, DragEventArgs e)
    {
        var accepts = Accepts(e);
        e.Effects = accepts ? DragDropEffects.Move : DragDropEffects.None;
        DropHint.Visibility = accepts ? Visibility.Visible : Visibility.Collapsed;
        Frame.BorderBrush = accepts ? Brushes.DodgerBlue : new SolidColorBrush(Color.FromArgb(96, 123, 135, 153));
        e.Handled = true;
    }
    private void OnDragLeave(object sender, DragEventArgs e)
    {
        DropHint.Visibility = Visibility.Collapsed;
        Frame.BorderBrush = new SolidColorBrush(Color.FromArgb(96, 123, 135, 153));
    }
    private void OnDrop(object sender, DragEventArgs e)
    {
        e.Handled = true; e.Effects = DragDropEffects.None;
        OnDragLeave(sender, e);
        if (!Accepts(e)) return;
        Workspace.Safe(() =>
        {
            if (e.Data.GetData(typeof(ShortcutDrag)) is ShortcutDrag move)
                workspace.Shortcuts.Move(move.Source, Model, move.Item);
            else if (e.Data.GetData(DataFormats.FileDrop, true) is string[] paths)
                foreach (var path in paths) workspace.Shortcuts.Import(Model, path);
            e.Effects = DragDropEffects.Move;
        });
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
