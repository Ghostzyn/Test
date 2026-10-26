using System.IO;
using System.Text.Json;
namespace DesktopDrawers.Services;
public sealed class DiagnosticLog(string directory)
{
    public string FilePath { get; } = Path.Combine(directory, "desktop-diagnostics.jsonl");
    public string? LastError { get; private set; }
    public void Write(string action, object details)
    {
        try
        {
            Directory.CreateDirectory(directory);
            if (File.Exists(FilePath) && new FileInfo(FilePath).Length > 1024 * 1024)
                File.Move(FilePath, FilePath + ".previous", overwrite: true);
            File.AppendAllText(FilePath, JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow, action, details }) + Environment.NewLine);
            LastError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { LastError = ex.Message; }
    }
}
