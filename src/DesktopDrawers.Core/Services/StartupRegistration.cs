namespace DesktopDrawers.Core.Services;

public interface IStartupStore
{
    string? Read();
    void Write(string command);
    void Remove();
}

// One owned per-user entry; the registry is the persistent source of truth.
public sealed class StartupRegistration(IStartupStore store, string executable, string? managedAssembly = null)
{
    public bool Enabled => store.Read() is not null;
    public string Command { get; } = BuildCommand(executable, managedAssembly);
    public void SetEnabled(bool enabled)
    {
        if (enabled) store.Write(Command);
        else store.Remove();
    }
    public static string BuildCommand(string executable, string? managedAssembly = null)
    {
        static string Quote(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Contains('"'))
                throw new ArgumentException("Ruta de inicio inválida.");
            return "\"" + value + "\"";
        }
        return Quote(executable) + (managedAssembly is null ? "" : " " + Quote(managedAssembly)) + " --background";
    }
}

public sealed class ResidentLifetime
{
    public bool ExitRequested { get; private set; }
    public bool ShouldHideOnClose => !ExitRequested;
    public void RequestExit() => ExitRequested = true;
}
