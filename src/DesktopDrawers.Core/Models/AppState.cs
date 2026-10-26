namespace DesktopDrawers.Core.Models;

public sealed class AppState
{
    public DesktopDrawers.Core.Services.DesktopMode DesktopMode { get; set; } = DesktopDrawers.Core.Services.DesktopMode.Compatibility;
    public int Version { get; set; } = 1;
    public bool Visible { get; set; } = true;
    public List<Drawer> Drawers { get; set; } = [];
}

public sealed class Drawer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Nuevo cajón";
    public double Left { get; set; } = 80;
    public double Top { get; set; } = 80;
    public double Width { get; set; } = 340;
    public double Height { get; set; } = 300;
    public bool Expanded { get; set; } = true;
    public string Background { get; set; } = "#172235";
    public string TitleColor { get; set; } = "#FFFFFF";
    public double Opacity { get; set; } = .9;
    public List<Shortcut> Items { get; set; } = [];
}

public sealed class Shortcut
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string OriginalPath { get; set; } = "";
    public string StoredPath { get; set; } = "";
}
