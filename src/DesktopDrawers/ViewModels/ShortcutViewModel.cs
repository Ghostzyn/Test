using System.IO;
using System.Windows.Media;
using DesktopDrawers.Core.Models;
using DesktopDrawers.Services;
namespace DesktopDrawers.ViewModels;
public sealed class ShortcutViewModel(Shortcut model)
{
    public Shortcut Model { get; } = model;
    public string Name => Model.Name;
    public ImageSource? Icon { get; } = File.Exists(model.StoredPath) ? ShellIcons.Load(model.StoredPath) : null;
}
public sealed record ShortcutDrag(Drawer Source, Shortcut Item);
