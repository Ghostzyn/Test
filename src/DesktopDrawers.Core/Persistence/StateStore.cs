using System.Text.Json;
using DesktopDrawers.Core.Models;

namespace DesktopDrawers.Core.Persistence;

public sealed class StateStore(string directory)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    private string FilePath => Path.Combine(DirectoryPath, "state.json");
    public bool RecoveredFromBackup { get; private set; }

    public AppState Load()
    {
        if (!File.Exists(FilePath) && !File.Exists(FilePath + ".bak")) return new();
        try { return Read(FilePath); }
        catch (Exception e) when (e is JsonException or IOException or InvalidDataException)
        {
            // Never overwrite unreadable state with an empty document.
            var state = Read(FilePath + ".bak");
            RecoveredFromBackup = true;
            return state;
        }
    }

    private static AppState Read(string path)
    {
        var state = JsonSerializer.Deserialize<AppState>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException("Estado vacío.");
        if (state.Version != 1 || state.Drawers is null || state.Drawers.Any(d => d is null || d.Items is null))
            throw new InvalidDataException("Formato de estado no compatible.");
        if (state.Drawers.Select(d => d.Id).Distinct().Count() != state.Drawers.Count)
            throw new InvalidDataException("Identificadores duplicados.");
        foreach (var d in state.Drawers)
        {
            d.Width = FiniteClamp(d.Width, 240, 2000, 340);
            d.Height = FiniteClamp(d.Height, 120, 2000, 300);
            d.Left = double.IsFinite(d.Left) ? d.Left : 80;
            d.Top = double.IsFinite(d.Top) ? d.Top : 80;
            d.Opacity = FiniteClamp(d.Opacity, .1, 1, .9);
        }
        return state;
    }

    private static double FiniteClamp(double v, double min, double max, double fallback) =>
        double.IsFinite(v) ? Math.Clamp(v, min, max) : fallback;

    public void Save(AppState state)
    {
        Directory.CreateDirectory(DirectoryPath);
        var temp = FilePath + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, state, Options);
                stream.Flush(true);
            }
            if (File.Exists(FilePath))
            {
                // Preserve the last good backup when recovering a damaged primary.
                File.Replace(temp, FilePath, RecoveredFromBackup ? null : FilePath + ".bak");
            }
            else File.Move(temp, FilePath);
            RecoveredFromBackup = false;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
